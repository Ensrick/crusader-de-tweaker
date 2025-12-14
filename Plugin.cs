using System;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using CrusaderDETweaker.Config.DamageMatrix;

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

                // Initialize all config systems (TOML and CSV) using unified interface
                // Validation is now integrated into the unified system
                ConfigManagerToml.Initialize(runValidation: true);

                // Initialize BepInEx config and apply runtime multipliers (LOAD LAST)
                ConfigManagerBepinex.Initialize(Config);
                ConfigManagerBepinex.ApplyAllMultiplierConfigs(); // REAL TIME HOOKS

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