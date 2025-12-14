using System.IO;

namespace CrusaderDETweaker.Config.ConfigToml
{
    internal static class ConfigPaths
    {
        private static string ConfigDir => BepInEx.Paths.ConfigPath;

        internal static string Units => Path.Combine(ConfigDir, "CrusaderDETweaker_Units.toml");
        internal static string Structures => Path.Combine(ConfigDir, "CrusaderDETweaker_Structures.toml");

        // Future file ideas
        //internal static string UnitsMilitary => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Military.toml");
        //internal static string UnitsWorkers => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Workers.toml");
        //internal static string UnitsAnimals => Path.Combine(ConfigDir, "CrusaderDETweaker_Units_Animals.toml");
        //internal static string DamageMatrix => Path.Combine(ConfigDir, "CrusaderDETweaker_DamageMatrix.toml");
    }
}
