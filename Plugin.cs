// Plugin.cs
//
// PURPOSE: Main BepInEx plugin entry point - orchestrates all initialization.
//
// INITIALIZATION FLOW:
// 1. Awake() - Called by BepInEx when plugin loads, registers library load event
// 2. CrusaderLibrary_LibraryLoaded() - Called when SHCDE-SE library loads (game APIs available)
//    - Acquires UnitApi and BuildingApi instances
//    - Calls ConfigManager.Initialize() to load TOML/CSV configs
//    - Calls BepInExConfigManager.Initialize() to set up real-time multipliers
//    - Creates marker file for launch script
//
// IMPORTANT FOR AI AGENTS:
// - Plugin GUID "CrusaderDETweaker" MUST match folder name in BepInEx/plugins/
// - DO NOT change GUID without updating build paths and documentation
// - UnitApi and BuildingApi are static for easy access throughout codebase
// - Initialization happens asynchronously (waits for SHCDE-SE library to load)
//
using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using CrusaderDETweaker.Config.BepInEx;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Main plugin entry point for Crusader DE Tweaker.
    /// 
    /// This BepInEx plugin provides configuration systems for modifying unit and structure properties
    /// in Stronghold Crusader: Definitive Edition. It integrates with SHCDE-SE (Script Extender) to
    /// access game internals and apply modifications.
    /// 
    /// Initialization Flow:
    /// 1. Awake() - Registers library load event handler
    /// 2. CrusaderLibrary_LibraryLoaded() - Initializes APIs and config systems
    /// 3. ConfigManager.Initialize() - Loads TOML/CSV configurations
    /// 4. BepInExConfigManager.Initialize() - Sets up real-time multiplier hooks
    /// </summary>
    [BepInDependency(SHCDESE.BepInEx.Bootstrap.Plugin.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    /// <summary>
    /// CRITICAL: Plugin GUID must match the folder name in BepInEx/plugins/
    /// 
    /// BepInEx loads plugins from folders matching their GUID. This GUID determines:
    /// - Plugin folder: BepInEx/plugins/CrusaderDETweaker/
    /// - Config file: BepInEx/config/CrusaderDETweaker.cfg
    /// - Build output: MUST be in plugins/CrusaderDETweaker/CrusaderDETweaker.dll
    /// 
    /// DO NOT CHANGE THIS GUID without updating:
    /// 1. CrusaderDETweaker.csproj OutputPath (both Debug and Release)
    /// 2. build.ps1 DLL path references
    /// 3. All documentation references to the plugin folder name
    /// 
    /// The GUID "CrusaderDETweaker" is the CORRECT and INTENDED value.
    /// Previous incorrect GUID "ensrick.crusaderdetweaker" was a mistake.
    /// </summary>
    [BepInPlugin("CrusaderDETweaker", "Crusader DE Tweaker", "2.5.0")]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Singleton instance of the plugin (for static access).
        /// </summary>
        internal static Plugin Instance { get; private set; }
        
        /// <summary>
        /// Logger instance for plugin-wide logging.
        ///
        /// Log levels (configured in BepInEx.cfg under [Logging]):
        /// - LogInfo: General information (enabled by default)
        /// - LogWarning: Warnings about potential issues (enabled by default)
        /// - LogError: Errors that prevent functionality (enabled by default)
        /// - LogDebug: Detailed debugging information (disabled by default)
        ///
        /// To enable debug logging:
        /// 1. Edit BepInEx\config\BepInEx.cfg
        /// 2. Find [Logging] section
        /// 3. Change LogLevels to include Debug: "Fatal, Error, Warning, Message, Info, Debug"
        ///
        /// All code in this project uses Plugin.Logger for centralized logging.
        /// </summary>
        internal new static ManualLogSource Logger;

        /// <summary>
        /// Cached reference to the game's unit manager API.
        /// Provides access to unit properties and modification methods.
        /// </summary>
        internal static GameUnitManagerAPI UnitApi { get; private set; }
        
        /// <summary>
        /// Cached reference to the game's building manager API.
        /// Provides access to structure properties and modification methods.
        /// </summary>
        internal static GameBuildingManagerAPI BuildingApi { get; private set; }

        /// <summary>
        /// Cached reference to the game's global properties manager.
        /// Provides access to game-wide constants and assembly patches.
        /// </summary>
        internal static GameGlobalsManager GlobalsApi { get; private set; }

        /// <summary>
        /// Cached reference to the game's player manager API.
        /// Provides access to player properties.
        /// </summary>
        internal static GamePlayerManagerAPI PlayerApi { get; private set; }




        private bool _isInitialized;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            Logger.LogInfo("CrusaderDETweaker loading");

            SHCDESE.API.LowLevel.CrusaderLibrary.Instance.LibraryLoaded += (handle, memory) => CrusaderLibrary_LibraryLoaded();
        }

        /// <summary>
        /// Called when the SHCDE-SE library has finished loading.
        /// This is when the game APIs become available for use.
        /// 
        /// Initialization sequence:
        /// 1. Acquire game API instances
        /// 2. Initialize all configuration systems (TOML and CSV)
        /// 3. Initialize BepInEx config systems (real-time multipliers)
        /// </summary>
        private void CrusaderLibrary_LibraryLoaded()
        {
            // Prevent double initialization
            if (_isInitialized) return;

            try
            {
                // Acquire game API instances - these provide access to unit/structure properties
                UnitApi = GameUnitManagerAPI.Instance;
                BuildingApi = GameBuildingManagerAPI.Instance;
                GlobalsApi = GameGlobalsManager.Instance;
                PlayerApi = GamePlayerManagerAPI.Instance;

                if (UnitApi == null || BuildingApi == null)
                {
                    Logger.LogError("Failed to get Unit or Building API � mod cannot function.");
                    return;
                }

                // Initialize all config systems (TOML and CSV) using unified interface
                ConfigManager.Initialize(runValidation: true);

                // Initialize BepInEx config systems (real-time multipliers)
                // Use a custom ConfigFile in the plugin subfolder instead of the default BepInEx location
                var bepInExCfgPath = Path.Combine(Paths.ConfigPath, "CrusaderDETweaker", "CrusaderDETweaker_GlobalMultipliers.cfg");
                BepInExConfigManager.Initialize(new ConfigFile(bepInExCfgPath, true));

                // QA self-check: run the core-logic unit suites on load and log PASS/FAIL to
                // LogOutput.log. Mock handlers only — no game-API calls, safe during init. This
                // surfaces a regression immediately in the log if core logic ever breaks.
                CrusaderDETweaker.Tests.CoreTestRunner.RunAllTests();

                Logger.LogInfo("Crusader DE Tweaker initialized successfully.");
                _isInitialized = true;

                // Write marker file for launch script to detect when initialization is complete
                // Marker file location: %APPDATA%\BepInEx\config\CrusaderDETweaker\plugin_initialized.ready
                // This file signals to the launch script (launch_game.ps1) that plugin initialization is complete
                // The script polls for this file and closes the game gracefully after detecting it
                try
                {
                    // DEV NOTE: Marker file path matches launch_game.ps1 marker file path
                    // Path: %APPDATA%\BepInEx\config\CrusaderDETweaker\plugin_initialized.ready
                    string markerFilePath = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "BepInEx", "config", "CrusaderDETweaker", "plugin_initialized.ready"
                    );
                    
                    string markerDir = System.IO.Path.GetDirectoryName(markerFilePath);
                    if (!System.IO.Directory.Exists(markerDir))
                    {
                        System.IO.Directory.CreateDirectory(markerDir);
                    }
                    
                    string markerContent = DateTime.Now.ToString("o") + Environment.NewLine + "Plugin initialization complete.";
                    System.IO.File.WriteAllText(markerFilePath, markerContent);
                    
                    Logger.LogDebug($"Initialization marker file created: {markerFilePath}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to write initialization marker file: {ex.Message}");
                    Logger.LogDebug($"Marker file exception: {ex.GetType().FullName} - {ex.StackTrace}");
                    // Non-fatal, continue anyway
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"OUTER CATCH: CrusaderDETweaker failed to initialize: {ex.Message}");
                Logger.LogError($"OUTER CATCH: Exception type: {ex.GetType().FullName}");
                Logger.LogError($"OUTER CATCH: Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"OUTER CATCH: Inner exception: {ex.InnerException.Message}");
                }
                _isInitialized = false;
            }
            
            Logger.LogInfo("CrusaderLibrary_LibraryLoaded method completed");
        }
    }
}
