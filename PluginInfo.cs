// PluginInfo.cs
//
// Single source of truth for the plugin's identity and version.
//
// Referenced by:
// - Plugin.cs           [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
// - Properties/AssemblyInfo.cs  [assembly: AssemblyVersion/AssemblyFileVersion]
// - scripts/build.ps1   stamps the OUTPUT info.json's "Version" from PLUGIN_VERSION
//
// To bump the version, change PLUGIN_VERSION here AND "Version" in info.json (package_release.ps1 and
// ship.ps1 preflight refuse a mismatch; the tracked info.json is never rewritten by a build).
// PLUGIN_GUID MUST stay "CrusaderDETweaker": it is the BepInEx plugin folder name.

namespace CrusaderDETweaker
{
    internal static class PluginInfo
    {
        public const string PLUGIN_GUID = "CrusaderDETweaker";
        public const string PLUGIN_NAME = "Crusader DE Tweaker";
        public const string PLUGIN_VERSION = "2.6.8";
    }
}
