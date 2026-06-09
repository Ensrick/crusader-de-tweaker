// Config/BepInEx/Systems/Handlers/HealthMultiplierHandler.cs
// DEBUG: Set BepInExConfigManager.DebugLogging = true to enable step-by-step logging.
//   [DBG-Health-RAW]: Fires for EVERY OnUnitCreate call (Pre AND Post).
//                     If this never appears when units are recruited, the hook is not triggering.
//   [DBG-Health-N]:   Step-by-step inside the Post-filtered subscription.
using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.Toml.Units.Properties;
using CrusaderDETweaker.Data;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    /// <summary>
    /// Handles health multiplier application via event hooks.
    /// Extracted from UnitMultipliersConfig to reduce class size.
    /// </summary>
    internal static class HealthMultiplierHandler
    {
        /// <summary>
        /// Subscribes to unit creation event hook to apply health multiplier to newly created units.
        /// </summary>
        public static void Subscribe(ConfigEntry<float> multiplier)
        {
            Plugin.Logger.LogInfo("Subscribing to OnUnitCreate event hook...");

            // RAW diagnostic subscription: no phase filter, fires for every event call.
            // If [DBG-Health-RAW] never appears when units are recruited or spawned,
            // the underlying SHCDESE hook for unit creation is not firing.
            UnitR3EventHooks.OnUnitCreate.Observable
                .Subscribe(args =>
                {
                    if (BepInExConfigManager.DebugLogging.Value)
                        Plugin.Logger.LogInfo(
                            $"[DBG-Health-RAW] phase={args.Phase} unitType={args.UnitType} returnValue={args.ReturnValue}");
                });

            // Main subscription: Post phase only (unit ID available in ReturnValue after creation).
            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging.Value;
                    eChimps unitType = args.UnitType;

                    if (dbg)
                        Plugin.Logger.LogInfo(
                            $"[DBG-Health-1] Post received: unitType={unitType} returnValue={args.ReturnValue} multiplier={multiplier.Value}x");

                    // Health multiplier: skip if set to 1.0 (no change)
                    if (Mathf.Approximately(multiplier.Value, 1.0f))
                    {
                        if (dbg)
                            Plugin.Logger.LogInfo("[DBG-Health-2] Multiplier is 1.0, skipping");
                        return;
                    }

                    // Skip non-modifiable units
                    if (UnitCategories.IsNonModifiable(unitType))
                    {
                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-Health-3] Unit type {unitType} is non-modifiable, skipping");
                        return;
                    }

                    try
                    {
                        int unitId = (int)args.ReturnValue;

                        int currentMaxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
                        int newMaxHealth = Mathf.Max(1, (int)(currentMaxHealth * multiplier.Value));

                        if (dbg)
                            Plugin.Logger.LogInfo(
                                $"[DBG-Health-4] unitId={unitId} maxHealth {currentMaxHealth}->{newMaxHealth} (x{multiplier.Value:F4})");

                        Plugin.UnitApi.SetMaxHealth(unitId, newMaxHealth);
                        Plugin.UnitApi.SetCurrentHealth(unitId, newMaxHealth);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply health multiplier to {unitType}: {ex.Message}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnUnitCreate event hook");
        }
    }
}
