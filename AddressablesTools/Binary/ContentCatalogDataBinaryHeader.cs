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
            if (Version is not (1 or 2))
            {
                throw new NotSupportedException("Only versions 1 and 2 are supported");
            }
            reader.Version = Version;

            KeysOffset = reader.ReadUInt32();
            IdOffset = reader.ReadUInt32();
            InstanceProviderOffset = reader.ReadUInt32();
            SceneProviderOffset = reader.ReadUInt32();
            InitObjectsArrayOffset = reader.ReadUInt32();

            // version 1 has at least two sub versions:
            // 1.21.18 does not have this member, so we ignore it
            if (Version == 1 && KeysOffset == 0x20)
                BuildResultHashOffset = uint.MaxValue;
            else
                BuildResultHashOffset = reader.ReadUInt32();
        }

        internal void Write(CatalogBinaryWriter writer)
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(KeysOffset);
            writer.Write(IdOffset);
            writer.Write(InstanceProviderOffset);
            writer.Write(SceneProviderOffset);
            writer.Write(InitObjectsArrayOffset);
            if (BuildResultHashOffset != uint.MaxValue)
                writer.Write(BuildResultHashOffset);
        }
    }
}
