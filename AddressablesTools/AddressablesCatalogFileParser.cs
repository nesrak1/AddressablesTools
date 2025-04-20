using AddressablesTools.Binary;
using AddressablesTools.Catalog;
using AddressablesTools.JSON;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AddressablesTools
{
    public static class AddressablesCatalogFileParser
    {
        internal static ContentCatalogDataJson CCDJsonFromString(string data)
        {
            return JsonSerializer.Deserialize<ContentCatalogDataJson>(data);
        }

        public static ContentCatalogData FromBinaryData(byte[] data)
        {
            using MemoryStream ms = new MemoryStream(data);
            using CatalogBinaryReader reader = new CatalogBinaryReader(ms);

            ContentCatalogData catalogData = new ContentCatalogData();
            catalogData.Read(reader);

            return catalogData;
        }

        public static ContentCatalogData FromJsonString(string data)
        {
            ContentCatalogDataJson ccdJson = CCDJsonFromString(data);

            ContentCatalogData catalogData = new ContentCatalogData();
            catalogData.Read(ccdJson);

            return catalogData;
        }

        public static CatalogFileType GetCatalogFileType(Stream stream)
        {
            byte[] data = new byte[4];
            int readBytes = stream.Read(data, 0, 4);
            if (readBytes != 4)
            {
                return CatalogFileType.None;
            }

            int possibleMagic;
            possibleMagic = BinaryPrimitives.ReadInt32LittleEndian(data);
            if (possibleMagic == 0x0de38942)
            {
                return CatalogFileType.Binary;
            }
            else if (possibleMagic == 0x4289e30d)
            {
                return CatalogFileType.Binary;
            }
            else
            {
                if (data[0] == '{')
                {
                    return CatalogFileType.Json;
                }

                // double check there isn't whitespace before the {
                stream.Position = 0;
                while (true)
                {
                    int v = stream.ReadByte();
                    if (v == -1)
                    {
                        return CatalogFileType.None;
                    }
                    else if (v == '\t' || v == ' ')
                    {
                        continue;
                    }
                    else if (v == '{')
                    {
                        return CatalogFileType.Json;
                    }
                }
            }
        }

        internal static byte[] GetBundleTextAssetData(AssetsManager manager, BundleFileInstance bundleInst)
        {
            // there should only be one file in this bundle, so 0 is fine
            AssetsFileInstance assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0);
            AssetsFile assetsFile = assetsInst.file;

            // there should also be only one text asset
            AssetFileInfo catalogAssetInfo = assetsFile.GetAssetsOfType(AssetClassID.TextAsset)[0];

            // faster to manually read
            AssetsFileReader reader = assetsFile.Reader;
            reader.Position = catalogAssetInfo.GetAbsoluteByteOffset(assetsFile);

            reader.ReadCountStringInt32(); // ignore name
            reader.Align();
            int dataSize = reader.ReadInt32();
            byte[] data = reader.ReadBytes(dataSize);

            manager.UnloadAll();

            return data;
        }

        internal static ContentCatalogData FromBundle(AssetsManager manager, BundleFileInstance bundleInst)
        {
            byte[] data = GetBundleTextAssetData(manager, bundleInst);
            if (data.Length < 4)
            {
                throw new InvalidDataException("Catalog data too small");
            }

            int possibleMagic;
            possibleMagic = BinaryPrimitives.ReadInt32LittleEndian(data);
            if (possibleMagic == 0x0de38942)
            {
                return FromBinaryData(data);
            }
            else if (possibleMagic == 0x4289e30d)
            {
                // different hash code on big endian maybe?
                throw new NotSupportedException("Big endian catalogs are not supported");
            }
            else
            {
                return FromJsonString(Encoding.UTF8.GetString(data));
            }
        }

        public static ContentCatalogData FromBundle(Stream stream)
        {
            AssetsManager manager = new AssetsManager();
            // name doesn't matter since we don't have dependencies
            BundleFileInstance bundleInst = manager.LoadBundleFile(stream, "catalog.bundle");
            return FromBundle(manager, bundleInst);
        }

        public static ContentCatalogData FromBundle(string path)
        {
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(path);
            return FromBundle(manager, bundleInst);
        }

        public static byte[] ToBinaryData(ContentCatalogData ccd)
        {
            using MemoryStream ms = new MemoryStream();
            using CatalogBinaryWriter writer = new CatalogBinaryWriter(ms);

            ccd.Write(writer, SerializedTypeAsmContainer.ForNet40());

            return ms.ToArray();
        }

        public static string ToJsonString(ContentCatalogData ccd)
        {
            ContentCatalogDataJson ccdJson = new ContentCatalogDataJson();

            ccd.Write(ccdJson);

            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(ccdJson, options);
        }

        internal static void ToBundle(ContentCatalogData ccd, AssetsManager manager, BundleFileInstance bundleInst, Stream stream)
        {
            string json = ToJsonString(ccd);

            // there should only be one file in this bundle, so 0 is fine
            AssetsFileInstance assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0);
            AssetsFile assetsFile = assetsInst.file;

            // there should also be only one text asset
            AssetFileInfo catalogAssetInfo = assetsFile.GetAssetsOfType(AssetClassID.TextAsset)[0];

            MemoryStream newTextAssetMem = new MemoryStream();
            AssetsFileWriter newTextAssetWriter = new AssetsFileWriter(newTextAssetMem);
            newTextAssetWriter.WriteCountStringInt32("catalog"); // doesn't really matter
            newTextAssetWriter.Align();
            newTextAssetWriter.WriteCountStringInt32(json);
            newTextAssetWriter.Align();

            catalogAssetInfo.SetNewData(newTextAssetMem.ToArray());

            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);

            AssetsFileWriter bundleWriter = new AssetsFileWriter(stream);
            bundleInst.file.Write(bundleWriter);

            manager.UnloadAll();
        }

        public static void ToBundle(ContentCatalogData ccd, Stream inStream, Stream outStream)
        {
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(inStream, "catalog.bundle");
            ToBundle(ccd, manager, bundleInst, outStream);
        }

        public static void ToBundle(ContentCatalogData ccd, string inPath, string outPath)
        {
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(inPath);
            using FileStream fs = File.OpenWrite(outPath);
            ToBundle(ccd, manager, bundleInst, fs);
        }
    }
}
