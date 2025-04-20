using AddressablesTools.Binary;
using System;
using System.Buffers.Binary;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AddressablesTools.Classes
{
    public enum AssetLoadMode
    {
        RequestedAssetAndDependencies,
        AllPackedAssetsAndDependencies
    }

    public class AssetBundleRequestOptions
    {
        public string Hash { get; set; }
        public uint Crc { get; set; }
        public CommonInfo ComInfo { get; set; }
        public string BundleName { get; set; }
        public long BundleSize { get; set; }

        internal void Read(string jsonText)
        {
            JsonObject jsonObj = JsonSerializer.Deserialize<JsonObject>(jsonText);
            if (jsonObj == null)
            {
                return;
            }

            Hash = (string)jsonObj["m_Hash"];
            Crc = (uint)jsonObj["m_Crc"];
            BundleName = (string)jsonObj["m_BundleName"];
            BundleSize = (long)jsonObj["m_BundleSize"];

            // this is only for writing back
            int commonInfoVersion;
            if (jsonObj["m_ChunkedTransfer"] == null)
            {
                commonInfoVersion = 1;
            }
            else if (jsonObj["m_AssetLoadMode"] == null &&
                     jsonObj["m_UseCrcForCachedBundles"] == null &&
                     jsonObj["m_UseUWRForLocalBundles"] == null &&
                     jsonObj["m_ClearOtherCachedVersionsWhenLoaded"] == null)
            {
                commonInfoVersion = 2;
            }
            else
            {
                commonInfoVersion = 3;
            }

            ComInfo = new CommonInfo()
            {
                Version = commonInfoVersion,
                Timeout = (short)(int)jsonObj["m_Timeout"],
                ChunkedTransfer = (bool)(jsonObj["m_ChunkedTransfer"] ?? false),
                RedirectLimit = (byte)(int)jsonObj["m_RedirectLimit"],
                RetryCount = (byte)(int)jsonObj["m_RetryCount"],
                AssetLoadMode = (AssetLoadMode)(int)(jsonObj["m_AssetLoadMode"] ?? (int)AssetLoadMode.RequestedAssetAndDependencies),
                UseCrcForCachedBundle = (bool)(jsonObj["m_UseCrcForCachedBundles"] ?? false),
                UseUnityWebRequestForLocalBundles = (bool)(jsonObj["m_UseUWRForLocalBundles"] ?? false),
                ClearOtherCachedVersionsWhenLoaded = (bool)(jsonObj["m_ClearOtherCachedVersionsWhenLoaded"] ?? false),
            };
        }

        internal void Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            uint hashOffset = reader.ReadUInt32();
            uint bundleNameOffset = reader.ReadUInt32();
            uint crc = reader.ReadUInt32();
            uint bundleSize = reader.ReadUInt32();
            uint commonInfoOffset = reader.ReadUInt32();

            reader.BaseStream.Position = hashOffset;
            uint hashV0 = reader.ReadUInt32();
            uint hashV1 = reader.ReadUInt32();
            uint hashV2 = reader.ReadUInt32();
            uint hashV3 = reader.ReadUInt32();
            Hash = new Hash128(hashV0, hashV1, hashV2, hashV3).Value;

            BundleName = reader.ReadEncodedString(bundleNameOffset, '_');
            Crc = crc;
            BundleSize = bundleSize;

            // split in another class in case we need to do writing with duplicates later
            ComInfo = new CommonInfo()
            {
                Version = 3
            };
            ComInfo.Read(reader, commonInfoOffset);
        }

        internal string WriteJson()
        {
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            JsonObject jsonObj = new JsonObject();

            jsonObj["m_Hash"] = Hash;
            jsonObj["m_Crc"] = Crc;
            jsonObj["m_Timeout"] = ComInfo.Timeout;
            jsonObj["m_RedirectLimit"] = ComInfo.RedirectLimit;
            jsonObj["m_RetryCount"] = ComInfo.RetryCount;
            jsonObj["m_BundleName"] = BundleName;
            jsonObj["m_BundleSize"] = BundleSize;
            if (ComInfo.Version > 1)
            {
                jsonObj["m_ChunkedTransfer"] = ComInfo.ChunkedTransfer;
            }
            if (ComInfo.Version > 2)
            {
                jsonObj["m_AssetLoadMode"] = (int)ComInfo.AssetLoadMode;
                jsonObj["m_UseCrcForCachedBundles"] = ComInfo.UseCrcForCachedBundle; // not a typo
                jsonObj["m_UseUWRForLocalBundles"] = ComInfo.UseUnityWebRequestForLocalBundles;
                jsonObj["m_ClearOtherCachedVersionsWhenLoaded"] = ComInfo.ClearOtherCachedVersionsWhenLoaded;
            }

            return JsonSerializer.Serialize(jsonObj, options);
        }

        internal uint WriteBinary(CatalogBinaryWriter writer)
        {
            uint hashOffset = new Hash128(Hash).Write(writer);
            uint bundleNameOffset = writer.WriteEncodedString(BundleName, '_');
            uint crc = Crc;
            uint bundleSize = (uint)BundleSize;
            uint commonInfoOffset = ComInfo.Write(writer);

            Span<byte> bytes = stackalloc byte[20];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, hashOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], bundleNameOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], crc);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], bundleSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[16..], commonInfoOffset);
            return writer.WriteWithCache(bytes);
        }

        public class CommonInfo
        {
            public short Timeout { get; set; }
            public byte RedirectLimit { get; set; }
            public byte RetryCount { get; set; }
            public AssetLoadMode AssetLoadMode { get; set; }
            public bool ChunkedTransfer { get; set; }
            public bool UseCrcForCachedBundle { get; set; }
            public bool UseUnityWebRequestForLocalBundles { get; set; }
            public bool ClearOtherCachedVersionsWhenLoaded { get; set; }

            // this is not a real field, but this helps us know which fields to write back
            // version 1 (json) = don't write AssetLoadMode, UseCrcForCachedBundle, UseUnityWebRequestForLocalBundles,
            //                                ClearOtherCachedVersionsWhenLoaded, ChunkedTransfer
            // version 2 (json) = don't write AssetLoadMode, UseCrcForCachedBundle, UseUnityWebRequestForLocalBundles,
            //                                ClearOtherCachedVersionsWhenLoaded
            // version 3 (json + binary) = write all fields
            public int Version { get; init; }

            internal void Read(CatalogBinaryReader reader, uint offset)
            {
                reader.BaseStream.Position = offset;

                short timeout = reader.ReadInt16();
                byte redirectLimit = reader.ReadByte();
                byte retryCount = reader.ReadByte();
                int flags = reader.ReadInt32();

                Timeout = timeout;
                RedirectLimit = redirectLimit;
                RetryCount = retryCount;

                if ((flags & 1) != 0)
                {
                    AssetLoadMode = AssetLoadMode.AllPackedAssetsAndDependencies;
                }
                else
                {
                    AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;
                }

                ChunkedTransfer = (flags & 2) != 0;
                UseCrcForCachedBundle = (flags & 4) != 0;
                UseUnityWebRequestForLocalBundles = (flags & 8) != 0;
                ClearOtherCachedVersionsWhenLoaded = (flags & 16) != 0;
            }

            internal uint Write(CatalogBinaryWriter writer)
            {
                int flags = 0;
                flags |= ((int)AssetLoadMode) & 1;
                flags |= (ChunkedTransfer ? 1 : 0) << 1;
                flags |= (UseCrcForCachedBundle ? 1 : 0) << 2;
                flags |= (UseUnityWebRequestForLocalBundles ? 1 : 0) << 3;
                flags |= (ClearOtherCachedVersionsWhenLoaded ? 1 : 0) << 4;

                Span<byte> data = stackalloc byte[8];
                BinaryPrimitives.WriteInt16LittleEndian(data, Timeout);
                data[2] = RedirectLimit;
                data[3] = RetryCount;
                BinaryPrimitives.WriteInt32LittleEndian(data[4..], flags);
                return writer.WriteWithCache(data);
            }
        }
    }
}
