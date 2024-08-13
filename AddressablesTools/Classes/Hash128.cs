using System;
using System.Buffers.Binary;

namespace AddressablesTools.Classes
{
    public class Hash128
    {
        public string Value { get; set; }

        public Hash128(string value)
        {
            Value = value;
        }

        public Hash128(uint v0, uint v1, uint v2, uint v3)
        {
            byte[] data = new byte[16];
            Span<byte> dataSpan = data.AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan[0..], v0);
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan[4..], v1);
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan[8..], v2);
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan[12..], v3);
            Value = Convert.ToHexString(data).ToLowerInvariant();
        }
    }
}
