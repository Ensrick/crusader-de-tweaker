// Config/BepInEx/Systems/UnitMultipliersConfig.cs
using System;
using System.Linq;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Data;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages unit-related multipliers (melee damage, ranged damage, health).
    /// Uses real-time event hooks to apply multipliers as units take damage or are created.
    /// </summary>
    internal class UnitMultipliersConfig : IBepInExConfigSystem
    {
        public string Name => "Unit Multipliers";

        public bool IsInitialized { get; private set; }

        public ConfigEntry<float> MeleeDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> RangedDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> HealthMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            MeleeDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitMeleeDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all melee damage taken by units. Note: Minimum damage is always 1 (game uses default damage matrix value if damage is 0)."
            );

            RangedDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitRangedDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all ranged damage taken by units. Affects all projectile types (Arrow, Bolt, Slinger, Javelin). Base projectile damage is 2500. Note: Minimum damage is always 1 (game uses default damage matrix value if damage is 0)."
            );

            HealthMultiplier = config.Bind(
                "Multipliers",
                "UnitHealthMultiplier",
                1.0f,
                "Global multiplier for unit max health"
            );

            ValidateMultipliers();
            IsInitialized = true;
        }

        public void Apply()
        {
            ApplyMeleeDamageMultiplier();
            ApplyRangedDamageMultiplier();
            ApplyHealthMultiplier();
        }

        /// <summary>
        /// Validates all unit multiplier values and logs warnings for invalid values.
        /// </summary>
        private void ValidateMultipliers()
        {
            ValidateMultiplier("UnitMeleeDamageTakenMultiplier", MeleeDamageTakenMultiplier.Value);
            ValidateMultiplier("UnitRangedDamageTakenMultiplier", RangedDamageTakenMultiplier.Value);
            ValidateMultiplier("UnitHealthMultiplier", HealthMultiplier.Value);
        }

        /// <summary>
        /// Validates a single multiplier value and logs a warning if invalid.
        /// </summary>
        private void ValidateMultiplier(string name, float value)
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
        /// Subscribes to melee damage event hook to apply melee damage multiplier in real-time.
        /// </summary>
        private void ApplyMeleeDamageMultiplier()
        {
            Plugin.Logger.LogInfo("Subscribing to OnUnitTakeMeleeDamage event hook...");
            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        // Commented out to reduce log spam - uncomment for debugging
                        // Plugin.Logger.LogDebug($"OnUnitTakeMeleeDamage hook triggered: AttackerId={args.AttackingUnitId}, DefenderId={args.DamagedUnitId}, Damage={args.Damage}, Multiplier={MeleeDamageTakenMultiplier.Value}");
                        
                        if (Mathf.Approximately(MeleeDamageTakenMultiplier.Value, 1.0f))
                        {
                            // Plugin.Logger.LogDebug("Melee multiplier is 1.0, skipping modification");
                            return;
                        }

                        eChimps attacker = Plugin.UnitApi.GetType(args.AttackingUnitId);
                        eChimps defender = Plugin.UnitApi.GetType(args.DamagedUnitId);

                        int baseDamage = args.Damage > 0
                            ? args.Damage
                            : Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);

                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        int modified = Mathf.Max(1, (int)(baseDamage * MeleeDamageTakenMultiplier.Value));

                        // Commented out to reduce log spam - uncomment for debugging
                        // Plugin.Logger.LogInfo($"Melee damage modification: {attacker} -> {defender} | args.Damage={args.Damage} | baseDamage={baseDamage} | multiplier={MeleeDamageTakenMultiplier.Value} | modified={modified}");
                        args.Damage = modified;
                        // Plugin.Logger.LogDebug($"After modification: args.Damage={args.Damage}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to apply unit melee damage multiplier: {ex.Message}\n{ex.StackTrace}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnUnitTakeMeleeDamage event hook");
        }

        /// <summary>
        /// Subscribes to ranged damage event hook to apply ranged damage multiplier in real-time.
        /// </summary>
        private void ApplyRangedDamageMultiplier()
        {
            // Hook for ranged damage multiplier (using Ex version which allows damage modification)
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        if (Mathf.Approximately(RangedDamageTakenMultiplier.Value, 1.0f))
                            return;

                        // Apply multiplier to projectile damage
                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        int modified = Mathf.Max(1, (int)(args.Damage * RangedDamageTakenMultiplier.Value));
                        args.Damage = modified;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to apply ranged damage multiplier: {ex.Message}");
                    }
                });
        }

        /// <summary>
        /// Subscribes to unit creation event hook to apply health multiplier to newly created units.
        /// </summary>
        private void ApplyHealthMultiplier()
        {
            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    if (Mathf.Approximately(HealthMultiplier.Value, 1.0f))
                        return; // Skip if multiplier is 1.0

                    eChimps unitType = args.UnitType;

                    // Skip non-modifiable units
                    if (UnitCategories.NonModable.Contains(unitType))
                        return;

                    try
                    {
                        int unitId = (int)args.ReturnValue; // ReturnValue is long, cast to int

                        // Get current health and apply multiplier
                        int currentMaxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
                        int newMaxHealth = Mathf.Max(1, (int)(currentMaxHealth * HealthMultiplier.Value));

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

