A Unityless way to read and write addressables.

Work in progress. Only the latest few formats are supported at this moment.

---

Using AddressablesTools is simple. Use either
AddressablesJsonParser.FromBundle("path/to/file.bundle");
or
AddressablesJsonParser.FromString(File.ReadAllText("path/to/file.json"));

From there, you can access the `Resources` dictionary which contains a mapping from an object (usually a string or number) to a list of resource locations.

In the `searchasset` example below, we can look up an asset string and find all of the bundles that are needed to load it. To do so, we find all keys that contain the substring we're searching for and that have resource locations with a `ProviderId` of `BundledAssetProvider`. After that, we can look up the `Dependency` id back into the `Resources` dictionary to find all of the necessary bundles. In this list, the first item is always the bundle that contains the asset, and all other bundles are dependencies needed by the first bundle.

This can be useful if you want to know what bundles you need to load in order to load an asset in a tool without having to load _every_ bundle in the game.

---

If you're still confused, maybe check out https://github.com/nesrak1/SeaOfStarsSpriteExtractor for a more "real-life" example.

---

The "Example" program contains two tools: searchasset and patchcrc.

The searchasset command takes an argument to the catalog.json or catalog.bundle file. It will then ask you for a string to search for and will display any results that it finds.

The patchcrc command also takes an argument to catalog.json or catalog.bundle. It sets the m_Crc of all entries to 0, effectively disabling all CRC checks.

---

This software is not sponsored by or affiliated with Unity Technologies or its affiliates. "Unity" is a registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere.