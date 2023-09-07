using AddressablesTools;
using AddressablesTools.Catalog;
using AssetsTools.NET;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

static void SearchExample(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("need path to catalog.json");
        return;
    }

    bool fromBundle = IsUnityFS(args[1]);

    ContentCatalogData ccd;
    if (fromBundle)
        ccd = AddressablesJsonParser.FromBundle(args[1]);
    else
        ccd = AddressablesJsonParser.FromString(File.ReadAllText(args[1]));

    Console.Write("search key to find bundles of: ");
    string? search = Console.ReadLine();

    if (search == null)
    {
        return;
    }

    search = search.ToLower();

    foreach (object k in ccd.Resources.Keys)
    {
        if (k is string s && s.ToLower().Contains(search))
        {
            Console.Write(s);
            foreach (var rsrc in ccd.Resources[s])
            {
                Console.WriteLine($" ({rsrc.ProviderId})");
                if (rsrc.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                {
                    List<ResourceLocation> o = ccd.Resources[rsrc.Dependency];
                    Console.WriteLine($"  {o[0].InternalId}");
                    if (o.Count > 1)
                    {
                        for (int i = 1; i < o.Count; i++)
                        {
                            Console.WriteLine($"    {o[i].InternalId}");
                        }
                    }
                }
            }
        }
    }
}

static bool IsUnityFS(string path)
{
    const string unityFs = "UnityFS";
    using AssetsFileReader reader = new AssetsFileReader(path);
    if (reader.BaseStream.Length < unityFs.Length)
    {
        return false;
    }

    return reader.ReadStringLength(unityFs.Length) == unityFs;
}

static void PatchCrcExample(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("need path to catalog.json");
        return;
    }

    bool fromBundle = IsUnityFS(args[1]);

    ContentCatalogData ccd;
    if (fromBundle)
        ccd = AddressablesJsonParser.FromBundle(args[1]);
    else
        ccd = AddressablesJsonParser.FromString(File.ReadAllText(args[1]));

    Console.WriteLine("patching...");

    foreach (var resourceList in ccd.Resources.Values)
    {
        foreach (var rsrc in resourceList)
        {
            if (rsrc.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
            {
                var data = rsrc.Data;
                if (data != null && data is ClassJsonObject classJsonObject)
                {
                    JsonSerializerOptions options = new JsonSerializerOptions()
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    JsonObject? jsonObj = JsonSerializer.Deserialize<JsonObject>(classJsonObject.JsonText);
                    if (jsonObj != null)
                    {
                        jsonObj["m_Crc"] = 0;
                        classJsonObject.JsonText = JsonSerializer.Serialize(jsonObj, options);
                        rsrc.Data = classJsonObject;
                    }
                }
            }
        }
    }

    if (fromBundle)
        AddressablesJsonParser.ToBundle(ccd, args[1], args[1] + ".patched");
    else
        File.WriteAllText(args[1] + ".patched", AddressablesJsonParser.ToJson(ccd));

    File.Move(args[1], args[1] + ".old");
    File.Move(args[1] + ".patched", args[1]);
}

if (args.Length < 1)
{
    Console.WriteLine("need args: <mode> <file>");
    Console.WriteLine("modes: searchasset, patchcrc");
}
else if (args[0] == "searchasset")
{
    SearchExample(args);
}
else if (args[0] == "patchcrc")
{
    PatchCrcExample(args);
}
else
{
    Console.WriteLine("mode not supported");
}