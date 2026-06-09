// AI Dev Comment: IConfigSystem implementation for unit tags configuration.
// Orchestrates generation and loading of CrusaderDETweaker_UnitTags.toml file.
// Integrates unit tag-based damage modifiers into the three-tier config system.

using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Toml.Units.Properties;

namespace CrusaderDETweaker.Config.Toml.UnitTags
{
    /// <summary>
    /// Configuration system for unit tags and tag interactions.
    /// Allows users to define custom damage modifiers based on unit tags.
    /// </summary>
    internal class UnitTagsConfigSystem : IConfigSystem
    {
        public string Name => "Unit Tags";
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Get the full path to the unit tags config file.
        /// </summary>
        private static string GetConfigPath() => ConfigPaths.UnitTags;

        /// <summary>
        /// Generate default unit tags config file if it doesn't exist.
        /// </summary>
        public void GenerateDefaults()
        {
            // DEACTIVATED: Tag system is too complex for automated management within context limits.
            // Complexity and interaction accuracy were better handled by manual CSV overrides.
            Plugin.Logger.LogInfo("Tags property generation is DEACTIVATED.");
            /*
            // Phase D: Initialize tags before discovery
            TagsProperty.RegisterDefaultTags();

            AutoGenConfig.Load();
            
            Plugin.Logger.LogInfo($"[System] AutoGen Config Loaded: Enabled={AutoGenConfig.AutoGenerateTags}, Force={AutoGenConfig.ForceRegenerate}");

            // AutoGen manages its own generation in Load()
            if (AutoGenConfig.AutoGenerateTags)
            {
                Plugin.Logger.LogInfo("[System] Starting UnitTagsConfigGenerator.Generate...");
                UnitTagsConfigGenerator.Generate(ConfigPaths.UnitTagsGenerated);
                Plugin.Logger.LogInfo("[System] UnitTagsConfigGenerator.Generate completed.");
                
                // If main config doesn't exist, use the generated one
                if (!File.Exists(ConfigPaths.UnitTags) || AutoGenConfig.ForceRegenerate)
                {
                    Plugin.Logger.LogInfo("Copying auto-generated tags to semantic config.");
                    File.Copy(ConfigPaths.UnitTagsGenerated, ConfigPaths.UnitTags, true);
                    AutoGenConfig.ClearForceRegenerate();
                }
            }
            else if (!File.Exists(ConfigPaths.UnitTags))
            {
                // Fallback to minimal generation if auto-gen disabled and file missing
                UnitTagsConfigGenerator.Generate(ConfigPaths.UnitTags);
            }
            */
        }

        /// <summary>
        /// Load unit tags configuration from file.
        /// </summary>
        public void Load()
        {
            // DEACTIVATED: Tag system is too complex for automated management within context limits.
            Plugin.Logger.LogInfo("Unit Tags loading is DEACTIVATED.");
            IsLoaded = false;
            /*
            // Ensure generation happens before loading
            GenerateDefaults();

            // Phase D: Re-register tags again just in case (though GenerateDefaults handles it)
            // Actually, Load calls register internally via loader, but defaults ensure baseline.
            // TagsProperty.RegisterDefaultTags(); // Already called in GenerateDefaults

            string filePath = ConfigPaths.UnitTags;

            try
            {
                Plugin.Logger.LogDebug($"Loading unit tags config: {filePath}");

                if (!File.Exists(filePath))
                {
                    Plugin.Logger.LogWarning($"Unit tags config not found, skipping: {filePath}");
                    IsLoaded = false;
                    return;
                }

                bool success = UnitTagsConfigLoader.Load(filePath);
                IsLoaded = success;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load unit tags config: {ex.Message}");
                IsLoaded = false;
            }
            */
        }

        /// <summary>
        /// Validate unit tags configuration.
        /// Currently no validation needed - tag system is additive only.
        /// </summary>
        public bool Validate()
        {
            if (!IsLoaded)
                return false;

            // No validation needed for tag system (purely additive)
            return true;
        }
    }
}
