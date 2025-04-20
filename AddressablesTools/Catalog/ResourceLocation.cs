using AddressablesTools.Binary;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AddressablesTools.Catalog
{
    public class ResourceLocation
    {
        public string InternalId { get; set; }
        public string ProviderId { get; set; }
        public object DependencyKey { get; set; }
        public List<ResourceLocation> Dependencies { get; set; }
        public object Data { get; set; }
        public int HashCode { get; set; }
        public int DependencyHashCode { get; set; }
        public string PrimaryKey { get; set; }
        public SerializedType Type { get; set; }

        internal void Read(
            string internalId, string providerId, object dependencyKey, object data,
            int depHashCode, object primaryKey, SerializedType resourceType
        )
        {
            InternalId = internalId;
            ProviderId = providerId;
            DependencyKey = dependencyKey;
            Dependencies = null;
            Data = data;
            HashCode = internalId.GetHashCode() * 31 + providerId.GetHashCode();
            DependencyHashCode = depHashCode;
            PrimaryKey = primaryKey.ToString();
            Type = resourceType;
        }

        internal void Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;
            uint primaryKeyOffset = reader.ReadUInt32();
            uint internalIdOffset = reader.ReadUInt32();
            uint providerIdOffset = reader.ReadUInt32();
            uint dependenciesOffset = reader.ReadUInt32();
            int dependencyHashCode = reader.ReadInt32();
            uint dataOffset = reader.ReadUInt32();
            uint typeOffset = reader.ReadUInt32();

            PrimaryKey = reader.ReadEncodedString(primaryKeyOffset, '/');
            InternalId = reader.ReadEncodedString(internalIdOffset, '/');
            ProviderId = reader.ReadEncodedString(providerIdOffset, '.');

            uint[] dependencyOffsets = reader.ReadOffsetArray(dependenciesOffset);
            List<ResourceLocation> dependencies = new List<ResourceLocation>(dependencyOffsets.Length);
            for (int i = 0; i < dependencyOffsets.Length; i++)
            {
                //reader.BaseStream.Position = dependencyOffsets[i];
                uint objectOffset = dependencyOffsets[i];
                var dependencyLocation = reader.ReadCustom(objectOffset, () =>
                {
                    var newDepLoc = new ResourceLocation();
                    newDepLoc.Read(reader, objectOffset);
                    return newDepLoc;
                });
                dependencies.Add(dependencyLocation);
            }

            DependencyKey = null;
            Dependencies = dependencies;

            // officially, dependenciesOffset is used here. lol. we can't do
            // that since writing the file would permenantly lose that value.
            DependencyHashCode = dependencyHashCode;
            Data = SerializedObjectDecoder.DecodeV2(reader, dataOffset);
            Type = new SerializedType();
            Type.Read(reader, typeOffset);
        }

        internal uint Write(CatalogBinaryWriter writer, SerializedTypeAsmContainer staCont)
        {
            uint dependenciesOffset;
            if (Dependencies.Count > 0)
            {
                uint[] dependenciesList = new uint[Dependencies.Count];
                for (int i = 0; i < Dependencies.Count; i++)
                {
                    dependenciesList[i] = Dependencies[i].Write(writer, staCont);
                }

                dependenciesOffset = writer.WriteOffsetArray(dependenciesList);
            }
            else
            {
                dependenciesOffset = uint.MaxValue;
            }

            uint primaryKeyOffset = writer.WriteEncodedString(PrimaryKey, '/');
            uint internalIdOffset = writer.WriteEncodedString(InternalId, '/');
            uint providerIdOffset = writer.WriteEncodedString(ProviderId, '.');

            int dependencyHashCode = DependencyHashCode;
            uint dataOffset = SerializedObjectDecoder.EncodeV2(writer, staCont, Data);
            uint typeOffset = Type.Write(writer);

            Span<byte> bytes = stackalloc byte[28];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, primaryKeyOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], internalIdOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], providerIdOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], dependenciesOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes[16..], dependencyHashCode);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[20..], dataOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[24..], typeOffset);
            return writer.WriteWithCache(bytes);
        }
    }
}
