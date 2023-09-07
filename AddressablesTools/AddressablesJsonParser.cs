using AddressablesTools.Catalog;
using AddressablesTools.JSON;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AddressablesTools
{
    public static class AddressablesJsonParser
    {
        internal static ContentCatalogDataJson CCDJsonFromString(string data)
        {
            return JsonSerializer.Deserialize<ContentCatalogDataJson>(data);
        }

        public static ContentCatalogData FromString(string data)
        {
            ContentCatalogDataJson ccdJson = CCDJsonFromString(data);

            ContentCatalogData catalogData = new ContentCatalogData();
            catalogData.Read(ccdJson);

            return catalogData;
        }

        internal static ContentCatalogData FromBundle(AssetsManager manager, BundleFileInstance bundleInst)
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
            string data = reader.ReadCountStringInt32();

            manager.UnloadAll();

            return FromString(data);
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

        public static string ToJson(ContentCatalogData ccd)
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
            string json = ToJson(ccd);

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
