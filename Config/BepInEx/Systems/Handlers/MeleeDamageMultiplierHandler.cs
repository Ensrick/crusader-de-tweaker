// Config/BepInEx/Systems/Handlers/MeleeDamageMultiplierHandler.cs
// DEBUG: Set BepInExConfigManager.DebugLogging = true to enable step-by-step logging.
//   [DBG-MeleeDmg-RAW]: Fires for EVERY OnUnitTakeMeleeDamage call (Pre AND Post).
//                       If this never appears during melee combat, the hook is not triggering.
//   [DBG-MeleeDmg-N]:   Step-by-step inside the Pre-filtered subscription.
using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    /// <summary>
    /// Handles melee damage multiplier application via event hooks.
    /// Extracted from UnitMultipliersConfig to reduce class size.
    /// </summary>
    internal static class MeleeDamageMultiplierHandler
    {
        /// <summary>
        /// Subscribes to melee damage event hook to apply melee damage multiplier in real-time.
        /// </summary>
        public static void Subscribe(ConfigEntry<float> multiplier)
        {
            Plugin.Logger.LogInfo("Subscribing to OnUnitTakeMeleeDamage event hook...");

            // RAW diagnostic subscription: no phase filter, fires for every event call.
            // If [DBG-MeleeDmg-RAW] never appears in the log during melee combat,
            // the underlying SHCDESE hook for unit melee damage is not firing.
            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Subscribe(args =>
                {
                    if (BepInExConfigManager.DebugLogging.Value)
                        Plugin.Logger.LogInfo(
                            $"[DBG-MeleeDmg-RAW] phase={args.Phase} attackerId={args.AttackingUnitId} damagedId={args.DamagedUnitId} dmg={args.Damage}");
                });

            // Main subscription: Pre phase only, applies melee damage multiplier.
            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging.Value;
                    try
                    {
                        if (dbg)
                            Plugin.Logger.LogInfo(
                                $"[DBG-MeleeDmg-1] Pre received: attackerId={args.AttackingUnitId} damagedId={args.DamagedUnitId} dmg={args.Damage} multiplier={multiplier.Value}x");

                        if (Mathf.Approximately(multiplier.Value, 1.0f))
                        {
                            if (dbg)
                                Plugin.Logger.LogInfo("[DBG-MeleeDmg-2] Multiplier is 1.0, skipping");
                            return;
                        }

                        eChimps attacker = Plugin.UnitApi.GetType(args.AttackingUnitId);
                        eChimps defender = Plugin.UnitApi.GetType(args.DamagedUnitId);

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-MeleeDmg-3] attacker={attacker} defender={defender}");

                        int baseDamage = args.Damage > 0
                            ? args.Damage
                            : Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-MeleeDmg-4] baseDamage={baseDamage} (from args={args.Damage > 0})");

                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        int modified = Mathf.Max(1, (int)(baseDamage * multiplier.Value));
                        args.Damage = modified;

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-MeleeDmg-5] dmg {baseDamage}->{modified} (x{multiplier.Value:F4})");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to apply unit melee damage multiplier: {ex.Message}\n{ex.StackTrace}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnUnitTakeMeleeDamage event hook");
        }
    }
}
