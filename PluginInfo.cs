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
        // Simulation behaviour version (2.11.9, GitLab #15): raise it in every release that changes what the SAME config
        // files do in a match (a fix to how a value is applied, a new default behaviour, a different apply moment).
        // Host config sync rejects a host whose patch version differs unless both have the same value here.
        // 1: 2.11.9; 2: 2.11.10 (structure damage untouched at all 1.0). 2.11.3 (Engineer Speed while working) sends no
        // value and is in every version that sends one, so it needs no step of its own.
        public const int SIMULATION_VERSION = 2;
        public const string PLUGIN_VERSION = "2.11.11";
    }
}
