using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;

namespace AddressablesTools.Binary
{
    internal class CatalogBinaryWriter : BinaryWriter
    {
        public int Version { get; set; } = 1;

        private readonly Dictionary<UInt128, uint> _dataCache = [];
        private readonly Dictionary<string, uint> _quickStrCache = [];

        public CatalogBinaryWriter(Stream input) : base(input) { }

        public void Reserve(int space)
        {
            const int BLOCK_SIZE = 512;

            Span<byte> zeros = stackalloc byte[BLOCK_SIZE];
            while (space > BLOCK_SIZE)
            {
                BaseStream.Write(zeros);
                space -= BLOCK_SIZE;
            }
            if (space > 0)
            {
                BaseStream.Write(zeros[..space]);
            }
        }

        public uint WriteWithCache(ReadOnlySpan<byte> data)
        {
            UInt128 key = XxHash128.HashToUInt128(data);
            if (_dataCache.TryGetValue(key, out uint value))
            {
                return value;
            }

            uint pos = (uint)BaseStream.Position;
            Write(data);
            _dataCache[key] = pos;
            return pos;
        }

        private uint WriteBasicString(string data, bool unicode)
        {
            // an extra cache so we don't run Encoding.XXX.GetBytes unnecessarily
            if (_quickStrCache.TryGetValue(data, out uint cachedOff))
            {
                return cachedOff;
            }

            byte[] lengthlessBytes;
            if (unicode)
            {
                lengthlessBytes = Encoding.Unicode.GetBytes(data);
            }
            else
            {
                lengthlessBytes = Encoding.ASCII.GetBytes(data);
            }

            byte[] bytes = new byte[lengthlessBytes.Length + 4];
            Span<byte> bytesSpan = bytes.AsSpan();
            BinaryPrimitives.WriteInt32LittleEndian(bytesSpan, lengthlessBytes.Length);
            lengthlessBytes.CopyTo(bytesSpan[4..]);

            uint pos = WriteWithCache(bytes) + 4;
            _quickStrCache[data] = pos;
            return pos;
        }

        private uint WriteDynamicString(string data, bool unicode, char sep)
        {
            const int MIN_SPLIT_SIZE = 8;

            string[] dataSplits = data.Split(sep);
            List<string> joinedSplits = new List<string>(dataSplits.Length);

            // would regular string concat be faster?
            int currentSplitLength = -1; // remove one for first seperator char
            int totalSplitLength = 0;
            List<string> currentSplit = new List<string>();
            for (int i = dataSplits.Length - 1; i >= 0; i--)
            {
                string dataSplit = dataSplits[i];
                currentSplit.Add(dataSplit);

                // add one for seperator
                if (unicode)
                    currentSplitLength += Encoding.Unicode.GetByteCount(dataSplit) + 1;
                else
                    currentSplitLength += Encoding.ASCII.GetByteCount(dataSplit) + 1;

                if (currentSplitLength >= MIN_SPLIT_SIZE || i == 0)
                {
                    if (currentSplit.Count == 1)
                    {
                        joinedSplits.Add(currentSplit[0]);
                    }
                    else
                    {
                        joinedSplits.Add(string.Join(sep, currentSplit.AsEnumerable().Reverse()));
                    }

                    // only sum split contents, no seperators
                    totalSplitLength += Math.Max(currentSplitLength, 0) - (currentSplit.Count - 1);

                    currentSplitLength = -1;
                    currentSplit.Clear();
                }
            }

            // to keep with how unity does it, we'll check their way (even though this is slower)
            if (dataSplits.Length < 2 || (dataSplits.Length == 2 && totalSplitLength < MIN_SPLIT_SIZE))
            {
                return WriteBasicString(data, unicode);
            }

            List<uint> splitOffsets = new List<uint>(joinedSplits.Count);
            foreach (string split in joinedSplits)
            {
                uint offset = WriteBasicString(split, unicode);
                splitOffsets.Add(offset);
            }

            uint lastLlOffset = uint.MaxValue;
            Span<byte> pieceBytes = stackalloc byte[8];
            for (int i = splitOffsets.Count - 1; i >= 0; i--)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(pieceBytes, splitOffsets[i]);
                BinaryPrimitives.WriteUInt32LittleEndian(pieceBytes[4..], lastLlOffset);
                uint thisLlOffset = WriteWithCache(pieceBytes);

                lastLlOffset = thisLlOffset;
            }

            return lastLlOffset;
        }

        private static bool IsStringAscii(string str)
        {
            int strLen = str.Length;
            for (int i = 0; i < strLen; i++)
            {
                if (str[i] > 255)
                    return false;
            }

            return true;
        }

        public uint WriteEncodedString(string data, char dynstrSep = '\0')
        {
            if (data == null)
            {
                return uint.MaxValue;
            }

            bool unicode = data.Length > 0 && !IsStringAscii(data);
            bool dynamicString = dynstrSep != '\0' && data.Contains(dynstrSep);

            uint result;
            if (dynamicString)
            {
                result = WriteDynamicString(data, unicode, dynstrSep);
                result |= 0x40000000;
            }
            else
            {
                result = WriteBasicString(data, unicode);
            }

            if (unicode)
            {
                result |= 0x80000000;
            }

            return result;
        }

        public uint WriteOffsetArray(uint[] data, bool withCache = true)
        {
            byte[] bytes = new byte[data.Length * 4 + 4];
            Span<byte> bytesSpan = bytes.AsSpan();

            BinaryPrimitives.WriteInt32LittleEndian(bytesSpan, data.Length * 4);
            int dataIdx = 0;
            for (int i = 4; i < bytes.Length; i += 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytesSpan[i..], data[dataIdx++]);
            }

            if (withCache)
            {
                uint pos = WriteWithCache(bytes);
                return pos + 4;
            }
            else
            {
                uint pos = (uint)BaseStream.Position;
                Write(bytes);
                return pos + 4;
            }
        }
    }
}