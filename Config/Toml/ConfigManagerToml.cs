using CrusaderDETweaker.Config.Toml.Armor;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Manages initialization of all TOML-based configuration systems.
    /// </summary>
    internal static class ConfigManagerToml
    {
        /// <summary>
        /// Initialize all TOML configuration systems.
        /// Generates default config files if they don't exist, then loads and applies them.
        /// </summary>
        internal static void Initialize()
        {
            // Create default configs if they don't exist
            ConfigGenerator.GenerateDefaultConfigUnits();
            ConfigGenerator.GenerateDefaultConfigStructures();
            ArmorConfigGenerator.GenerateDefault();

            // Load and apply all configs
            ConfigLoader.ApplyAllUnitConfigs();
            ConfigLoader.ApplyAllStructureConfigs();
            ArmorConfigLoader.Load();
        }
    }
}