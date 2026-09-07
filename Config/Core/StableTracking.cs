// Config/Core/StableTracking.cs
//
// PURPOSE: Stable horse-slot bookkeeping for units with RequiresHorse = true.
//
// WHY THIS EXISTS:
//   The game enforces a horse requirement natively ONLY for the 7 European barracks units
//   (Archer .. Knight): c_game_player_buy_eu_mercenary reads the EU good-cost table, where the
//   value -1 (_SE_REQUIRE_HORSE) in the last slot means "needs a free stable horse". Arabian and
//   Bedouin mercenaries have no row in that table (SHCDE-SE exposes exactly 7 entries and its
//   array wrapper silently ignores writes outside them), so RequiresHorse = true on e.g.
//   CHIMP_TYPE_ARAB_HORSEMAN wrote nothing and the Mercenary Post hired them freely.
//
//   This class supplies what the game does not: counting free slots across ALL of the local
//   player's stables (for the recruit-time gate in MakeTroopRecruitHook) and linking a finished
//   unit to a free slot (from OnUnitCreate and the deferred OnUnitTransition path).
//
// SLOT MODEL (SHCDE-SE GameBuildingManagerAPI):
//   Each stable has 4 slots. A slot is free when its unit global-id link is <= 0. Linking is
//   bidirectional (the unit records its stable), so the game clears the slot when the unit dies,
//   exactly as for a natively hired Knight.
//
// CONSUMERS:
//   - MakeTroopRecruitHook: refuses / trims a hire when no free horse is available.
//   - ConfigLoader.RegisterSessionHooks: links spawned and recruited units to a slot.
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.Toml.Units.Properties;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums; // AliveState, PlayerRelationship

namespace CrusaderDETweaker.Config.Core
{
    internal static class StableTracking
    {
        private const int SlotsPerStable = 4;

        // Reused across calls to avoid per-spawn allocations.
        private static readonly List<int> _stableIds = new List<int>();

        /// <summary>
        /// True for the 7 European barracks units whose horse requirement the GAME enforces
        /// through the EU good-cost table (Archer, Crossbowman, Spearman, Pikeman, Maceman,
        /// Swordsman, Knight). Every other recruitable unit relies on the mod's recruit gate.
        /// </summary>
        internal static bool GameEnforcesHorseCost(eChimps unitType)
        {
            return unitType >= eChimps.CHIMP_TYPE_ARCHER && unitType <= eChimps.CHIMP_TYPE_KNIGHT;
        }

        /// <summary>
        /// True when the unit type needs a stable horse AND the game does not enforce it itself,
        /// i.e. the mod must gate the hire (Arabian / Bedouin mercenaries and other non-EU units).
        /// </summary>
        internal static bool ModEnforcesHorseCost(eChimps unitType)
        {
            return RequiresHorseProperty.HorseRequiringUnits.Contains(unitType)
                && !GameEnforcesHorseCost(unitType);
        }

        /// <summary>
        /// Number of free horse slots across every stable owned by <paramref name="playerId"/>.
        /// 0 when the player has no stable.
        /// </summary>
        internal static int CountFreeSlots(int playerId)
        {
            int free = 0;
            CollectStables(playerId);
            foreach (int stableId in _stableIds)
            {
                for (int slot = 0; slot < SlotsPerStable; slot++)
                {
                    if (Plugin.BuildingApi.GetStablesUnitGlobalIdLink(stableId, slot) <= 0)
                        free++;
                }
            }
            return free;
        }

        /// <summary>
        /// Link one horse-requiring unit of the local player to the first free slot of any of
        /// their stables. No-op for unit types without RequiresHorse = true, non-local owners,
        /// units already occupying a slot (the game links natively hired EU cavalry itself), or
        /// when no stable slot is free.
        /// </summary>
        internal static void TryLinkUnitToStable(eChimps unitType, int unitId)
        {
            try
            {
                if (!RequiresHorseProperty.HorseRequiringUnits.Contains(unitType))
                    return;

                int localPlayerId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                if (localPlayerId < 0 || Plugin.UnitApi?.GetOwner(unitId) != localPlayerId)
                    return;

                CollectStables(localPlayerId);
                if (_stableIds.Count == 0) return;

                int freeStable = -1, freeSlot = -1;
                foreach (int stableId in _stableIds)
                {
                    for (int slot = 0; slot < SlotsPerStable; slot++)
                    {
                        // Already linked (natively, or by an earlier event for the same unit): done.
                        if (Plugin.BuildingApi.GetStablesUnitIdLink(stableId, slot) == unitId)
                            return;

                        if (freeSlot < 0 && Plugin.BuildingApi.GetStablesUnitGlobalIdLink(stableId, slot) <= 0)
                        {
                            freeStable = stableId;
                            freeSlot = slot;
                        }
                    }
                }
                if (freeSlot < 0)
                {
                    Plugin.Logger.LogDebug($"[StableTracking] No free stable slot for {unitType} (id={unitId}).");
                    return;
                }

                int unitGlobalId = Plugin.UnitApi.GetGlobalId(unitId);
                if (unitGlobalId < 0) return;

                Plugin.BuildingApi.SetStablesUnitIdLink(freeStable, freeSlot, unitId, unitGlobalId);
                Plugin.Logger.LogDebug($"[StableTracking] Linked {unitType} (id={unitId}) to stable {freeStable} slot {freeSlot}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[StableTracking] Error linking unit to stable: {ex.Message}");
            }
        }

        private static void CollectStables(int playerId)
        {
            _stableIds.Clear();
            var api = Plugin.BuildingApi;
            if (api == null) return;

            // Alive stables owned by the player, filtered inside the query (same pattern as
            // BuildingCapHandler: dead / pending-deletion buildings are excluded and the owner
            // check runs on r_PlayerIdOwner directly, without a GetOwner round trip per id).
            api.GetAllBuildings(_stableIds, AliveState.IsAlive, eStructs.STRUCT_STABLES,
                PlayerRelationship.Self, playerId);
        }
    }
}
