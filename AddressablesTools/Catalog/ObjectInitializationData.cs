using AddressablesTools.Binary;
using AddressablesTools.JSON;
using System;
using System.Buffers.Binary;

namespace AddressablesTools.Catalog
{
    public class ObjectInitializationData
    {
        public string Id { get; set; }
        public SerializedType ObjectType { get; set; }
        public string Data { get; set; }

        internal void Read(ObjectInitializationDataJson obj)
        {
            Id = obj.m_Id;
            ObjectType = new SerializedType();
            ObjectType.Read(obj.m_ObjectType);
            Data = obj.m_Data;
        }

        internal void Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            uint idOffset = reader.ReadUInt32();
            uint objectTypeOffset = reader.ReadUInt32();
            uint dataOffset = reader.ReadUInt32();

            Id = reader.ReadEncodedString(idOffset);
            ObjectType = new SerializedType();
            ObjectType.Read(reader, objectTypeOffset);
            Data = reader.ReadEncodedString(dataOffset);
        }

        internal void Write(ObjectInitializationDataJson obj)
        {
            obj.m_Id = Id;
            obj.m_ObjectType = new SerializedTypeJson();
            ObjectType.Write(obj.m_ObjectType);
            obj.m_Data = Data;
        }

        internal uint Write(CatalogBinaryWriter writer)
        {
            Span<byte> bytes = stackalloc byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, writer.WriteEncodedString(Id));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], ObjectType.Write(writer));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], writer.WriteEncodedString(Data));
            return writer.WriteWithCache(bytes);
        }
    }
}
