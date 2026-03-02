// Config/BepInEx/Systems/Handlers/StructureDamageMultiplierHandler.cs
// AI DEV: Handles OnBuildingTileTakeDamage (Pre phase) to apply all structure damage multipliers.
// BuildingTileTakeDamageEventArgs has no AttackingUnitId property; the native hook signature is:
//   c_game_buildingtile_take_damage(pTileManager, tileId, tileX, tileY, damage, a6, playerIdSource, a8, a9)
// args.Unknown1 = native param a6 = attacker unit ID. Verified via ildasm IL dump.
// Demolisher/Sapper identification: Unknown1 > 0 → GetType() → eChimps enum match.
// Demolisher/Sapper multipliers are applied first, then structure-type, then global.
using System;
using BepInEx.Configuration;
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
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        // Get building ID from tile ID
                        ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId((ushort)args.TileId);

                        // Detect structure type
                        var typeInfo = StructureTypeDetector.Detect((ushort)args.TileId, buildingId);
                        if (typeInfo == null)
                            return;

                        float damageMultiplier = 1.0f;
                        int originalDamage = args.Damage;
                        bool isSapperOrDemolisher = false;

                        // Demolisher/Sapper detection: Unknown1 (native param a6) is the attacker unit ID.
                        // In the hook: c_game_buildingtile_take_damage(pTileManager, tileId, tileX, tileY,
                        //              damage, a6=attackerUnitId, playerIdSource, a8, a9)
                        // Applied first so they stack correctly with structure-type and global multipliers.
                        int attackingUnitId = args.Unknown1;
                        if (attackingUnitId > 0 && Plugin.UnitApi != null)
                        {
                            eChimps attackerType = Plugin.UnitApi.GetType(attackingUnitId);
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
                        if (typeInfo.Value.IsWall)
                        {
                            damageMultiplier *= wallMultiplier.Value;
                        }
                        else if (typeInfo.Value.IsTower || typeInfo.Value.IsGatehouse)
                        {
                            damageMultiplier *= towerMultiplier.Value;
                        }

                        if (typeInfo.Value.IsCivilStructure)
                        {
                            damageMultiplier *= civilStructureMultiplier.Value;
                        }

                        // Apply general structure multiplier last
                        damageMultiplier *= globalMultiplier.Value;

                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        var modified = (int)Mathf.Clamp((float)args.Damage * damageMultiplier, 1, int.MaxValue);
                        args.Damage = modified;

                        if (isSapperOrDemolisher)
                            Plugin.Logger.LogInfo($"[StructureDmgMult] Damage after hook: {modified} (total mult applied: {damageMultiplier:F4}x)");
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

