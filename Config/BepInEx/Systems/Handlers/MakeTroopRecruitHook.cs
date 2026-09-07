// Config/BepInEx/Systems/Handlers/MakeTroopRecruitHook.cs
//
// PURPOSE: Gate recruits at RECRUIT time by hooking the game's EngineInterface.GameAction(MakeTroop)
//          command. Two independent gates:
//            1. per-unit-type MaxCount caps;
//            2. the RequiresHorse requirement for units the game does not gate itself (Arabian /
//               Bedouin mercenaries: the native horse check exists only for the 7 Barracks units,
//               see Config/Core/StableTracking.cs) - refused when no stable horse slot is free.
//
// WHY THIS IS NEEDED (and why UnitCapHandler's OnUnitCreate path is not enough):
//   SHCDE-SE raises OnUnitCreate only from c_game_unit_spawn_ex. Recruiting a soldier from the
//   Barracks / Mercenary Post does NOT go through that path (recruiting transforms a peasant into
//   a soldier rather than spawning a fresh unit), so the spawn-based UnitCapHandler never sees
//   recruited units and the cap was silently ignored for them. The vanilla recruit command routes
//   through EngineInterface.GameAction(Enums.GameActionCommand.MakeTroop, ...), so we intercept
//   that and refuse / trim over-cap recruit requests BEFORE they are queued — no wasted gold,
//   unlike spawn-then-delete. Pattern adapted from Serpens66's UnitLimit mod.
//
// GAME SIGNATURE (verified against Assembly-CSharp via Mono.Cecil):
//   static int Enums-less: int EngineInterface.GameAction(Enums.GameActionCommand command,
//                                                          int structureID, int state, int value2)
//   For MakeTroop (Enums.GameActionCommand.MakeTroop == 1007):
//     structureID = requested amount (vanilla passes 1, 5 with Shift, 1000 with Ctrl),
//     state       = troop type, numerically equal to SHCDESE.Interop.eChimps,
//     value2      = unused here, forwarded unchanged.
//   Return value: forwarded from the original; we return 0 to block.
//
// NAMESPACE NOTE: EngineInterface and Enums.GameActionCommand are GLOBAL-namespace types in
//   Assembly-CSharp. SHCDESE.Interop.Enums is a *namespace*, so we reference the game types with
//   the global:: prefix to avoid the clash and only `using SHCDESE.Interop` for eChimps.
//
using System;
using System.Collections.Generic;
using System.Reflection;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.Core; // StableTracking
using MonoMod.RuntimeDetour;
using SHCDESE.Interop; // eChimps

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    internal static class MakeTroopRecruitHook
    {
        private delegate int GameActionDelegate(global::Enums.GameActionCommand command, int structureID, int state, int value2);

        private static Hook _hook;
        private static GameActionDelegate _trampoline;
        private static Dictionary<eChimps, int> _caps;

        // Time-expiring reservations. A recruited soldier does not appear in the live unit count
        // until the peasant has walked to the barracks and transformed (seconds later), so without
        // this, rapid recruit clicks would each read the old (low) live count and blow past the cap.
        // Each approved recruit reserves a slot that expires. SHCDE-SE 1.35+ exposes
        // OnUnitTransition, but that event fires during the later peasant transformation and cannot
        // cancel or trim the paid recruit command. Expiry frees cancelled recruits and self-corrects
        // once the finished soldier appears in the live total.
        private static readonly Dictionary<eChimps, List<DateTime>> _pending = new Dictionary<eChimps, List<DateTime>>();
        private static readonly TimeSpan PendingLifetime = TimeSpan.FromSeconds(15);

        // Same idea for stable horses: an approved horse-requiring hire occupies its slot only once
        // the recruit has transformed and StableTracking linked it, so reservations bridge the gap.
        // One shared list - a horse is a horse regardless of which unit type takes it.
        private static readonly List<DateTime> _pendingHorses = new List<DateTime>();

        /// <summary>
        /// Install the MakeTroop detour once. Safe to call at plugin init: this only patches a
        /// static method's entry, it does not read or write game state (the detour BODY runs only
        /// when the game later calls GameAction, in-session). <paramref name="caps"/> is the same
        /// dictionary UnitCapHandler holds, read live at recruit time.
        /// </summary>
        internal static void Install(Dictionary<eChimps, int> caps)
        {
            if (_hook != null) return; // GameAction is static; one detour persists for the process
            _caps = caps;

            try
            {
                MethodInfo gameAction = typeof(global::EngineInterface).GetMethod(
                    "GameAction",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(global::Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                    null);

                if (gameAction == null)
                {
                    Plugin.Logger.LogError("[UnitCaps] EngineInterface.GameAction(GameActionCommand,int,int,int) not found — recruit caps disabled.");
                    return;
                }

                _hook = new Hook(gameAction, (GameActionDelegate)Detour);
                _trampoline = _hook.GenerateTrampoline<GameActionDelegate>();
                Plugin.Logger.LogInfo("[UnitCaps] MakeTroop recruit-cap hook installed.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[UnitCaps] Failed to install MakeTroop recruit-cap hook: {ex.Message}");
                _hook = null;
                _trampoline = null;
            }
        }

        private static int Detour(global::Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            GameActionDelegate tramp = _trampoline;
            if (tramp == null) return 0; // only possible during the install microsecond (no gameplay yet)

            if (command != global::Enums.GameActionCommand.MakeTroop)
                return tramp(command, structureID, state, value2);

            int amount = structureID > 0 ? structureID : 1;

            try
            {
                bool dbg = BepInExConfigManager.DebugLogging?.Value ?? false;
                eChimps unitType = (eChimps)state;

                // Gate 1: only types the user actually capped (>= 0); everything else is unlimited.
                // Gate 2: RequiresHorse = true on a unit whose horse cost the game does not enforce.
                int cap = 0;
                bool capped = _caps != null && _caps.TryGetValue(unitType, out cap);
                bool horseGated = StableTracking.ModEnforcesHorseCost(unitType);
                if (!capped && !horseGated)
                    return tramp(command, structureID, state, value2);

                int localId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                if (localId <= 0)
                    return tramp(command, structureID, state, value2);

                // amount >= 1000 is the Ctrl "as many as possible" request: grant up to the tightest gate.
                bool recruitMax = amount >= 1000;
                int allowed = recruitMax ? int.MaxValue : amount;
                string detail = "";

                if (capped)
                {
                    PrunePending(unitType);
                    int live = UnitCapHandler.CountPlayerUnitsOfType(localId, unitType);
                    int pending = PendingCount(unitType);
                    allowed = Math.Min(allowed, cap - (live + pending));
                    detail += $" live={live} pending={pending} cap={cap}";
                }

                if (horseGated)
                {
                    PruneHorsePending();
                    int freeHorses = StableTracking.CountFreeSlots(localId) - _pendingHorses.Count;
                    allowed = Math.Min(allowed, freeHorses);
                    detail += $" freeHorses={freeHorses}";
                }

                if (allowed <= 0)
                {
                    // Cap reached (cap == 0 means disabled, always blocked) or no free stable horse.
                    if (dbg)
                        Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop blocked {unitType}: requested {amount};{detail}.");
                    return 0;
                }
                if (allowed == int.MaxValue) allowed = amount; // unreachable in practice: a gate always bounds it

                if (capped) Reserve(unitType, allowed);
                if (horseGated) ReserveHorses(allowed);

                if (allowed != amount)
                {
                    if (dbg)
                        Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop trimmed {unitType}: requested {amount} -> {allowed};{detail}.");
                    return tramp(command, allowed, state, value2);
                }

                if (dbg)
                    Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop allowed {unitType}: {allowed};{detail}.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[UnitCaps] MakeTroop hook error: {ex.Message}");
            }

            return tramp(command, structureID, state, value2);
        }

        private static int PendingCount(eChimps unitType)
            => _pending.TryGetValue(unitType, out var list) ? list.Count : 0;

        private static void Reserve(eChimps unitType, int amount)
        {
            if (amount <= 0) return;
            if (!_pending.TryGetValue(unitType, out var list))
            {
                list = new List<DateTime>();
                _pending[unitType] = list;
            }
            DateTime expiry = DateTime.UtcNow + PendingLifetime;
            for (int i = 0; i < amount; i++) list.Add(expiry);
        }

        private static void PrunePending(eChimps unitType)
        {
            if (!_pending.TryGetValue(unitType, out var list)) return;
            DateTime now = DateTime.UtcNow;
            list.RemoveAll(t => t <= now);
            if (list.Count == 0) _pending.Remove(unitType);
        }

        private static void ReserveHorses(int amount)
        {
            DateTime expiry = DateTime.UtcNow + PendingLifetime;
            for (int i = 0; i < amount; i++) _pendingHorses.Add(expiry);
        }

        private static void PruneHorsePending()
        {
            DateTime now = DateTime.UtcNow;
            _pendingHorses.RemoveAll(t => t <= now);
        }
    }
}
