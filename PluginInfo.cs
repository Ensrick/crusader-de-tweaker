// PluginInfo.cs
//
// Single source of truth for the plugin's identity and version.
//
// Referenced by:
// - Plugin.cs           [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
// - Properties/AssemblyInfo.cs  [assembly: AssemblyVersion/AssemblyFileVersion]
// - scripts/build.ps1   stamps info.json's "Version" from PLUGIN_VERSION at build time
//
// To bump the version, change PLUGIN_VERSION here and rebuild — nothing else needs editing.
// PLUGIN_GUID MUST stay "CrusaderDETweaker": it is the BepInEx plugin folder name.

namespace CrusaderDETweaker
{
    internal static class PluginInfo
    {
        public const string PLUGIN_GUID = "CrusaderDETweaker";
        public const string PLUGIN_NAME = "Crusader DE Tweaker";
        public const string PLUGIN_VERSION = "2.6.4";
    }
}
