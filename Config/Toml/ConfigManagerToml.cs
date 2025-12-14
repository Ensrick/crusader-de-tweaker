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
    internal static class ConfigManagerToml
    {
        private static readonly IConfigSystem[] ConfigSystems = new IConfigSystem[]
        {
            new UnitConfigSystem(),
            new StructureConfigSystem(),
            new ArmorConfigSystem(),
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
            // Generate default configs for all systems
            foreach (var system in ConfigSystems)
            {
                system.GenerateDefaults();
            }

            // Load and apply all configs
            foreach (var system in ConfigSystems)
            {
                system.Load();
            }

            // Run validation if requested
            if (runValidation)
            {
                int validatedCount = 0;
                foreach (var system in ConfigSystems)
                {
                    if (system.Validate())
                    {
                        validatedCount++;
                    }
                }
                Plugin.Logger.LogInfo($"Initialized {ConfigSystems.Length} config systems ({validatedCount} validated)");
            }
            else
            {
                Plugin.Logger.LogInfo($"Initialized {ConfigSystems.Length} config systems");
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
        /// Reload all TOML configuration systems.
        /// Useful for hot-reloading configs during development or after file changes.
        /// </summary>
        internal static void ReloadAll()
        {
            Plugin.Logger.LogInfo("Reloading all TOML config systems...");
            
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
            
            Plugin.Logger.LogInfo("All TOML config systems reloaded");
        }
    }
}