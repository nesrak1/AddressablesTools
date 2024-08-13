using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AddressablesTools.Reader
{
    internal class CatalogBinaryReader : BinaryReader
    {
        public CatalogBinaryReader(Stream input) : base(input) { }

        private string ReadBasicString(long offset, bool unicode)
        {
            BaseStream.Position = offset - 4;
            int length = ReadInt32();
            byte[] data = ReadBytes(length);
            if (unicode)
            {
                return Encoding.UTF8.GetString(data);
            }
            else
            {
                return Encoding.ASCII.GetString(data);
            }
        }

        private string ReadDynamicString(long offset, bool unicode, char sep)
        {
            BaseStream.Position = offset;
            List<string> partStrs = new List<string>();
            while (true)
            {
                long partStringOffset = ReadUInt32();
                long nextPartOffset = ReadUInt32();
                partStrs.Add(ReadBasicString(partStringOffset, unicode));
                if (nextPartOffset == uint.MaxValue)
                {
                    break;
                }

                BaseStream.Position = nextPartOffset;
            }

            return string.Join(sep, partStrs.AsEnumerable().Reverse());
        }

        public string ReadEncodedString(uint encodedOffset, char dynstrSep = '\0')
        {
            if (encodedOffset == uint.MaxValue)
            {
                return null;
            }

            bool unicode = (encodedOffset & 0x80000000) != 0;
            bool dynamicString = ((encodedOffset & 0x40000000) != 0) && dynstrSep != '\0';
            long offset = encodedOffset & 0x3fffffff;

            string result;
            if (!dynamicString)
            {
                result = ReadBasicString(offset, unicode);
            }
            else
            {
                result = ReadDynamicString(offset, unicode, dynstrSep);
            }

            return result;
        }

        public uint[] ReadOffsetArray(uint encodedOffset)
        {
            if (encodedOffset == uint.MaxValue)
            {
                return Array.Empty<uint>();
            }

            BaseStream.Position = encodedOffset - 4;
            int byteSize = ReadInt32();
            if (byteSize % 4 != 0)
            {
                throw new InvalidDataException("Array size must be a multiple of 4");
            }

            int elemCount = byteSize / 4;
            uint[] result = new uint[elemCount];
            for (int i = 0; i < elemCount; i++)
            {
                result[i] = ReadUInt32();
            }

            return result;
        }
    }
}
