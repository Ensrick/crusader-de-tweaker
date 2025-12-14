using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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

        internal static ConfigEntry<float> UnitHealthMultiplier { get; private set; }

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
            UnitHealthMultiplier = config.Bind(
               "Multipliers",
               "UnitHealthMultiplier",
               1.0f
           );
        }
        internal static void ApplyAllMultiplierConfigs()
        {
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    if (Mathf.Approximately(StructureDamageTakenMultiplier.Value, 1.0f))
                        return;

                    var modified = (int)Mathf.Clamp((float)args.Damage * StructureDamageTakenMultiplier.Value, 1, int.MaxValue);
                    args.Damage = modified;
                });

            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
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
                });

            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    if (Mathf.Approximately(UnitHealthMultiplier.Value, 1.0f))
                        return; // Skip if multiplier is 1.0

                    eChimps unitType = args.UnitType;

                    // Skip non-modifiable units
                    if (Systems.StatsUnits.NonModableUnits.Contains(unitType))
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
        }
    }
}
