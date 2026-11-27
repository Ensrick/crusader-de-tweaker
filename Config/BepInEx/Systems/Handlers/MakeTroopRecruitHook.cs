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
// 2.11.12 (GitLab #17, plan FWP-U-20 / P6-01): when APIShared (Serpens66) with its GameActionEvents is loaded, the gate
//   runs as a registration there (APISharedGameActionAdapter) instead of this file's own MonoMod hook, so the two mods
//   share one hook on EngineInterface.GameAction. Both paths use TryDecide / AllowedAmount and decide identically.
//   CFG [Compatibility] GameActionHook = Own forces the own hook (kept for two releases).
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
        private static bool _activated;

        /// <summary>Which GameAction interception carries the recruit gate (log / self-test).</summary>
        internal static string ActivePath { get; private set; } = "none";

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

        // Guards _pending/_pendingHorses so read-count -> check -> reserve is atomic. Every access is
        // on whatever thread calls EngineInterface.GameAction. The recruit buttons call it from Unity's
        // main thread; no second caller is known, but UnitTransitionDispatcher shows SE events can arrive
        // off-thread, and an uncontended lock costs nothing on a click path. The game's GameAction (the
        // trampoline, or APIShared's original) is always called OUTSIDE the lock.
        private static readonly object _pendingLock = new object();

        /// <summary>
        /// The recruit gate's decision for one MakeTroop request (2.11.12: shared by the mod's own hook and the
        /// APIShared GameActionEvents adapter, so both paths decide identically).
        /// </summary>
        internal struct RecruitDecision
        {
            public eChimps Unit;
            public bool Capped;
            public bool HorseGated;
            /// <summary>The request as the gate reads it: the first payload, 1 when it is 0 or less.</summary>
            public int Requested;
            /// <summary>What may be recruited: 0 or less = refuse, Requested = unchanged, else trim to this.</summary>
            public int Allowed;
            public string Detail;
        }

        /// <summary>
        /// Remembers the caps table, which UnitCapHandler holds and the gate reads live at recruit time. The
        /// interception itself is chosen and installed by <see cref="Activate"/> once the CFG is bound.
        /// </summary>
        internal static void Install(Dictionary<eChimps, int> caps)
        {
            _caps = caps;
        }

        /// <summary>
        /// 2.11.12 (GitLab #17, plan FWP-U-20): choose the GameAction interception once, after the CFG is bound.
        /// "Auto" (default): APIShared's shared GameActionEvents when an APIShared with that API is loaded
        /// (detected by its members, not its version), else the mod's own hook. "Own": always the mod's own hook
        /// (the behaviour of 2.11.11 and earlier; kept for two releases). Never both.
        /// </summary>
        internal static void Activate(string mode)
        {
            if (_activated) return;
            _activated = true;

            if (!string.Equals(mode, "Own", StringComparison.OrdinalIgnoreCase))
            {
                if (APISharedGameActionAdapter.TryRegister(out string reason))
                {
                    ActivePath = "APIShared";
                    Plugin.Logger.LogInfo($"[UnitCaps] Recruit gate registered with APIShared's shared GameAction events ({reason}); the mod's own GameAction hook is not installed.");
                    return;
                }
                Plugin.Logger.LogInfo($"[UnitCaps] APIShared GameAction events not used ({reason}); installing the mod's own GameAction hook.");
            }
            else
            {
                Plugin.Logger.LogInfo("[UnitCaps] [Compatibility] GameActionHook = Own: installing the mod's own GameAction hook.");
            }
            InstallOwnHook();
        }

        private static void InstallOwnHook()
        {
            if (_hook != null) return; // GameAction is static; one detour persists for the process

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
                ActivePath = "own hook";
                Plugin.Logger.LogInfo("[UnitCaps] MakeTroop recruit-cap hook installed.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[UnitCaps] Failed to install MakeTroop recruit-cap hook: {ex.Message}");
                _hook = null;
                _trampoline = null;
            }
        }

        /// <summary>
        /// Pure: how many of <paramref name="amount"/> may be recruited (the pre-2.11.12 arithmetic, unchanged).
        /// amount >= 1000 is the Ctrl "as many as possible" request: grant up to the tightest gate.
        /// </summary>
        internal static int AllowedAmount(int amount, bool capped, int cap, int live, int pending, bool horseGated, int freeHorses)
        {
            bool recruitMax = amount >= 1000;
            int allowed = recruitMax ? int.MaxValue : amount;
            if (capped) allowed = Math.Min(allowed, cap - (live + pending));
            if (horseGated) allowed = Math.Min(allowed, freeHorses);
            if (allowed == int.MaxValue) allowed = amount; // unreachable in practice: a gate always bounds it
            return allowed;
        }

        /// <summary>
        /// The gate for one MakeTroop request. False = not gated (pass the request on unchanged). With
        /// <paramref name="reserve"/> the approved amount is reserved under the same lock (own hook); the
        /// APIShared path reserves later, in Accepted, with the final inputs (<see cref="ReserveAccepted"/>).
        /// </summary>
        internal static bool TryDecide(int structureID, int state, bool reserve, out RecruitDecision decision)
        {
            decision = default;
            int amount = structureID > 0 ? structureID : 1;
            eChimps unitType = (eChimps)state;

            // Gate 1: only types the user actually capped (>= 0); everything else is unlimited.
            // Gate 2: RequiresHorse = true on a unit whose horse cost the game does not enforce.
            int cap = 0;
            bool capped = _caps != null && _caps.TryGetValue(unitType, out cap);
            bool horseGated = StableTracking.ModEnforcesHorseCost(unitType);
            if (!capped && !horseGated)
                return false;

            int localId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
            if (localId <= 0)
                return false;

            string detail = "";
            lock (_pendingLock)
            {
                int live = 0, pending = 0, freeHorses = 0;
                if (capped)
                {
                    PrunePending(unitType);
                    live = UnitCapHandler.CountPlayerUnitsOfType(localId, unitType);
                    pending = PendingCount(unitType);
                    detail += $" live={live} pending={pending} cap={cap}";
                }

                if (horseGated)
                {
                    PruneHorsePending();
                    freeHorses = StableTracking.CountFreeSlots(localId) - _pendingHorses.Count;
                    detail += $" freeHorses={freeHorses}";
                }

                int allowed = AllowedAmount(amount, capped, cap, live, pending, horseGated, freeHorses);
                if (reserve && allowed > 0)
                {
                    if (capped) Reserve(unitType, allowed);
                    if (horseGated) ReserveHorses(allowed);
                }

                decision = new RecruitDecision
                {
                    Unit = unitType, Capped = capped, HorseGated = horseGated,
                    Requested = amount, Allowed = allowed, Detail = detail
                };
            }
            return true;
        }

        /// <summary>APIShared path: reserve what an accepted (unvetoed) request will recruit.</summary>
        internal static void ReserveAccepted(RecruitDecision decision, int amount)
        {
            if (amount <= 0) return;
            lock (_pendingLock)
            {
                if (decision.Capped) Reserve(decision.Unit, amount);
                if (decision.HorseGated) ReserveHorses(amount);
            }
        }

        /// <summary>The same debug lines for both paths.</summary>
        internal static void LogDecision(RecruitDecision d)
        {
            if (!(BepInExConfigManager.DebugLogging?.Value ?? false)) return;
            if (d.Allowed <= 0)
                Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop blocked {d.Unit}: requested {d.Requested};{d.Detail}.");
            else if (d.Allowed != d.Requested)
                Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop trimmed {d.Unit}: requested {d.Requested} -> {d.Allowed};{d.Detail}.");
            else
                Plugin.Logger.LogInfo($"[UnitCaps] MakeTroop allowed {d.Unit}: {d.Allowed};{d.Detail}.");
        }

        private static int Detour(global::Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            GameActionDelegate tramp = _trampoline;
            if (tramp == null) return 0; // only possible during the install microsecond (no gameplay yet)

            if (command != global::Enums.GameActionCommand.MakeTroop)
                return tramp(command, structureID, state, value2);

            try
            {
                if (!TryDecide(structureID, state, reserve: true, out RecruitDecision d))
                    return tramp(command, structureID, state, value2);

                LogDecision(d);

                // Cap reached (cap == 0 means disabled, always blocked) or no free stable horse.
                if (d.Allowed <= 0)
                    return 0;

                if (d.Allowed != d.Requested)
                    return tramp(command, d.Allowed, state, value2);
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
