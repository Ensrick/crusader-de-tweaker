using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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

                ConfigManager.Initialize(Config);
                ConfigManager.ApplyAllUnitConfigs();
                ConfigManager.ApplyAllBuildingConfigs();
                ConfigManager.ApplyAllMultiplierConfigs(); // REAL TIME HOOKS - LOAD LAST

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