// Config/ConfigManager.cs
//
// PURPOSE: Central orchestrator for all configuration systems (TOML and CSV).
//
// INITIALIZATION ORDER (CRITICAL - DO NOT CHANGE):
// 1. Generate default config files (if missing) — FILE I/O ONLY, no API calls
// 2. Load TOML/CSV configs — registers hooks, reads files. No API calls.
// 3. Validate all configs (optional)
//
// *** CRITICAL WARNING FOR AI AGENTS ***
// This runs during LibraryLoaded, BEFORE any game session exists.
// DO NOT call any game API here (UnitApi, BuildingApi, GlobalsApi, PlayerApi).
// Doing so causes a native ACCESS_VIOLATION crash. The crash appears inside
// SHCDE-SE's init in the log — misleading, but the cause is us.
// ALL game API calls must be deferred to OnStartMap / OnLoadMap hooks.
// Past CTDs from violating this rule:
//   - GenerateDefaultConfig calling TryGetFromAPI for missing entries
//   - CaptureOriginalDefaults calling GetMeleeDamageFromTo for every unit pair
//   - ApplyAllGlobalConfigs calling GameGlobalsManager.SetValue at load time
//
// IMPORTANT FOR AI AGENTS:
// - ConfigSystems array defines which systems are initialized
// - All config systems implement IConfigSystem interface for consistency
// - TOML configs load before CSV matrices (CSV can override TOML changes)
//
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.DamageMatrix;
using CrusaderDETweaker.Config.Toml.Systems;
using SHCDESE.Interop;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Central manager for all configuration systems (TOML and CSV).
    /// 
    /// This class orchestrates the initialization of all config systems using the unified
    /// IConfigSystem interface. It ensures proper initialization order:
    /// 
    /// 1. Generate default config files (file I/O only)
    /// 2. Load global and TOML configs
    /// 3. Load CSV damage matrices after TOML configs
    /// 4. Validate all configs (optional)
    /// 
    /// The unified interface allows different config types (TOML, CSV) to be managed
    /// consistently, making it easy to add new config systems in the future.
    /// </summary>
    internal static class ConfigManager
    {
        private static readonly IConfigSystem[] ConfigSystems = new IConfigSystem[]
        {
            // DEACTIVATED: Tag system is too complex for automated management within context limits
            // new UnitTagsConfigSystem(),  // Load tags FIRST (before units, so tag interactions are ready)
            new GlobalConfigSystem(),
            new UnitConfigSystem(),
            new StructureConfigSystem(),
            new DamageMatrixConfigSystem()
        };

        /// <summary>
        /// Initialize all configuration systems.
        /// 
        /// This method orchestrates the complete initialization sequence:
        /// 1. Generate default config files (if missing)
        /// 2. Load config values and register deferred event hooks
        /// 3. Validate all configs (if requested)
        /// 
        /// Native API reads and writes are not safe here because no game session exists yet.
        /// Systems must defer those operations to OnStartMap or OnLoadMap handlers.
        /// </summary>
        /// <param name="runValidation">Whether to run validation after loading (default: true)</param>
        internal static void Initialize(bool runValidation = true)
        {
            Plugin.Logger.LogInfo($"Initializing {ConfigSystems.Length} config systems...");

            // STEP 3: Generate default config files for all systems (if they don't exist)
            // This creates template files that users can edit to customize game behavior
            Plugin.Logger.LogInfo("Generating default config files...");
            int generatedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    Plugin.Logger.LogInfo($"[DIAG] GenerateDefaults: {system.Name}...");
                    system.GenerateDefaults();
                    generatedCount++;
                    Plugin.Logger.LogInfo($"[DIAG] GenerateDefaults OK: {system.Name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to generate defaults for {system.Name}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Generated defaults for {generatedCount}/{ConfigSystems.Length} systems");

            // STEP 4: Load and apply all configs
            Plugin.Logger.LogInfo("Loading config files...");
            int loadedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    Plugin.Logger.LogInfo($"[DIAG] Load: {system.Name}...");
                    system.Load();
                    loadedCount++;
                    Plugin.Logger.LogInfo($"[DIAG] Load OK: {system.Name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to load {system.Name}: {ex.Message}");
                }
            }

            Plugin.Logger.LogInfo($"Loaded {loadedCount}/{ConfigSystems.Length} systems");

            // STEP 5: Run validation if requested
            // Validation checks that configs are correct and don't contain errors
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
            statusLines.AppendLine("Config Systems Status:");
            
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

