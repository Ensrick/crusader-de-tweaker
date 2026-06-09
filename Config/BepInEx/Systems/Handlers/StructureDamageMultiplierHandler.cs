// Config/BepInEx/Systems/Handlers/StructureDamageMultiplierHandler.cs
// AI DEV: Handles OnBuildingTileTakeDamage (Pre phase) to apply all structure damage multipliers.
// BuildingTileTakeDamageEventArgs has no AttackingUnitId property.
// Use Plugin.UnitApi.GetCurrentContextAttackingUnitId() (SHCDESE v1.21.1+) to get the attacker unit ID.
// This reads gContextAttackingUnitIdVA - a game-global set during c_game_buildingtile_take_damage.
// NOTE: args.Unknown1 (native param a6) is NOT the attacker unit ID - confirmed by SDK developer.
// Demolisher/Sapper identification: GetCurrentContextAttackingUnitId() > 0 → GetType() → eChimps enum match.
// Demolisher/Sapper multipliers are applied first, then structure-type, then global.
// IMPORTANT: args.TileId IS a valid StructureGrid index - pass it directly (as int) to GetTileBuildingId/GetTilePropertyFlag.
// The original bug was casting args.TileId to ushort before passing it, which silently truncated tile IDs > 65,535.
// On an 800x800 map (up to 640,000 tiles) this corrupted ~90% of lookups, always returning buildingId=0.
// DEBUG: Set BepInExConfigManager.DebugLogging = true to enable step-by-step logging.
//   [DBG-BldgDmg-RAW]: Fires for EVERY call to OnBuildingTileTakeDamage (Pre AND Post), no filter.
//                       If this never appears, the SHCDESE hook itself is not triggering.
//   [DBG-BldgDmg-N]:   Step-by-step inside the Pre-filtered subscription.
using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    /// <summary>
    /// Handles structure damage multiplier application via event hooks.
    /// Extracted from StructureMultipliersConfig to reduce class size.
    /// </summary>
    internal static class StructureDamageMultiplierHandler
    {
        /// <summary>
        /// Subscribes to building tile damage event hook to apply structure damage multipliers in real-time.
        /// </summary>
        public static void Subscribe(
            ConfigEntry<float> globalMultiplier,
            ConfigEntry<float> wallMultiplier,
            ConfigEntry<float> towerMultiplier,
            ConfigEntry<float> civilStructureMultiplier,
            ConfigEntry<float> demolisherMultiplier,
            ConfigEntry<float> sapperMultiplier)
        {
            Plugin.Logger.LogInfo("Subscribing to OnBuildingTileTakeDamage event hook...");

            // RAW diagnostic subscription: no phase filter, fires for every event call.
            // If [DBG-BldgDmg-RAW] never appears in the log after attacking structures,
            // the underlying SHCDESE hook (c_game_buildingtile_take_melee_damage) is not firing.
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Subscribe(args =>
                {
                    if (BepInExConfigManager.DebugLogging.Value)
                        Plugin.Logger.LogInfo(
                            $"[DBG-BldgDmg-RAW] phase={args.Phase} tile={args.TileId} dmg={args.Damage} unknown1={args.Unknown1} playerSrc={args.PlayerIdSource}");
                });

            // Main subscription: Pre phase only, applies all structure damage multipliers.
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    bool dbg = BepInExConfigManager.DebugLogging.Value;
                    try
                    {
                        if (dbg)
                            Plugin.Logger.LogInfo(
                                $"[DBG-BldgDmg-1] Pre received: tile={args.TileId} tileX={args.TileX} tileY={args.TileY} dmg={args.Damage} playerSrc={args.PlayerIdSource}");

                        // args.TileId is already a valid StructureGrid index — pass as int directly.
                        // Do NOT cast to ushort: tile IDs on an 800x800 map exceed 65,535 and will silently truncate.
                        ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-BldgDmg-2] tileId={args.TileId} buildingId={buildingId}");

                        // Detect structure type
                        var typeInfo = StructureTypeDetector.Detect(args.TileId, buildingId);

                        if (dbg)
                            Plugin.Logger.LogInfo(
                                $"[DBG-BldgDmg-3] typeInfo={(typeInfo.HasValue ? $"structType={typeInfo.Value.StructureType} isWall={typeInfo.Value.IsWall} isTower={typeInfo.Value.IsTower} isGatehouse={typeInfo.Value.IsGatehouse} isCivil={typeInfo.Value.IsCivilStructure}" : "null (skipped)")}");

                        if (typeInfo == null)
                            return;

                        float damageMultiplier = 1.0f;
                        int originalDamage = args.Damage;
                        bool isSapperOrDemolisher = false;

                        // Demolisher/Sapper detection via context-based unit ID (SHCDESE v1.21.1+).
                        // NOTE: args.Unknown1 (native a6) is NOT the attacker unit ID.
                        // GetCurrentContextUnitId() reads gContextAttackingUnitIdVA (renamed from
                        // GetCurrentContextAttackingUnitId in SHCDESE v1.28), a game-global set
                        // during c_game_buildingtile_take_damage.
                        // Applied first so they stack correctly with structure-type and global multipliers.
                        int attackingUnitId = Plugin.UnitApi.GetCurrentContextUnitId();

                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-BldgDmg-4] attackingUnitId={attackingUnitId}");

                        if (attackingUnitId > 0)
                        {
                            eChimps attackerType = Plugin.UnitApi.GetType(attackingUnitId);

                            if (dbg)
                                Plugin.Logger.LogInfo($"[DBG-BldgDmg-5] attackerType={attackerType}");

                            if (attackerType == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)
                            {
                                Plugin.Logger.LogInfo($"[StructureDmgMult] Demolisher (unit {attackingUnitId}) hit tile {args.TileId}. Damage before hook: {originalDamage}, DemolisherMult: {demolisherMultiplier.Value}x");
                                damageMultiplier *= demolisherMultiplier.Value;
                                isSapperOrDemolisher = true;
                            }
                            else if (attackerType == eChimps.CHIMP_TYPE_BEDOUIN_SAPPER)
                            {
                                Plugin.Logger.LogInfo($"[StructureDmgMult] Sapper (unit {attackingUnitId}) hit tile {args.TileId}. Damage before hook: {originalDamage}, SapperMult: {sapperMultiplier.Value}x");
                                damageMultiplier *= sapperMultiplier.Value;
                                isSapperOrDemolisher = true;
                            }
                        }

                        // Apply structure-type multipliers
                        if (typeInfo.HasValue)
                        {
                            if (typeInfo.Value.IsWall)
                            {
                                if (dbg)
                                    Plugin.Logger.LogInfo($"[DBG-BldgDmg-6] Applying wall multiplier: {wallMultiplier.Value}x (current total: {damageMultiplier}x)");
                                damageMultiplier *= wallMultiplier.Value;
                            }
                            else if (typeInfo.Value.IsTower || typeInfo.Value.IsGatehouse)
                            {
                                if (dbg)
                                    Plugin.Logger.LogInfo($"[DBG-BldgDmg-6] Applying tower/gatehouse multiplier: {towerMultiplier.Value}x (current total: {damageMultiplier}x)");
                                damageMultiplier *= towerMultiplier.Value;
                            }

                            if (typeInfo.Value.IsCivilStructure)
                            {
                                if (dbg)
                                    Plugin.Logger.LogInfo($"[DBG-BldgDmg-7] Applying civil structure multiplier: {civilStructureMultiplier.Value}x (current total: {damageMultiplier}x)");
                                damageMultiplier *= civilStructureMultiplier.Value;
                            }
                        }

                        // Apply general structure multiplier last
                        if (dbg)
                            Plugin.Logger.LogInfo($"[DBG-BldgDmg-8] Applying global structure multiplier: {globalMultiplier.Value}x (current total before: {damageMultiplier}x) isSapperOrDemolisher={isSapperOrDemolisher}");
                        damageMultiplier *= globalMultiplier.Value;

                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        var modified = (int)Mathf.Clamp((float)args.Damage * damageMultiplier, 1, int.MaxValue);
                        args.Damage = modified;

                        if (dbg)
                            Plugin.Logger.LogInfo($"[StructureDmgMult] tile={args.TileId} unitId={attackingUnitId} dmg {originalDamage}->{modified} (x{damageMultiplier:F4})");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to apply structure damage multiplier: {ex.Message}\n{ex.StackTrace}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnBuildingTileTakeDamage event hook");
        }
    }
}
