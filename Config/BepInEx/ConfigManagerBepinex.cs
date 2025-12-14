using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDETweaker.Data;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker
{
    internal static class ConfigManagerBepinex
    {
        internal static ConfigFile Config { get; private set; }

        internal static ConfigEntry<float> UnitMeleeDamageTakenMultiplier { get; private set; }
        internal static ConfigEntry<float> StructureDamageTakenMultiplier { get; private set; }
        internal static ConfigEntry<float> WallDamageTakenMultiplier { get; private set; }
        internal static ConfigEntry<float> TowerDamageTakenMultiplier { get; private set; }
        internal static ConfigEntry<float> CivilStructureDamageTakenMultiplier { get; private set; }

        internal static ConfigEntry<float> UnitHealthMultiplier { get; private set; }

        internal static ConfigEntry<float> LowWallCostMultiplier { get; private set; }
        internal static ConfigEntry<float> HighWallCostMultiplier { get; private set; }

        internal static ConfigEntry<float> UnitRangedDamageTakenMultiplier { get; private set; }

        internal static void Initialize(ConfigFile config)
        {
            Config = config;

            UnitMeleeDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitMeleeDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all melee damage taken by units"
            );

            StructureDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "StructureDamageTakenMultiplier",
                1.0f,
                "Global multiplier for damage taken by all structures"
            );

            WallDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "WallDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by walls (stone, crenel walls)"
            );

            TowerDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "TowerDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by towers (tower levels 1-5)"
            );

            CivilStructureDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "CivilStructureDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by civilian structures (non-towers, non-gatehouses)"
            );

            UnitHealthMultiplier = config.Bind(
               "Multipliers",
               "UnitHealthMultiplier",
               1.0f,
               "Global multiplier for unit max health"
           );

            // Read current values from API to use as defaults (game defaults: Low=0.25, High=0.5)
            float currentLowWallMultiplier = 0.25f;
            float currentHighWallMultiplier = 0.5f;
            try
            {
                currentLowWallMultiplier = Plugin.BuildingApi.GetLowWallCostMultiplier();
                currentHighWallMultiplier = Plugin.BuildingApi.GetHighWallCostMultiplier();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Could not read current wall cost multipliers from API, using defaults: {ex.Message}");
            }

            LowWallCostMultiplier = config.Bind(
                "Multipliers",
                "LowWallCostMultiplier",
                currentLowWallMultiplier,
                "Cost multiplier for low/short walls (stone walls at low height, stairs). Game default: 0.25"
            );

            HighWallCostMultiplier = config.Bind(
                "Multipliers",
                "HighWallCostMultiplier",
                currentHighWallMultiplier,
                "Cost multiplier for high walls (stone walls at high height, crenel walls). Game default: 0.5"
            );

            UnitRangedDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitRangedDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all ranged damage taken by units. Affects all projectile types (Arrow, Bolt, Slinger, Javelin). Base projectile damage is 2500."
            );

            // Validate all multipliers after binding
            ValidateMultipliers();

            // Apply wall cost multipliers
            ApplyWallCostMultipliers();
        }

        /// <summary>
        /// Validates all multiplier values and logs warnings for invalid values.
        /// Multipliers should be >= 0 (negative values would invert damage/healing).
        /// </summary>
        private static void ValidateMultipliers()
        {
            ValidateMultiplier("UnitMeleeDamageTakenMultiplier", UnitMeleeDamageTakenMultiplier.Value);
            ValidateMultiplier("StructureDamageTakenMultiplier", StructureDamageTakenMultiplier.Value);
            ValidateMultiplier("WallDamageTakenMultiplier", WallDamageTakenMultiplier.Value);
            ValidateMultiplier("TowerDamageTakenMultiplier", TowerDamageTakenMultiplier.Value);
            ValidateMultiplier("CivilStructureDamageTakenMultiplier", CivilStructureDamageTakenMultiplier.Value);
            ValidateMultiplier("UnitHealthMultiplier", UnitHealthMultiplier.Value);
            ValidateMultiplier("LowWallCostMultiplier", LowWallCostMultiplier.Value);
            ValidateMultiplier("HighWallCostMultiplier", HighWallCostMultiplier.Value);
            ValidateMultiplier("UnitRangedDamageTakenMultiplier", UnitRangedDamageTakenMultiplier.Value);
        }

        /// <summary>
        /// Validates a single multiplier value and logs a warning if invalid.
        /// </summary>
        private static void ValidateMultiplier(string name, float value)
        {
            if (value < 0.0f)
            {
                Plugin.Logger.LogWarning($"{name} is negative ({value}). Negative multipliers may cause unexpected behavior. Consider using a positive value.");
            }
            else if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Plugin.Logger.LogError($"{name} is invalid ({value}). Using default value of 1.0.");
            }
        }

        /// <summary>
        /// Applies wall cost multipliers from BepInEx config to the game API.
        /// Wall costs are based on height - low walls use LowWallCostMultiplier, high walls use HighWallCostMultiplier.
        /// </summary>
        private static void ApplyWallCostMultipliers()
        {
            try
            {
                Plugin.BuildingApi.SetLowWallCostMultiplier(LowWallCostMultiplier.Value);
                Plugin.BuildingApi.SetHighWallCostMultiplier(HighWallCostMultiplier.Value);
                Plugin.Logger.LogInfo($"Applied wall cost multipliers: Low={LowWallCostMultiplier.Value}, High={HighWallCostMultiplier.Value}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to apply wall cost multipliers: {ex.Message}");
            }
        }

        internal static void ApplyAllMultiplierConfigs()
        {
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        // Get building ID from tile ID
                        ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                        if (buildingId == 0)
                            return; // No building on this tile

                        eStructs structureType = Plugin.BuildingApi.GetType(buildingId);

                        // Skip non-modifiable structures
                        if (Data.StructureCategories.NonModable.Contains(structureType))
                            return;

                        float damageMultiplier = 1.0f;

                        // Apply specific multipliers first (most specific to least specific)
                        if (Data.StructureCategories.IsWall(structureType))
                        {
                            damageMultiplier *= WallDamageTakenMultiplier.Value;
                        }
                        else if (Data.StructureCategories.IsTower(structureType))
                        {
                            damageMultiplier *= TowerDamageTakenMultiplier.Value;
                        }

                        if (Data.StructureCategories.IsCivilStructure(structureType))
                        {
                            damageMultiplier *= CivilStructureDamageTakenMultiplier.Value;
                        }

                        // Apply general structure multiplier last
                        damageMultiplier *= StructureDamageTakenMultiplier.Value;

                        // Only apply if multiplier is not 1.0
                        if (!Mathf.Approximately(damageMultiplier, 1.0f))
                        {
                            var modified = (int)Mathf.Clamp((float)args.Damage * damageMultiplier, 1, int.MaxValue);
                            args.Damage = modified;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply structure damage multiplier: {ex.Message}");
                    }
                });

            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        if (Mathf.Approximately(UnitMeleeDamageTakenMultiplier.Value, 1.0f))
                            return;

                        eChimps attacker = Plugin.UnitApi.GetType(args.AttackingUnitId);
                        eChimps defender = Plugin.UnitApi.GetType(args.DamagedUnitId);

                        int baseDamage = args.Damage > 0
                            ? args.Damage
                            : Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);

                        int modified = Mathf.Max(1, (int)(baseDamage * UnitMeleeDamageTakenMultiplier.Value));
                        args.Damage = modified;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply unit melee damage multiplier: {ex.Message}");
                    }
                });

            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    if (Mathf.Approximately(UnitHealthMultiplier.Value, 1.0f))
                        return; // Skip if multiplier is 1.0

                    eChimps unitType = args.UnitType;

                    // Skip non-modifiable units
                    if (Data.UnitCategories.NonModable.Contains(unitType))
                        return;

                    try
                    {
                        int unitId = (int)args.ReturnValue; // ReturnValue is long, cast to int

                        // Get current health and apply multiplier
                        int currentMaxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
                        int newMaxHealth = Mathf.Max(1, (int)(currentMaxHealth * UnitHealthMultiplier.Value));

                        Plugin.UnitApi.SetMaxHealth(unitId, newMaxHealth);
                        Plugin.UnitApi.SetCurrentHealth(unitId, newMaxHealth);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply health multiplier to {unitType}: {ex.Message}");
                    }
                });

            // Hook for ranged damage multiplier (using Ex version which allows damage modification)
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        if (Mathf.Approximately(UnitRangedDamageTakenMultiplier.Value, 1.0f))
                            return;

                        // Apply multiplier to projectile damage
                        int modified = Mathf.Max(1, (int)(args.Damage * UnitRangedDamageTakenMultiplier.Value));
                        args.Damage = modified;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply ranged damage multiplier: {ex.Message}");
                    }
                });
        }
    }
}
