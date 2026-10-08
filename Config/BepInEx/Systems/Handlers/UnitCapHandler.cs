using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.BepInEx;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums; // AliveState, PlayerRelationship

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    internal static class UnitCapHandler
    {
        public static void Subscribe(Dictionary<eChimps, int> caps)
        {
            // NOTE: do NOT early-return on caps.Count == 0. This Subscribe() runs during
            // GlobalConfigSystem.Load(), which fires BEFORE UnitConfigSystem.Load() populates
            // `caps` (LoadUnitCaps). The dictionary is passed by reference and mutated in place,
            // so we register the subscription unconditionally and read it live at spawn time.
            if (caps == null) return;

            Plugin.Logger.LogInfo("[UnitCaps] Subscribing to OnUnitCreate for unit cap enforcement...");

            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging?.Value ?? false;
                    eChimps unitType = args.UnitType;

                    // Only tracked types are in the dict. cap == 0 means disabled (remove every
                    // spawn); cap > 0 means keep at most `cap`. Negatives (unlimited) are never tracked.
                    if (!caps.TryGetValue(unitType, out var cap))
                        return;

                    int unitId = (int)args.ReturnValue;
                    int owner = args.PlayerOwnerId;

                    try
                    {
                        int localId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                        if (owner != localId)
                            return;

                        if (Plugin.PlayerApi?.IsAIPlayer(owner) ?? false)
                            return;

                        // Includes the new unit (still NeedsInit here), so count > cap keeps at most `cap`.
                        int count = CountPlayerUnitsOfType(owner, unitType);

                        if (dbg)
                            Plugin.Logger.LogInfo($"[UnitCaps] {unitType}: count={count}, cap={cap}");

                        if (count > cap)
                        {
                            bool deleted = Plugin.UnitApi.DeleteUnitSafe(unitId);
                            string reason = cap == 0 ? "disabled" : $"cap reached ({cap})";
                            Plugin.Logger.LogWarning(
                                $"[UnitCaps] {unitType} {reason}. " +
                                $"Removed unit id={unitId} (success={deleted}).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"[UnitCaps] Error enforcing cap for {unitType}: {ex.Message}");
                    }
                });

            // Recruited units (Barracks / Mercenary Post) do NOT fire OnUnitCreate — recruiting
            // transforms a peasant into a soldier rather than spawning a fresh unit, so the
            // subscription above never catches them. Enforce the cap at recruit time instead by
            // hooking EngineInterface.GameAction(MakeTroop). The OnUnitCreate path above is kept as
            // belt-and-suspenders for the spawns that DO fire it (e.g. keep-spawned units).
            MakeTroopRecruitHook.Install(caps);

            Plugin.Logger.LogInfo("[UnitCaps] Subscribed — caps read live at spawn + recruit time.");
        }

        // internal so MakeTroopRecruitHook can reuse the exact same count semantics at recruit time.
        //
        // BUGFIX (unreleased, found with the GitLab #5 self-test): units still initialising count too. A new unit is
        // AliveState.NeedsInit during its own OnUnitCreate event and for the rest of that tick (measured in game,
        // selftest-20261008-105719: "NeedsInit at creation" for every spawned Knight). Counting IsAlive only left
        // the new unit out, so `count > cap` let spawned units reach MaxCount + 1 (Knight cap 2: three spawned
        // Knights all stayed), and units spawned in the same tick did not count each other at all.
        // MarkedForDeletion (removed by this handler or dying) and units killed by a projectile are not counted.
        internal static unsafe int CountPlayerUnitsOfType(int playerId, eChimps unitType)
        {
            var api = Plugin.UnitApi;
            var ids = new List<int>();
            api.GetAllUnits(ids, null, unitType, PlayerRelationship.Self, playerId);
            int count = 0;
            foreach (int id in ids)
            {
                if (!api.TryGetUnitById(id, out GameUnit* u) || u == null) continue;
                if ((u->r_AliveState == AliveState.IsAlive || u->r_AliveState == AliveState.NeedsInit) && u->r_IsKilledByProjectile == 0)
                    count++;
            }
            return count;
        }
    }
}
