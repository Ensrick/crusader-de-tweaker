// Config/Toml/ConfigPaths.cs
//
// PURPOSE: Centralizes file paths for TOML configuration files.
//
// USAGE:
// - Units: Path to unit properties TOML file
// - Structures: Path to structure properties TOML file
//
// IMPORTANT FOR AI AGENTS:
// - All TOML file paths should use this class (don't hardcode paths)
// - Uses BepInEx Paths.ConfigPath for consistent config directory resolution
// - File paths are in the GAME DIRECTORY: {GameDir}\BepInEx\config\CrusaderDETweaker\ (not %APPDATA%)
// - Multiplayer host config sync: while UseHostSyncFiles is true every read resolves into HostSync\
//   (the lobby host's files). The sync never writes the player's own files, and generation runs only
//   at launch (before any lobby), so it always targets the player's own folder.
//
using System.IO;
using BepInEx;

namespace CrusaderDETweaker.Config.Toml
{
    /// <summary>
    /// File path management for TOML configuration files.
    ///
    /// Centralizes all TOML file paths to ensure consistency and enable easy path changes.
    ///
    /// Config files are located in: {GameDir}\BepInEx\config\CrusaderDETweaker\
    /// Full path example: C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker\CrusaderDETweaker_*.toml
    ///
    /// This uses BepInEx's Paths.ConfigPath which automatically resolves to the correct config directory.
    /// </summary>
    internal static class ConfigPaths
    {
        /// <summary>
        /// Base config directory path (from BepInEx Paths.ConfigPath + plugin subfolder).
        /// Typically: {GameDir}\BepInEx\config\CrusaderDETweaker\ (BepInEx resolves ConfigPath to the game folder)
        /// </summary>
        internal static string OwnConfigDir => Path.Combine(Paths.ConfigPath, "CrusaderDETweaker");

        /// <summary>
        /// The lobby host's synced files (multiplayer clients only): {OwnConfigDir}\HostSync\.
        /// </summary>
        internal static string HostSyncDir => Path.Combine(OwnConfigDir, "HostSync");

        /// <summary>
        /// True while a multiplayer client plays with the lobby host's configs. Set ONLY by
        /// Config.Sync.ConfigSyncManager; every TOML and CSV read then resolves into HostSyncDir.
        /// </summary>
        internal static bool UseHostSyncFiles;

        /// <summary>The folder the loaders currently read from (MatrixPaths uses it too).</summary>
        internal static string ActiveConfigDir => UseHostSyncFiles ? HostSyncDir : OwnConfigDir;

        internal const string UnitsFileName = "CrusaderDETweaker_Units.toml";
        internal const string StructuresFileName = "CrusaderDETweaker_Structures.toml";
        internal const string GlobalsFileName = "CrusaderDETweaker_GameplaySettings.toml";

        internal static string Units => Path.Combine(ActiveConfigDir, UnitsFileName);
        internal static string Structures => Path.Combine(ActiveConfigDir, StructuresFileName);
        internal static string Globals => Path.Combine(ActiveConfigDir, GlobalsFileName);

        // Future file ideas
        //internal static string UnitsMilitary => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Military.toml");
        //internal static string UnitsWorkers => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Workers.toml");
        //internal static string UnitsAnimals => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Animals.toml");
    }
}
