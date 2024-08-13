using AddressablesTools.Reader;
using System;

namespace AddressablesTools.Binary
{
    internal class ContentCatalogDataBinaryHeader
    {
        public int Magic { get; set; }
        public int Version { get; set; }
        public uint KeysOffset { get; set; }
        public uint IdOffset { get; set; }
        public uint InstanceProviderOffset { get; set; }
        public uint SceneProviderOffset { get; set; }
        public uint InitObjectsArrayOffset { get; set; }
        public uint BuildResultHashOffset { get; set; }

        internal void Read(CatalogBinaryReader reader)
        {
            Magic = reader.ReadInt32();
            Version = reader.ReadInt32();
            if (Version != 2)
            {
                throw new NotSupportedException("Only version 2 is supported");
            }

            KeysOffset = reader.ReadUInt32();
            IdOffset = reader.ReadUInt32();
            InstanceProviderOffset = reader.ReadUInt32();
            SceneProviderOffset = reader.ReadUInt32();
            InitObjectsArrayOffset = reader.ReadUInt32();
            BuildResultHashOffset = reader.ReadUInt32();
        }
    }
}
