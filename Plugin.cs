using System;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.DamageMatrix;
using CrusaderDETweaker.Config.DamageMatrix.Core;

namespace CrusaderDETweaker
{
    [BepInDependency(SHCDESE.BepInEx.Bootstrap.Plugin.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ensrick.crusaderdetweaker", "Crusader DE Tweaker", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance { get; private set; }
        internal new static ManualLogSource Logger;

        // Cached APIs
        internal static GameUnitManagerAPI UnitApi { get; private set; }
        internal static GameBuildingManagerAPI BuildingApi { get; private set; }

        // ============================================================================
        // MODEL DISCOVERY MODE
        // ============================================================================
        // Set to true to enable model discovery mode:
        //   - Automatically regenerates Discovered_*.toml files on startup
        //   - Enables debugging and validation output
        //   - Overwrites existing discovery files
        //
        // Set to false for user mode:
        //   - Only loads discovery files if they exist
        //   - Never regenerates or overwrites files
        //   - User config files (CrusaderDETweaker_*.toml) are never touched
        // ============================================================================
        private const bool ENABLE_MODEL_DISCOVERY_MODE = true;

        /// <summary>
        /// Check if model discovery mode is enabled.
        /// </summary>
        internal static bool IsModelDiscoveryModeEnabled => ENABLE_MODEL_DISCOVERY_MODE;

        private bool _isInitialized;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            Logger.LogInfo("CrusaderDETweaker loading…");

            SHCDESE.API.LowLevel.CrusaderLibrary.Instance.LibraryLoaded += _ => CrusaderLibrary_LibraryLoaded();
        }

        private void CrusaderLibrary_LibraryLoaded()
        {
            if (_isInitialized) return;

            try
            {
                UnitApi = GameUnitManagerAPI.Instance;
                BuildingApi = GameBuildingManagerAPI.Instance;

                if (UnitApi == null || BuildingApi == null)
                {
                    Logger.LogError("Failed to get Unit or Building API — mod cannot function.");
                    return;
                }

                // Model Discovery Mode: Regenerate discovery files automatically
#pragma warning disable CS0162 // Unreachable code (expected when ENABLE_MODEL_DISCOVERY_MODE is false)
                if (ENABLE_MODEL_DISCOVERY_MODE)
                {
                    Logger.LogInfo("=== MODEL DISCOVERY MODE ENABLED ===");
                    Logger.LogInfo("Regenerating discovered properties files...");
                    try
                    {
                        // Ensure CSV data is captured before discovery
                        CsvMatrixReader.CaptureOriginalDefaults();
                        
                        // Run discovery test to regenerate files (overwrites existing)
                        Systems.ModelDiscovery.ModelDiscoveryTester.RunTest();
                        Logger.LogInfo("Model discovery completed, files regenerated.");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError($"Model discovery failed: {ex.Message}");
                        Logger.LogError(ex.StackTrace);
                        Logger.LogWarning("Continuing with existing discovery files (if any) or old system.");
                    }
                }
#pragma warning disable CS0162 // Unreachable code (expected when ENABLE_MODEL_DISCOVERY_MODE is true)
                else
                {
                    // User Mode: Only load discovery files if they exist, never regenerate
                    string discoveredUnitsPath = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "BepInEx", "config", "CrusaderDETweaker", "Discovered_Units.toml"
                    );
                    
                    if (System.IO.File.Exists(discoveredUnitsPath))
                    {
                        Logger.LogInfo("Discovered properties files found, loading...");
                    }
                    else
                    {
                        Logger.LogInfo("Discovered properties files not found. Using legacy damage system.");
                        Logger.LogInfo("To regenerate discovery files, set ENABLE_MODEL_DISCOVERY_MODE = true in Plugin.cs");
                    }
                }
#pragma warning restore CS0162

                // Initialize all config systems (TOML and CSV) using unified interface
                // Validation is now integrated into the unified system
                ConfigManager.Initialize(runValidation: true);

                // Initialize BepInEx config and apply runtime multipliers (LOAD LAST)
                // BepInEx configs use real-time event hooks for runtime modifications
                BepInExConfigManager.Initialize(Config);

                Logger.LogInfo("Crusader DE Tweaker initialized successfully.");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CrusaderDETweaker failed to initialize: {ex}");
                _isInitialized = false;
            }
        }
    }
}