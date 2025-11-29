using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace CrusaderDETweaker
{
    internal static class ConfigManager
    {
        internal static ConfigFile Config { get; private set; }

        internal static Dictionary<eChimps, UnitConfig> UnitConfigs { get; } = new Dictionary<eChimps, UnitConfig>();
        internal static Dictionary<eStructs, StructureConfig> StructureConfigs { get; } = new Dictionary<eStructs, StructureConfig>();

        internal static ConfigEntry<float> UnitMeleeDamageTakenMultiplier { get; private set; }
        internal static ConfigEntry<float> StructureDamageTakenMultiplier { get; private set; }

        internal static void Initialize(ConfigFile config)
        {
            Config = config;

            UnitMeleeDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitMeleeDamageTakenMultiplier",
                1.0f
            );

            StructureDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "StructureDamageTakenMultiplier",
                1.0f
            );
        }
        private static StructureConfig GetOrCreateStructureConfig(eStructs structure)
        {
            if (!StructureConfigs.TryGetValue(structure, out var config))
            {
                try
                {
                    string name = structure.ToString();

                    // Get raw values from API
                    var health = (int)Plugin.BuildingApi.GetDefaultHealth(structure);
                    var defaultCost = Plugin.BuildingApi.GetDefaultCost(structure);

                    Plugin.Logger.LogInfo($"[{name}] Raw API values - Health: {health}, Gold: {defaultCost.Gold}, Wood: {defaultCost.Wood}, Stone: {defaultCost.Stone}, Iron: {defaultCost.Iron}, Pitch: {defaultCost.Pitch}");

                    config = new StructureConfig
                    {
                        Health = Config.Bind(
                            "Structures",
                            $"{name}.Health",
                            health,
                            $"-------- {name} --------"),
                        GoldCost = Config.Bind("Structures", $"{name}.GoldCost", defaultCost.Gold),
                        WoodCost = Config.Bind("Structures", $"{name}.WoodCost", defaultCost.Wood),
                        StoneCost = Config.Bind("Structures", $"{name}.StoneCost", defaultCost.Stone),
                        IronCost = Config.Bind("Structures", $"{name}.IronCost", defaultCost.Iron),
                        PitchCost = Config.Bind("Structures", $"{name}.PitchCost", defaultCost.Pitch),
                    };
                    Plugin.Logger.LogInfo($"Loaded StructureConfig for {name}");
                    StructureConfigs[structure] = config;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to create StructureConfig for {structure}: {ex}");
                    return null;
                }
            }
            return config;
        }
        private static UnitConfig GetOrCreateUnitConfig(eChimps unit)
        {
            if (!ConfigManager.UnitConfigs.TryGetValue(unit, out var config))
            {
                try
                {
                    string name = unit.ToString();

                    config = new UnitConfig
                    {
                        Health = Config.Bind(
                            "Units",
                            $"{name}.Health",
                            (int)Plugin.UnitApi.GetDefaultHealth(unit),
                            $"-------- {name} --------"),
                        ArrowDamageTaken = Config.Bind("Units", $"{name}.ArrowDamageTaken", Plugin.UnitApi.GetRangedArrowDamageTo(unit)),
                        BoltDamageTaken = Config.Bind("Units", $"{name}.BoltDamageTaken", Plugin.UnitApi.GetRangedBoltDamageTo(unit)),
                        // **API NEEDS FIX** 
                        //JavelinDamageTaken = Config.Bind("Units", $"{name}.JavelinDamageTaken", UnitApi.GetRangedJavelinDamageTo(unit)),
                        SlingerDamageTaken = Config.Bind("Units", $"{name}.SlingerDamageTaken", Plugin.UnitApi.GetRangedSlingerDamageTo(unit)),
                        GoldCost = Config.Bind("Units", $"{name}.GoldCost", Plugin.UnitApi.GetUnitGoldCost(unit)),
                        MeleeSwordDamageTakenMult = Config.Bind("Units", $"{name}.MeleeSwordDamageTakenMult", 1.0f),
                        MeleeBluntDamageTakenMult = Config.Bind("Units", $"{name}.MeleeBluntDamageTakenMult", 1.0f),
                        MeleePolearmDamageTakenMult = Config.Bind("Units", $"{name}.MeleePolearmDamageTakenMult", 1.0f),
                        MeleeCavalryDamageTakenMult = Config.Bind("Units", $"{name}.MeleeCavalryDamageTakenMult", 1.0f),
                        MeleeOtherDamageTakenMult = Config.Bind("Units", $"{name}.MeleeOtherDamageTakenMult", 1.0f),
                        Resource1Type = Config.Bind("Units", $"{name}.Resource1Type", Plugin.UnitApi.GetUnitGoodCosts(unit).cost1.ToString()),
                        Resource2Type = Config.Bind("Units", $"{name}.Resource2Type", Plugin.UnitApi.GetUnitGoodCosts(unit).cost2.ToString()),
                        Resource3Type = Config.Bind("Units", $"{name}.Resource3Type", Plugin.UnitApi.GetUnitGoodCosts(unit).cost3.ToString()),
                        Resource4Type = Config.Bind("Units", $"{name}.Resource4Type", Plugin.UnitApi.GetUnitGoodCosts(unit).cost4.ToString()),
                    };

                    Plugin.Logger.LogInfo($"Loaded UnitConfig for {name}");
                    ConfigManager.UnitConfigs[unit] = config;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to create UnitConfig for {unit}: {ex}");
                    return null;
                }
            }
            return config;
        }
        internal static void ApplyAllUnitConfigs()
        {
            foreach (eChimps unit in Enum.GetValues(typeof(eChimps)))
            {
                try
                {
                    var cfg = GetOrCreateUnitConfig(unit);
                    if (cfg == null) continue;

                    if (cfg.Health?.Value is int health)
                        Plugin.UnitApi.SetDefaultHealth(unit, (uint)health);

                    if (cfg.ArrowDamageTaken?.Value is int arrow)
                        Plugin.UnitApi.SetRangedArrowDamageTo(unit, arrow);

                    if (cfg.BoltDamageTaken?.Value is int bolt)
                        Plugin.UnitApi.SetRangedBoltDamageTo(unit, bolt);

                    if (cfg.SlingerDamageTaken?.Value is int slinger)
                        Plugin.UnitApi.SetRangedSlingerDamageTo(unit, slinger);

                    if (cfg.MeleeSwordDamageTakenMult?.Value is float blade)
                        UnitStats.ApplySwordDamageMultipliers(unit, blade);

                    if (cfg.MeleeBluntDamageTakenMult?.Value is float blunt)
                        UnitStats.ApplyBluntDamageMultipliers(unit, blunt);

                    if (cfg.MeleePolearmDamageTakenMult?.Value is float polearm)
                        UnitStats.ApplyPolearmDamageMultipliers(unit, polearm);

                    if (cfg.MeleeCavalryDamageTakenMult?.Value is float cavalry)
                        UnitStats.ApplyCavalryDamageMultipliers(unit, cavalry);

                    if (cfg.MeleeOtherDamageTakenMult?.Value is float other)
                        UnitStats.ApplyOtherDamageMultipliers(unit, other);

                    if (cfg.GoldCost?.Value is int gold)
                        Plugin.UnitApi.SetUnitGoldCost(unit, gold);

                    eGoods ParseOrDefault(string value, eGoods defaultValue) =>
                        !string.IsNullOrEmpty(value) && Enum.TryParse(value, out eGoods result)
                            ? result
                            : defaultValue;

                    var currentCosts = Plugin.UnitApi.GetUnitGoodCosts(unit);
                    var costs = new UnitGoodCosts
                    {
                        cost1 = ParseOrDefault(cfg.Resource1Type?.Value, currentCosts.cost1),
                        cost2 = ParseOrDefault(cfg.Resource2Type?.Value, currentCosts.cost2),
                        cost3 = ParseOrDefault(cfg.Resource3Type?.Value, currentCosts.cost3),
                        cost4 = ParseOrDefault(cfg.Resource4Type?.Value, currentCosts.cost4)
                    };
                    Plugin.UnitApi.SetUnitGoodCosts(unit, costs);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to apply config for unit {unit}: {ex}");
                }
            }
        }
        internal static void ApplyAllBuildingConfigs()
        {
            foreach (eStructs structure in Enum.GetValues(typeof(eStructs)))
            {
                try
                {
                    var cfg = ConfigManager.GetOrCreateStructureConfig(structure);
                    if (cfg == null) continue;

                    if (cfg.Health?.Value is int health)
                        Plugin.BuildingApi.SetDefaultHealth(structure, (uint)health);

                    var cost = Plugin.BuildingApi.GetDefaultCost(structure);

                    if (cfg.GoldCost?.Value is int gold)
                        cost.Gold = gold;
                    if (cfg.WoodCost?.Value is int wood)
                        cost.Wood = wood;
                    if (cfg.StoneCost?.Value is int stone)
                        cost.Stone = stone;
                    if (cfg.IronCost?.Value is int iron)
                        cost.Iron = iron;
                    if (cfg.PitchCost?.Value is int pitch)
                        cost.Pitch = pitch;

                    Plugin.BuildingApi.SetDefaultCost(structure, cost);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to apply config for structure {structure}: {ex}");
                }
            }
        }
        internal static void ApplyAllMultiplierConfigs()
        {
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    var modified = (int)Mathf.Clamp((float)args.Damage * StructureDamageTakenMultiplier.Value, 1, int.MaxValue);
                    args.Damage = modified;
                });

            UnitR3EventHooks.OnUnitTakeMeeleDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    eChimps attacker = Plugin.UnitApi.GetType(args.AttackingUnitId);
                    eChimps defender = Plugin.UnitApi.GetType(args.DamagedUnitId);

                    int baseDamage = args.Value > 0
                        ? args.Value
                        : Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);

                    int modified = Mathf.Max(1, (int)(baseDamage * UnitMeleeDamageTakenMultiplier.Value));

                    //Plugin.Logger.LogInfo($"Melee: {attacker} -> {defender} | args.Value={args.Value} | baseDamage={baseDamage} | mult={UnitMeleeDamageTakenMultiplier.Value} | final={modified}");

                    args.Value = modified;
                });
        }
    }

    internal class UnitConfig
    {
        internal ConfigEntry<int> Health { get; set; }
        internal ConfigEntry<int> ArrowDamageTaken { get; set; }
        internal ConfigEntry<int> BoltDamageTaken { get; set; }
        internal ConfigEntry<int> SlingerDamageTaken { get; set; }
        internal ConfigEntry<float> MeleeSwordDamageTakenMult { get; set; }
        internal ConfigEntry<float> MeleeBluntDamageTakenMult { get; set; }
        internal ConfigEntry<float> MeleePolearmDamageTakenMult { get; set; }
        internal ConfigEntry<float> MeleeCavalryDamageTakenMult { get; set; }
        internal ConfigEntry<float> MeleeOtherDamageTakenMult { get; set; }
        internal ConfigEntry<int> GoldCost { get; set; }
        internal ConfigEntry<string> Resource1Type { get; set; }
        internal ConfigEntry<string> Resource2Type { get; set; }
        internal ConfigEntry<string> Resource3Type { get; set; }
        internal ConfigEntry<string> Resource4Type { get; set; }
    }

    internal class StructureConfig
    {
        internal ConfigEntry<int> Health { get; set; }
        internal ConfigEntry<int> GoldCost { get; set; }
        internal ConfigEntry<int> WoodCost { get; set; }
        internal ConfigEntry<int> StoneCost { get; set; }
        internal ConfigEntry<int> IronCost { get; set; }
        internal ConfigEntry<int> PitchCost { get; set; }
    }
}
