using System.Text.Json.Serialization;

namespace AddressablesTools.JSON
{
#pragma warning disable IDE1006
    internal class ContentCatalogDataJson
    {
        public string m_LocatorId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string m_BuildResultHash { get; set; } // ???
        public ObjectInitializationDataJson m_InstanceProviderData { get; set; }
        public ObjectInitializationDataJson m_SceneProviderData { get; set; }
        public ObjectInitializationDataJson[] m_ResourceProviderData { get; set; }
        public string[] m_ProviderIds { get; set; }
        public string[] m_InternalIds { get; set; }
        public string m_KeyDataString { get; set; }
        public string m_BucketDataString { get; set; }
        public string m_EntryDataString { get; set; }
        public string m_ExtraDataString { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] m_Keys { get; set; } // 1.1.3 - 1.16.10
        public SerializedTypeJson[] m_resourceTypes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] m_InternalIdPrefixes { get; set; } // 1.16.10+
    }
#pragma warning restore IDE1006
}
