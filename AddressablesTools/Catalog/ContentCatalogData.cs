using AddressablesTools.Binary;
using AddressablesTools.JSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AddressablesTools.Catalog
{
    public class ContentCatalogData
    {
        // only used for binary format
        public int Version { get; set; }

        public string LocatorId { get; set; }
        public string BuildResultHash { get; set; }
        public ObjectInitializationData InstanceProviderData { get; set; }
        public ObjectInitializationData SceneProviderData { get; set; }
        public ObjectInitializationData[] ResourceProviderData { get; set; }

        // used for resources for the json format, shouldn't be edited directly
        private string[] ProviderIds { get; set; }
        private string[] InternalIds { get; set; }
        private string[] Keys { get; set; } // for old versions
        private SerializedType[] ResourceTypes { get; set; }
        private string[] InternalIdPrefixes { get; set; }

        public Dictionary<object, List<ResourceLocation>> Resources { get; set; }

        internal void Read(ContentCatalogDataJson data)
        {
            LocatorId = data.m_LocatorId;
            BuildResultHash = data.m_BuildResultHash;

            InstanceProviderData = new ObjectInitializationData();
            InstanceProviderData.Read(data.m_InstanceProviderData);

            SceneProviderData = new ObjectInitializationData();
            SceneProviderData.Read(data.m_SceneProviderData);

            ResourceProviderData = new ObjectInitializationData[data.m_ResourceProviderData.Length];
            for (int i = 0; i < ResourceProviderData.Length; i++)
            {
                ResourceProviderData[i] = new ObjectInitializationData();
                ResourceProviderData[i].Read(data.m_ResourceProviderData[i]);
            }

            ProviderIds = new string[data.m_ProviderIds.Length];
            for (int i = 0; i < ProviderIds.Length; i++)
            {
                ProviderIds[i] = data.m_ProviderIds[i];
            }

            InternalIds = new string[data.m_InternalIds.Length];
            for (int i = 0; i < InternalIds.Length; i++)
            {
                InternalIds[i] = data.m_InternalIds[i];
            }

            if (data.m_Keys != null)
            {
                Keys = new string[data.m_Keys.Length];
                for (int i = 0; i < Keys.Length; i++)
                {
                    Keys[i] = data.m_Keys[i];
                }
            }
            else
            {
                Keys = null;
            }

            ResourceTypes = new SerializedType[data.m_resourceTypes.Length];
            for (int i = 0; i < ResourceTypes.Length; i++)
            {
                ResourceTypes[i] = new SerializedType();
                ResourceTypes[i].Read(data.m_resourceTypes[i]);
            }

            if (data.m_InternalIdPrefixes != null)
            {
                InternalIdPrefixes = new string[data.m_InternalIdPrefixes.Length];
                for (int i = 0; i < InternalIdPrefixes.Length; i++)
                {
                    InternalIdPrefixes[i] = data.m_InternalIdPrefixes[i];
                }
            }
            else
            {
                InternalIdPrefixes = null;
            }

            ReadResources(data);
        }

        internal void Read(CatalogBinaryReader reader)
        {
            ContentCatalogDataBinaryHeader header = new ContentCatalogDataBinaryHeader();
            header.Read(reader);

            Version = reader.Version;

            LocatorId = reader.ReadEncodedString(header.IdOffset);
            BuildResultHash = reader.ReadEncodedString(header.BuildResultHashOffset);

            InstanceProviderData = new ObjectInitializationData();
            InstanceProviderData.Read(reader, header.InstanceProviderOffset);

            SceneProviderData = new ObjectInitializationData();
            SceneProviderData.Read(reader, header.SceneProviderOffset);

            uint[] resourceProviderDataOffsets = reader.ReadOffsetArray(header.InitObjectsArrayOffset);
            ResourceProviderData = new ObjectInitializationData[resourceProviderDataOffsets.Length];
            for (int i = 0; i < ResourceProviderData.Length; i++)
            {
                ResourceProviderData[i] = new ObjectInitializationData();
                ResourceProviderData[i].Read(reader, resourceProviderDataOffsets[i]);
            }

            ReadResources(reader, header);
        }

        private void ReadResources(ContentCatalogDataJson data)
        {
            List<Bucket> buckets;

            MemoryStream bucketStream = new MemoryStream(Convert.FromBase64String(data.m_BucketDataString));
            using (BinaryReader bucketReader = new BinaryReader(bucketStream))
            {
                int bucketCount = bucketReader.ReadInt32();
                buckets = new List<Bucket>(bucketCount);

                for (int i = 0; i < bucketCount; i++)
                {
                    int offset = bucketReader.ReadInt32();

                    int entryCount = bucketReader.ReadInt32();
                    int[] entries = new int[entryCount];
                    for (int j = 0; j < entryCount; j++)
                    {
                        entries[j] = bucketReader.ReadInt32();
                    }

                    buckets.Add(new Bucket(offset, entries));
                }
            }

            List<object> keys;

            MemoryStream keyDataStream = new MemoryStream(Convert.FromBase64String(data.m_KeyDataString));
            using (BinaryReader keyReader = new BinaryReader(keyDataStream))
            {
                int keyCount = keyReader.ReadInt32();
                keys = new List<object>(keyCount);

                for (int i = 0; i < keyCount; i++)
                {
                    keyDataStream.Position = buckets[i].offset;
                    keys.Add(SerializedObjectDecoder.DecodeV1(keyReader));
                }
            }

            List<ResourceLocation> locations;

            MemoryStream entryDataStream = new MemoryStream(Convert.FromBase64String(data.m_EntryDataString));
            MemoryStream extraDataStream = new MemoryStream(Convert.FromBase64String(data.m_ExtraDataString));
            using (BinaryReader entryReader = new BinaryReader(entryDataStream))
            using (BinaryReader extraReader = new BinaryReader(extraDataStream))
            {
                int entryCount = entryReader.ReadInt32();
                locations = new List<ResourceLocation>(entryCount);

                for (int i = 0; i < entryCount; i++)
                {
                    int internalIdIndex = entryReader.ReadInt32();
                    int providerIndex = entryReader.ReadInt32();
                    int dependencyKeyIndex = entryReader.ReadInt32();
                    int depHash = entryReader.ReadInt32();
                    int dataIndex = entryReader.ReadInt32();
                    int primaryKeyIndex = entryReader.ReadInt32();
                    int resourceTypeIndex = entryReader.ReadInt32();

                    string internalId = InternalIds[internalIdIndex];
                    int splitIndex = internalId.IndexOf('#');
                    if (splitIndex != -1)
                    {
                        int prefixIndex = int.Parse(internalId[..splitIndex]);
                        internalId = string.Concat(InternalIdPrefixes[prefixIndex], internalId.AsSpan(splitIndex + 1));
                    }

                    string providerId = ProviderIds[providerIndex];

                    object dependencyKey = null;
                    if (dependencyKeyIndex >= 0)
                    {
                        dependencyKey = keys[dependencyKeyIndex];
                    }

                    object objData = null;
                    if (dataIndex >= 0)
                    {
                        extraDataStream.Position = dataIndex;
                        objData = SerializedObjectDecoder.DecodeV1(extraReader);
                    }

                    object primaryKey;
                    if (Keys == null)
                    {
                        primaryKey = keys[primaryKeyIndex];
                    }
                    else
                    {
                        // unity moment
                        primaryKey = Keys[primaryKeyIndex];
                    }

                    SerializedType resourceType = ResourceTypes[resourceTypeIndex];

                    var loc = new ResourceLocation();
                    loc.Read(internalId, providerId, dependencyKey, objData, depHash, primaryKey, resourceType);
                    locations.Add(loc);
                }
            }

            Resources = new Dictionary<object, List<ResourceLocation>>(buckets.Count);
            for (int i = 0; i < buckets.Count; i++)
            {
                int[] bucketEntries = buckets[i].entries;
                List<ResourceLocation> locs = new List<ResourceLocation>(bucketEntries.Length);
                for (int j = 0; j < bucketEntries.Length; j++)
                {
                    locs.Add(locations[bucketEntries[j]]);
                }
                Resources[keys[i]] = locs;
            }
        }

        private void ReadResources(CatalogBinaryReader reader, ContentCatalogDataBinaryHeader header)
        {
            uint[] keyLocationOffsets = reader.ReadOffsetArray(header.KeysOffset);
            Resources = new Dictionary<object, List<ResourceLocation>>(keyLocationOffsets.Length / 2);
            for (int i = 0; i < keyLocationOffsets.Length; i += 2)
            {
                uint keyOffset = keyLocationOffsets[i];
                uint locationListOffset = keyLocationOffsets[i + 1];
                object key = SerializedObjectDecoder.DecodeV2(reader, keyOffset);

                uint[] locationOffsets = reader.ReadOffsetArray(locationListOffset);
                List<ResourceLocation> locations = new List<ResourceLocation>(locationOffsets.Length);
                for (int j = 0; j < locationOffsets.Length; j++)
                {
                    ResourceLocation location = new ResourceLocation();
                    location.Read(reader, locationOffsets[j]);
                    locations.Add(location);
                }

                Resources[key] = locations;
            }
        }

        internal void Write(ContentCatalogDataJson data)
        {
            data.m_LocatorId = LocatorId;
            data.m_BuildResultHash = BuildResultHash;

            data.m_InstanceProviderData = new ObjectInitializationDataJson();
            InstanceProviderData.Write(data.m_InstanceProviderData);

            data.m_SceneProviderData = new ObjectInitializationDataJson();
            SceneProviderData.Write(data.m_SceneProviderData);

            data.m_ResourceProviderData = new ObjectInitializationDataJson[ResourceProviderData.Length];
            for (int i = 0; i < data.m_ResourceProviderData.Length; i++)
            {
                data.m_ResourceProviderData[i] = new ObjectInitializationDataJson();
                ResourceProviderData[i].Write(data.m_ResourceProviderData[i]);
            }

            WriteResources(data);

            data.m_ProviderIds = new string[ProviderIds.Length];
            for (int i = 0; i < data.m_ProviderIds.Length; i++)
            {
                data.m_ProviderIds[i] = ProviderIds[i];
            }

            Dictionary<string, int> newPrefixesToIndex = MakeDictionaryList(InternalIdPrefixes.ToList());
            data.m_InternalIds = new string[InternalIds.Length];
            for (int i = 0; i < data.m_InternalIds.Length; i++)
            {
                int splitIndex = InternalIds[i].LastIndexOf('/');
                if (splitIndex != -1)
                {
                    int prefixIndex = newPrefixesToIndex[InternalIds[i][..splitIndex]];
                    data.m_InternalIds[i] = $"{prefixIndex}#{InternalIds[i][splitIndex..]}";
                }
                else
                {
                    data.m_InternalIds[i] = InternalIds[i];
                }
            }

            if (Keys != null)
            {
                data.m_Keys = new string[Keys.Length];
                for (int i = 0; i < data.m_Keys.Length; i++)
                {
                    data.m_Keys[i] = Keys[i];
                }
            }
            else
            {
                data.m_Keys = null;
            }

            data.m_resourceTypes = new SerializedTypeJson[ResourceTypes.Length];
            for (int i = 0; i < data.m_resourceTypes.Length; i++)
            {
                data.m_resourceTypes[i] = new SerializedTypeJson();
                ResourceTypes[i].Write(data.m_resourceTypes[i]);
            }

            if (InternalIdPrefixes != null)
            {
                data.m_InternalIdPrefixes = new string[InternalIdPrefixes.Length];
                for (int i = 0; i < data.m_InternalIdPrefixes.Length; i++)
                {
                    data.m_InternalIdPrefixes[i] = InternalIdPrefixes[i];
                }
            }
            else
            {
                data.m_InternalIdPrefixes = null;
            }
        }

        internal void Write(CatalogBinaryWriter writer, SerializedTypeAsmContainer staCont)
        {
            if (Version == 1)
            {
                throw new NotSupportedException("Only version 2 catalogs are supported for writing so far.");
            }

            writer.Version = Version;

            ContentCatalogDataBinaryHeader header = new ContentCatalogDataBinaryHeader();
            header.Write(writer); // empty header

            header.Magic = 0x0de38942;
            header.Version = 2;
            header.KeysOffset = (uint)writer.BaseStream.Position + 4;
            writer.Reserve(4 + Resources.Count * 4 * 2); // empty key list + length

            header.IdOffset = writer.WriteEncodedString(LocatorId);
            header.InstanceProviderOffset = InstanceProviderData.Write(writer);
            header.SceneProviderOffset = SceneProviderData.Write(writer);

            uint[] initObjectsOffsets = new uint[ResourceProviderData.Length];
            for (int i = 0; i < ResourceProviderData.Length; i++)
            {
                initObjectsOffsets[i] = ResourceProviderData[i].Write(writer);
            }

            header.InitObjectsArrayOffset = writer.WriteOffsetArray(initObjectsOffsets);
            header.BuildResultHashOffset = writer.WriteEncodedString(BuildResultHash);

            WriteResources(writer, header, staCont);

            writer.BaseStream.Position = 0;
            header.Write(writer);
        }

        private void WriteResources(CatalogBinaryWriter writer, ContentCatalogDataBinaryHeader header, SerializedTypeAsmContainer staCont)
        {
            List<uint[]> tmpLocationOffsetArray = new List<uint[]>();
            uint[] keyLocationOffsets = new uint[Resources.Count * 2];
            foreach (var kvp in Resources)
            {
                // resource locations are written first
                uint[] locationOffsets = new uint[kvp.Value.Count];
                for (int j = 0; j < kvp.Value.Count; j++)
                {
                    ResourceLocation location = kvp.Value[j];
                    locationOffsets[j] = location.Write(writer, staCont);
                }

                tmpLocationOffsetArray.Add(locationOffsets);
            }

            int i = 0;
            int i2 = 0;
            foreach (var kvp in Resources)
            {
                uint keyOffset = SerializedObjectDecoder.EncodeV2(writer, staCont, kvp.Key);
                keyLocationOffsets[i++] = keyOffset;

                uint locationListOffset = writer.WriteOffsetArray(tmpLocationOffsetArray[i2++]);
                keyLocationOffsets[i++] = locationListOffset;
            }

            // don't write with cache since we already reserved space for it
            writer.BaseStream.Position = header.KeysOffset - 4;
            header.KeysOffset = writer.WriteOffsetArray(keyLocationOffsets, false);
        }

        private void WriteResources(ContentCatalogDataJson data)
        {
            HashSet<string> newInternalIdHs = new HashSet<string>();
            HashSet<string> newProviderIdHs = new HashSet<string>();
            HashSet<SerializedType> newResourceTypeHs = new HashSet<SerializedType>();
            HashSet<string> newInternalIdPrefixes = new HashSet<string>();

            HashSet<ResourceLocation> newLocationHs = new HashSet<ResourceLocation>();

            List<object> newKeys = Resources.Keys.ToList();

            foreach (var value in Resources.Values)
            {
                foreach (var location in value)
                {
                    newLocationHs.Add(location);

                    if (location.InternalId == null)
                        throw new Exception("Location's internal ID cannot be null");

                    if (location.ProviderId == null)
                        throw new Exception("Location's provider ID cannot be null");

                    int splitIndex = location.InternalId.LastIndexOf('/');
                    if (splitIndex != -1)
                    {
                        newInternalIdPrefixes.Add(location.InternalId[..splitIndex]);
                    }

                    newInternalIdHs.Add(location.InternalId);
                    newProviderIdHs.Add(location.ProviderId);

                    if (location.Type != null)
                    {
                        newResourceTypeHs.Add(location.Type);
                    }
                }
            }

            List<string> newInternalIds = newInternalIdHs.ToList();
            List<string> newProviderIds = newProviderIdHs.ToList();
            List<SerializedType> newResourceTypes = newResourceTypeHs.ToList();
            List<ResourceLocation> newLocations = newLocationHs.ToList();

            Dictionary<object, int> newKeyToIndex = MakeDictionaryList(newKeys);
            Dictionary<string, int> newInternalIdsToIndex = MakeDictionaryList(newInternalIds);
            Dictionary<string, int> newProviderIdsToIndex = MakeDictionaryList(newProviderIds);
            Dictionary<SerializedType, int> newResourceTypesToIndex = MakeDictionaryList(newResourceTypes);
            Dictionary<ResourceLocation, int> newLocationsToIndex = MakeDictionaryList(newLocations);

            MemoryStream entryDataStream = new MemoryStream();
            MemoryStream extraDataStream = new MemoryStream();
            using (BinaryWriter entryWriter = new BinaryWriter(entryDataStream))
            using (BinaryWriter extraWriter = new BinaryWriter(extraDataStream))
            {
                entryWriter.Write(newLocationHs.Count);

                foreach (var location in newLocationHs)
                {
                    int internalIdIndex = newInternalIdsToIndex[location.InternalId];
                    int providerIndex = newProviderIdsToIndex[location.ProviderId];
                    int dependencyKeyIndex = (location.DependencyKey == null) ? -1 : newKeyToIndex[location.DependencyKey];
                    int depHash = location.DependencyHashCode; // todo calculate this
                    int dataIndex = -1;
                    if (location.Data != null)
                    {
                        dataIndex = (int)extraDataStream.Position;
                        SerializedObjectDecoder.EncodeV1(extraWriter, location.Data);
                    }
                    int primaryKeyIndex = newKeyToIndex[location.PrimaryKey];
                    int resourceTypeIndex = newResourceTypesToIndex[location.Type];

                    entryWriter.Write(internalIdIndex);
                    entryWriter.Write(providerIndex);
                    entryWriter.Write(dependencyKeyIndex);
                    entryWriter.Write(depHash);
                    entryWriter.Write(dataIndex);
                    entryWriter.Write(primaryKeyIndex);
                    entryWriter.Write(resourceTypeIndex);
                }
            }

            MemoryStream keyDataStream = new MemoryStream();
            MemoryStream bucketStream = new MemoryStream();
            using (BinaryWriter keyWriter = new BinaryWriter(keyDataStream))
            using (BinaryWriter bucketWriter = new BinaryWriter(bucketStream))
            {
                keyWriter.Write(newKeys.Count); // same as Resources.Count
                bucketWriter.Write(newKeys.Count);

                foreach (var resourceKvp in Resources)
                {
                    object resourceKey = resourceKvp.Key;
                    List<ResourceLocation> resourceValue = resourceKvp.Value;

                    Bucket bucket = new Bucket
                    {
                        offset = (int)keyDataStream.Position,
                        entries = new int[resourceValue.Count]
                    };

                    // write key
                    SerializedObjectDecoder.EncodeV1(keyWriter, resourceKey);

                    for (int i = 0; i < resourceValue.Count; i++)
                    {
                        bucket.entries[i] = newLocationsToIndex[resourceValue[i]];
                    }

                    // write bucket
                    bucketWriter.Write(bucket.offset);
                    bucketWriter.Write(bucket.entries.Length);
                    for (int i = 0; i < bucket.entries.Length; i++)
                    {
                        bucketWriter.Write(bucket.entries[i]);
                    }
                }
            }

            ProviderIds = newProviderIds.ToArray();
            InternalIds = newInternalIds.ToArray();
            InternalIdPrefixes = newInternalIdPrefixes.ToArray();
            ResourceTypes = newResourceTypes.ToArray();

            data.m_BucketDataString = Convert.ToBase64String(bucketStream.ToArray());
            data.m_KeyDataString = Convert.ToBase64String(keyDataStream.ToArray());
            data.m_EntryDataString = Convert.ToBase64String(entryDataStream.ToArray());
            data.m_ExtraDataString = Convert.ToBase64String(extraDataStream.ToArray());
        }

        private static Dictionary<T, int> MakeDictionaryList<T>(List<T> list)
        {
            return list
                .Select((item, index) => new { Item = item, Index = index })
                .ToDictionary(x => x.Item, x => x.Index);
        }

        private struct Bucket
        {
            public int offset;
            public int[] entries;

            public Bucket(int offset, int[] entries)
            {
                this.offset = offset;
                this.entries = entries;
            }
        }
    }
}
