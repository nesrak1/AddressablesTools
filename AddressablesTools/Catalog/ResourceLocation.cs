using AddressablesTools.Reader;
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
                reader.BaseStream.Position = dependencyOffsets[i];

                ResourceLocation dependencyLocation = new ResourceLocation();
                dependencyLocation.Read(reader, dependencyOffsets[i]);
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
    }
}
