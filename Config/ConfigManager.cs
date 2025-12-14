using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.DamageMatrix;
using CrusaderDETweaker.Config.Toml.Armor;
using CrusaderDETweaker.Config.Toml.Systems;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Manages initialization of all configuration systems (TOML and CSV).
    /// Uses the unified IConfigSystem interface for consistent initialization.
    /// </summary>
    internal static class ConfigManager
    {
        private static readonly IConfigSystem[] ConfigSystems = new IConfigSystem[]
        {
            new UnitConfigSystem(),
            new StructureConfigSystem(),
            new ArmorConfigSystem(),
            new Config.Toml.Tags.TagConfigSystem(),
            new DamageMatrixConfigSystem()
        };

        /// <summary>
        /// Initialize all configuration systems.
        /// Generates default config files if they don't exist, then loads and applies them.
        /// Optionally runs validation for systems that support it.
        /// </summary>
        /// <param name="runValidation">Whether to run validation after loading (default: true)</param>
        internal static void Initialize(bool runValidation = true)
        {
            Plugin.Logger.LogInfo($"Initializing {ConfigSystems.Length} config systems...");

            // Generate default configs for all systems
            Plugin.Logger.LogInfo("Generating default config files...");
            int generatedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    system.GenerateDefaults();
                    generatedCount++;
                    Plugin.Logger.LogDebug($"  Generated defaults for {system.Name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to generate defaults for {system.Name}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Generated defaults for {generatedCount}/{ConfigSystems.Length} systems");

            // Load and apply all configs
            Plugin.Logger.LogInfo("Loading config files...");
            int loadedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    system.Load();
                    loadedCount++;
                    Plugin.Logger.LogDebug($"  Loaded {system.Name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to load {system.Name}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Loaded {loadedCount}/{ConfigSystems.Length} systems");

            // Run validation if requested
            if (runValidation)
            {
                Plugin.Logger.LogInfo("Running validation...");
                int validatedCount = 0;
                foreach (var system in ConfigSystems)
                {
                    try
                    {
                        if (system.Validate())
                        {
                            validatedCount++;
                            Plugin.Logger.LogDebug($"  Validated {system.Name}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"  Validation failed for {system.Name}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Logger.LogError($"  Validation error for {system.Name}: {ex.Message}");
                    }
                }
                Plugin.Logger.LogInfo($"Initialized {ConfigSystems.Length} config systems ({loadedCount} loaded, {validatedCount} validated)");
            }
            else
            {
                Plugin.Logger.LogInfo($"Initialized {ConfigSystems.Length} config systems ({loadedCount} loaded)");
            }
        }

        /// <summary>
        /// Get all registered config systems.
        /// </summary>
        internal static IEnumerable<IConfigSystem> GetAllSystems()
        {
            return ConfigSystems;
        }

        /// <summary>
        /// Get the status of all config systems.
        /// </summary>
        internal static string GetStatus()
        {
            var loadedCount = ConfigSystems.Count(s => s.IsLoaded);
            return $"Config Systems: {loadedCount}/{ConfigSystems.Length} loaded";
        }

        /// <summary>
        /// Get detailed status information for all config systems.
        /// </summary>
        internal static string GetDetailedStatus()
        {
            var statusLines = new System.Text.StringBuilder();
            statusLines.AppendLine("=== Config Systems Status ===");
            
            foreach (var system in ConfigSystems)
            {
                var status = system.IsLoaded ? "✓ Loaded" : "✗ Not Loaded";
                statusLines.AppendLine($"  {system.Name}: {status}");
            }
            
            var loadedCount = ConfigSystems.Count(s => s.IsLoaded);
            statusLines.AppendLine($"\nTotal: {loadedCount}/{ConfigSystems.Length} systems loaded");
            
            return statusLines.ToString();
        }

        /// <summary>
        /// Reload all configuration systems.
        /// Useful for hot-reloading configs during development or after file changes.
        /// </summary>
        internal static void ReloadAll()
        {
            Plugin.Logger.LogInfo("Reloading all config systems...");
            
            foreach (var system in ConfigSystems)
            {
                try
                {
                    system.Load();
                    Plugin.Logger.LogInfo($"Reloaded {system.Name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to reload {system.Name}: {ex.Message}");
                }
            }
            
            Plugin.Logger.LogInfo("All config systems reloaded");
        }
    }
}

