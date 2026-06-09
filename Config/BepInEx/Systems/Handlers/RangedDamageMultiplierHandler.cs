// Config/BepInEx/Systems/Handlers/RangedDamageMultiplierHandler.cs
// DEBUG: Set BepInExConfigManager.DebugLogging = true to enable step-by-step logging.
//   [DBG-RangedDmg-RAW]: Fires for EVERY OnUnitTakeProjectileDamageEx call (Pre AND Post).
//                        If this never appears when units are shot, the hook is not triggering.
//   [DBG-RangedDmg-N]:   Step-by-step inside the Pre-filtered subscription.
using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    /// <summary>
    /// Handles ranged damage multiplier application via event hooks.
    /// Extracted from UnitMultipliersConfig to reduce class size.
    /// </summary>
    internal static class RangedDamageMultiplierHandler
    {
        /// <summary>
        /// Subscribes to ranged damage event hook to apply ranged damage multiplier in real-time.
        /// </summary>
        public static void Subscribe(ConfigEntry<float> multiplier)
        {
            Plugin.Logger.LogInfo("Subscribing to OnUnitTakeProjectileDamageEx event hook...");

            // RAW diagnostic subscription: no phase filter, fires for every event call.
            // If [DBG-RangedDmg-RAW] never appears in the log when units are hit by projectiles,
            // the underlying SHCDESE hook for projectile damage is not firing.
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                .Subscribe(args =>
                {
                    if (BepInExConfigManager.DebugLogging.Value)
                        Plugin.Logger.LogInfo(
                            $"[DBG-RangedDmg-RAW] phase={args.Phase} dmg={args.Damage}");
                });

            // Main subscription: Pre phase only, applies ranged damage multiplier.
            // Uses Ex version which allows damage modification.
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging.Value;
                    try
                    {
                        if (dbg)
                            Plugin.Logger.LogInfo(
                                $"[DBG-RangedDmg-1] Pre received: dmg={args.Damage} multiplier={multiplier.Value}x");

                        if (Mathf.Approximately(multiplier.Value, 1.0f))
                        {
                            if (dbg)
                                Plugin.Logger.LogInfo("[DBG-RangedDmg-2] Multiplier is 1.0, skipping");
                            return;
                        }

                        int original = args.Damage;

                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        int modified = Mathf.Max(1, (int)(args.Damage * multiplier.Value));
                        args.Damage = modified;

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-RangedDmg-3] dmg {original}->{modified} (x{multiplier.Value:F4})");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply ranged damage multiplier: {ex.Message}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnUnitTakeProjectileDamageEx event hook");
        }
    }
}
