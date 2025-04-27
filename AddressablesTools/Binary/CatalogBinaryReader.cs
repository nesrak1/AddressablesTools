using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AddressablesTools.Binary
{
    internal class CatalogBinaryReader : BinaryReader
    {
        public int Version { get; set; } = 1;

        private readonly Dictionary<uint, object> _objCache = [];

        public CatalogBinaryReader(Stream input) : base(input) { }

        public T CacheAndReturn<T>(uint offset, T obj)
        {
            _objCache[offset] = obj;
            return obj;
        }

        public bool TryGetCachedObject<T>(uint offset, out T typedObj)
        {
            if (_objCache.TryGetValue(offset, out object obj))
            {
                typedObj = (T)obj;
                return true;
            }

            typedObj = default;
            return false;
        }

        private string ReadBasicString(long offset, bool unicode)
        {
            BaseStream.Position = offset - 4;
            int length = ReadInt32();
            byte[] data = ReadBytes(length);
            if (unicode)
            {
                return Encoding.Unicode.GetString(data);
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

                partStrs.Add(ReadEncodedString((uint)partStringOffset)); // which seperator?

                if (nextPartOffset == uint.MaxValue)
                {
                    break;
                }

                BaseStream.Position = nextPartOffset;
            }

            if (partStrs.Count == 1)
                return partStrs[0];

            if (Version > 1)
                return string.Join(sep, partStrs.AsEnumerable().Reverse());
            else
                return string.Join(sep, partStrs);
        }

        public string ReadEncodedString(uint encodedOffset, char dynstrSep = '\0')
        {
            if (encodedOffset == uint.MaxValue)
            {
                return null;
            }

            if (TryGetCachedObject(encodedOffset, out string cachedStr))
            {
                return cachedStr;
            }

            bool unicode = (encodedOffset & 0x80000000) != 0;
            bool dynamicString = (encodedOffset & 0x40000000) != 0 && dynstrSep != '\0';
            long offset = encodedOffset & 0x3fffffff;

            if (!dynamicString)
            {
                return CacheAndReturn((uint)offset, ReadBasicString(offset, unicode));
            }
            else
            {
                return CacheAndReturn((uint)offset, ReadDynamicString(offset, unicode, dynstrSep));
            }
        }

        public uint[] ReadOffsetArray(uint encodedOffset)
        {
            if (encodedOffset == uint.MaxValue)
            {
                return [];
            }

            if (TryGetCachedObject(encodedOffset, out uint[] cachedArr))
            {
                return cachedArr;
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

            return CacheAndReturn(encodedOffset, result);
        }

        public T ReadCustom<T>(uint offset, Func<T> fetchFunc)
        {
            if (!TryGetCachedObject(offset, out T v))
            {
                v = fetchFunc();
                _objCache[offset] = v;
            }

            return v;
        }
    }
}
