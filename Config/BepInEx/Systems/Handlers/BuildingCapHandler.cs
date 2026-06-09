// Config/BepInEx/Systems/Handlers/BuildingCapHandler.cs
//
// PURPOSE: Enforces per-building-type count caps loaded from the Structures TOML.
//
// MECHANISM: Blocks placement BEFORE the building is built, via OnPlacementValidation (Pre).
//   The previous implementation hooked OnBuildingSpawn (Post) and DeleteBuildingSafe'd the
//   building after it already existed — which cost the player resources and flickered the
//   building in/out. It also risked bulldozing over-cap buildings when loading a save (those
//   re-spawn). Placement-blocking avoids all of that: an over-cap placement is simply refused,
//   exactly like the game's own footprint/terrain rules, and existing buildings are left alone.
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.BepInEx;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;   // eMappers.ConvertToEStructs()
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    internal static class BuildingCapHandler
    {
        public static void Subscribe(Dictionary<eStructs, int> caps)
        {
            // NOTE: do NOT early-return on caps.Count == 0. This Subscribe() runs during
            // GlobalConfigSystem.Load(), which fires BEFORE StructureConfigSystem.Load() populates
            // `caps` (LoadBuildingCaps). The dictionary is passed by reference and mutated in place,
            // so we register the subscription unconditionally and read it live at placement time.
            if (caps == null) return;

            Plugin.Logger.LogInfo("[BuildingCaps] Subscribing to OnPlacementValidation for building cap enforcement...");

            BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging?.Value ?? false;

                    try
                    {
                        // The placement event speaks eMappers; the cap dict is keyed by eStructs.
                        // ConvertToEStructs is a 1:1 mirror of the game's own converter; non-building
                        // mappers (terrain ops, etc.) fold to STRUCT_NULL and are simply not in the dict.
                        eStructs buildingType = args.Mappers.ConvertToEStructs();

                        // Only tracked types are in the dict. cap == 0 means disabled (block ALL
                        // placements); cap > 0 means allow at most `cap`. Negatives (unlimited) are
                        // never tracked, so an absent key = no limit.
                        if (!caps.TryGetValue(buildingType, out var cap))
                            return;

                        // Cap only the local human player (matches prior behaviour; AI unaffected).
                        int localId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                        if (args.PlayerId != localId)
                            return;

                        // At Pre-placement the new building does not exist yet, so `count` is the
                        // number already standing. Block once placing one more would exceed the cap:
                        //   cap == 0 -> count >= 0 -> always blocked (disabled)
                        //   cap == N -> block the (N+1)th placement
                        int count = CountPlayerBuildingsOfType(args.PlayerId, buildingType);
                        if (count >= cap)
                        {
                            args.CustomValidationRules = true;
                            args.ForceBlockPlacementState = true;

                            if (dbg)
                            {
                                string reason = cap == 0 ? "disabled" : $"cap reached ({cap})";
                                Plugin.Logger.LogInfo(
                                    $"[BuildingCaps] Blocked {buildingType} placement — {reason} (have {count}).");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"[BuildingCaps] Error validating placement: {ex.Message}");
                    }
                });

            Plugin.Logger.LogInfo("[BuildingCaps] Subscribed — caps read live at placement time.");
        }

        private static readonly List<int> _reusableBuildingList = new List<int>();

        private static int CountPlayerBuildingsOfType(int playerId, eStructs buildingType)
        {
            _reusableBuildingList.Clear();
            Plugin.BuildingApi?.GetAllBuildings(_reusableBuildingList, null, buildingType);
            int count = 0;
            for (int i = 0; i < _reusableBuildingList.Count; i++)
            {
                if (Plugin.BuildingApi.GetOwner(_reusableBuildingList[i]) == playerId)
                    count++;
            }
            return count;
        }
    }
}
