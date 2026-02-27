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
// - File paths are in %APPDATA%\BepInEx\config\ (not game directory)
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
    /// Config files are located in: %APPDATA%\BepInEx\config\CrusaderDETweaker\
    /// Full path example: C:\Users\[Username]\AppData\Roaming\BepInEx\config\CrusaderDETweaker\CrusaderDETweaker_*.toml
    ///
    /// This uses BepInEx's Paths.ConfigPath which automatically resolves to the correct config directory.
    /// </summary>
    internal static class ConfigPaths
    {
        /// <summary>
        /// Base config directory path (from BepInEx Paths.ConfigPath + plugin subfolder).
        /// Typically: %APPDATA%\BepInEx\config\CrusaderDETweaker\
        /// </summary>
        private static string ConfigDir => Path.Combine(Paths.ConfigPath, "CrusaderDETweaker");

        internal static string Units => Path.Combine(ConfigDir, "CrusaderDETweaker_Units.toml");
        internal static string Structures => Path.Combine(ConfigDir, "CrusaderDETweaker_Structures.toml");
        internal static string Globals => Path.Combine(ConfigDir, "CrusaderDETweaker_GameplaySettings.toml");
        internal static string UnitTags => Path.Combine(ConfigDir, "CrusaderDETweaker_UnitTags.toml");
        internal static string UnitTagsGenerated => Path.Combine(ConfigDir, "CrusaderDETweaker_UnitTags.generated.toml");
        internal static string UnitTagsAliases => Path.Combine(ConfigDir, "CrusaderDETweaker_UnitTags_Aliases.json");
        internal static string AutoGen => Path.Combine(ConfigDir, "CrusaderDETweaker_AutoGen.toml");

        // Future file ideas
        //internal static string UnitsMilitary => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Military.toml");
        //internal static string UnitsWorkers => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Workers.toml");
        //internal static string UnitsAnimals => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Animals.toml");
    }
}
