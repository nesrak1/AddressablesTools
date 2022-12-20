using AddressablesTools.Catalog;
using AddressablesTools.JSON;
using System;
using System.Collections.Generic;
using System.Text;
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
    }
}
