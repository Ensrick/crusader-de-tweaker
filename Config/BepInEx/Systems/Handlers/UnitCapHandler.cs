using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.Sync;
using R3;
using SHCDESE.API;
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
                        // 2.11.3 (FWP-E-01, GitLab #2): in a multiplayer match every PC runs this hook for
                        // every player's new unit, inside the simulation. Removing a unit (DeleteUnitSafe) changes
                        // only that PC's game, so the old "own player only" rule removed it on the owner's PC
                        // alone: a desync. Now every PC removes the same units: every human player's, and only
                        // with a cap that is the same on every PC (SharedCap). Single player: unchanged.
                        if (GameNetworkAPI.IsMultiplayerGame())
                        {
                            if (owner < 1 || owner > MaxPlayer || (Plugin.PlayerApi?.IsAIPlayer(owner) ?? true))
                                return;
                            int ownCap = cap;
                            if (!LobbyMaxCounts.TryGetSharedUnitCap(unitType, ConfigSyncManager.AllPeersUseHostFiles, out cap))
                            {
                                WarnNotShared(unitType, ownCap);
                                return;
                            }
                        }
                        else
                        {
                            int localId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                            if (owner != localId)
                                return;

                            if (Plugin.PlayerApi?.IsAIPlayer(owner) ?? false)
                                return;
                        }

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

            // The "not the same on every PC" note is logged once per unit type and match.
            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => { lock (_warnedNotShared) _warnedNotShared.Clear(); });

            Plugin.Logger.LogInfo("[UnitCaps] Subscribed — caps read live at spawn + recruit time.");
        }

        private const int MaxPlayer = 8;
        private static readonly HashSet<eChimps> _warnedNotShared = new HashSet<eChimps>();

        private static void WarnNotShared(eChimps unitType, int ownCap)
        {
            lock (_warnedNotShared)
            {
                if (!_warnedNotShared.Add(unitType)) return;
            }
            Plugin.Logger.LogInfo(
                $"[UnitCaps] {unitType}: multiplayer match, so MaxCount {ownCap} from your own Units file does not remove spawned units " +
                "(only a lobby value or the host's synced files are the same on every PC; removing a unit on one PC only would desync). " +
                "Your own recruiting and placement limits still apply.");
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
