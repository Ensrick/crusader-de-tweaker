// Systems/DamageCalculator.cs
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using CrusaderDETweaker.Systems.DamageCalculation;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    /// <summary>
    /// Armor categories for the weapon vs armor lookup table.
    /// These correspond to defender tags: Armor_None, Armor_Light, etc.
    /// </summary>
    public enum ArmorCategory
    {
        None,       // Civilians, animals - Armor_None tag
        Light,      // Arab Slinger, Eunuch - Armor_Light tag  
        Medium,     // Most military units - Armor_Medium tag
        Heavy,      // Knight, Swordsman - Armor_Heavy tag
        Siege       // Trebuchet - Armor_Siege tag
    }

    /// <summary>
    /// Weapon categories for the weapon vs armor lookup table.
    /// These correspond to attacker tags.
    /// </summary>
    public enum WeaponCategory
    {
        Unarmed,    // Civilians, weak units - Weapon_Unarmed tag
        Sword,      // Swords - Weapon_Sword tag
        Mace,       // Blunt weapons - Weapon_Mace tag
        Polearm,    // Spears, pikes - Weapon_Polearm tag
        Lance,      // Cavalry lances - Weapon_Lance tag
        Axe,        // Axes - Weapon_Axe tag
        Dagger,     // Assassin - Weapon_Dagger tag
        Ranged,     // Bow/Crossbow/Sling in melee - Ranged_* tags
        Beast       // Animals - Beast tag
    }

    /// <summary>
    /// Calculates melee damage between units using a weapon vs armor lookup table.
    /// 
    /// Core Formula: BaseMeleeDamage × WeaponVsArmorMultiplier × SpecialModifiers
    /// 
    /// The lookup table maps (WeaponCategory, ArmorCategory) to damage multipliers,
    /// matching the game's actual damage calculations.
    /// </summary>
    internal static class DamageCalculator
    {
        /// <summary>
        /// Minimum damage floor - all attacks deal at least this much damage.
        /// </summary>
        public const int MinimumDamage = DamageConstants.MinimumDamage;

        /// <summary>
        /// Weak attacker threshold - attackers with base damage at or below this
        /// deal flat damage, ignoring ALL modifiers.
        /// </summary>
        public const int WeakAttackerThreshold = DamageConstants.WeakAttackerThreshold;

        /// <summary>
        /// Weapon vs Armor multiplier lookup table.
        /// Key: (WeaponCategory, ArmorCategory), Value: Damage multiplier
        /// 
        /// Derived from analyzing CrusaderDETweaker_MeleeDamage.csv game data.
        /// Now uses WeaponVsArmorLookupTable for the actual data.
        /// </summary>
        internal static readonly Dictionary<(WeaponCategory, ArmorCategory), float> WeaponVsArmorTable = WeaponVsArmorLookupTable.Table;


        /// <summary>
        /// Calculate melee damage from attacker to defender using ORIGINAL registry data (before TOML modifications).
        /// This is used for verification to test the calculation logic against original game defaults.
        /// Returns -1 if calculation fails (missing data).
        /// </summary>
        public static int CalculateMeleeDamageWithOriginalData(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetOriginalUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetOriginalUnitData(defender);

            if (attackerData == null || defenderData == null)
            {
                return -1;
            }

            return CalculateMeleeDamageInternal(attackerData, defenderData);
        }

        /// <summary>
        /// Calculate melee damage from attacker to defender.
        /// Returns -1 if calculation fails (missing data).
        /// </summary>
        public static int CalculateMeleeDamage(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null || defenderData == null)
            {
                return -1;
            }

            return CalculateMeleeDamageInternal(attackerData, defenderData);
        }

        /// <summary>
        /// Internal calculation method that performs the actual damage calculation.
        /// </summary>
        private static int CalculateMeleeDamageInternal(Data.UnitDamageData attackerData, Data.UnitDamageData defenderData)
        {

            // RULE 1: Weak attackers (Base ≤ 2) deal flat damage
            if (attackerData.BaseMeleeDamage <= DamageConstants.WeakAttackerThreshold)
            {
                return attackerData.BaseMeleeDamage;
            }

            // Get weapon and armor categories early (needed for SiegeDefense check)
            var weaponCategory = GetWeaponCategory(attackerData);
            var armorCategory = GetArmorCategory(defenderData);

            // RULE 2: SiegeDefense - Specific units deal minimum damage (2) vs Trebuchet
            var siegeDefenseDamage = SiegeDefenseHandler.GetSiegeDefenseDamage(attackerData, defenderData, weaponCategory);
            if (siegeDefenseDamage.HasValue)
            {
                return siegeDefenseDamage.Value;
            }

            // Get weapon calculator from factory
            var calculator = WeaponDamageCalculatorFactory.GetCalculator(weaponCategory);
            
            // Special case: Strong melee vs SiegeDefense bypasses lookup table
            // ARAB_SWORDSMAN (100) vs TREBUCHET: Game=40 = 100 * 0.4
            // BEDOUIN_DEMOLISHER (40) vs TREBUCHET: Game=40 = 40 * 1.0
            // KNIGHT (80) vs TREBUCHET: Game=30 = 80 * 0.375
            // LORD (150) vs TREBUCHET: Game=150 = 150 * 1.0
            float damage;
            if ((defenderData.HasTag("SiegeDefense") || defenderData.HasTag("Armor_Siege")) &&
                (weaponCategory == WeaponCategory.Sword || weaponCategory == WeaponCategory.Mace || 
                 weaponCategory == WeaponCategory.Lance || weaponCategory == WeaponCategory.Axe || 
                 weaponCategory == WeaponCategory.Polearm))
            {
                // Strong melee vs SiegeDefense: use weapon-specific multipliers
                float? siegeMultiplier = SiegeDefenseHandler.GetSiegeDefenseMultiplier(weaponCategory, attackerData);
                if (attackerData.IsArmorPiercing)
                {
                    damage = attackerData.BaseMeleeDamage * 1.0f; // Lord: base * 1.0
                }
                else if (siegeMultiplier.HasValue)
                {
                    damage = attackerData.BaseMeleeDamage * siegeMultiplier.Value;
                }
                else
                {
                    damage = attackerData.BaseMeleeDamage * 1.0f; // Default
                }
            }
            else
            {
                // Calculate base damage using weapon calculator
                damage = calculator.CalculateBaseDamage(attackerData, defenderData, weaponCategory, armorCategory);
            }
            
            // Apply unit-specific modifier BEFORE caps (for normal weapons only)
            // This allows modifiers to override the standard formula
            // Special weapons (Dagger, Ranged, Unarmed, Beast) apply modifiers AFTER caps
            bool isSpecialWeapon = weaponCategory == WeaponCategory.Dagger || 
                                   weaponCategory == WeaponCategory.Ranged || 
                                   weaponCategory == WeaponCategory.Unarmed || 
                                   weaponCategory == WeaponCategory.Beast;
            
            if (!isSpecialWeapon)
            {
                float specialModifier = attackerData.GetModifierAgainst(defenderData.Unit);
                if (specialModifier != 1.0f)
                {
                    damage *= specialModifier;
                }
            }
            
            // Apply damage caps using weapon calculator
            damage = calculator.ApplyDamageCaps(damage, attackerData, defenderData, weaponCategory);

            // Apply tag vs tag modifiers (Polearm vs Ladderman, etc.)
            // Note: For Ranged units, tag modifiers are applied in RangedMeleeDamageCalculator
            // For other units, apply here
            if (weaponCategory != WeaponCategory.Ranged)
            {
                float tagModifier = TagVsTagModifierHelper.GetModifier(attackerData, defenderData);
                damage *= tagModifier;
            }

            // For special weapons (Assassin, Ranged, Unarmed, Beast), apply unit-specific modifiers AFTER caps
            if (isSpecialWeapon)
            {
                float specialModifier = attackerData.GetModifierAgainst(defenderData.Unit);
                damage *= specialModifier;
            }

            // Round and apply minimum damage floor
            int finalDamage = (int)Math.Round(damage);
            return Math.Max(DamageConstants.MinimumDamage, finalDamage);
        }

        /// <summary>
        /// Get the weapon category for an attacker based on their tags.
        /// Priority: Beast > Weapon tags > Ranged tags > Unarmed
        /// Units with both Weapon and Ranged tags use Weapon category for melee.
        /// </summary>
        private static WeaponCategory GetWeaponCategory(UnitDamageData attacker)
        {
            // Check in order of specificity
            if (attacker.HasTag("Beast"))
                return WeaponCategory.Beast;

            // Weapon tags take priority over Ranged tags (for melee damage)
            if (attacker.HasTag("Weapon_Dagger") || attacker.HasTag("Assassin"))
                return WeaponCategory.Dagger;

            if (attacker.HasTag("Weapon_Lance"))
                return WeaponCategory.Lance;

            if (attacker.HasTag("Weapon_Polearm"))
                return WeaponCategory.Polearm;

            if (attacker.HasTag("Weapon_Axe"))
                return WeaponCategory.Axe;

            if (attacker.HasTag("Weapon_Mace"))
                return WeaponCategory.Mace;

            if (attacker.HasTag("Weapon_Sword"))
                return WeaponCategory.Sword;

            // Only check Ranged tags if no Weapon tag was found
            // (Ranged units without melee weapons use Ranged category in melee)
            if (attacker.HasTag("Ranged_Bow") || attacker.HasTag("Ranged_Crossbow") ||
                attacker.HasTag("Ranged_Sling") || attacker.HasTag("Ranged_Javelin"))
                return WeaponCategory.Ranged;

            // Default to unarmed
            return WeaponCategory.Unarmed;
        }

        /// <summary>
        /// Get the armor category for a defender based on their tags.
        /// </summary>
        private static ArmorCategory GetArmorCategory(UnitDamageData defender)
        {
            if (defender.HasTag("Armor_Siege") || defender.HasTag("SiegeDefense"))
                return ArmorCategory.Siege;

            if (defender.HasTag("Armor_Heavy"))
                return ArmorCategory.Heavy;

            if (defender.HasTag("Armor_Light"))
                return ArmorCategory.Light;

            if (defender.HasTag("Armor_None"))
                return ArmorCategory.None;

            // Default to Medium if no armor tag specified
            return ArmorCategory.Medium;
        }

        /// <summary>
        /// Look up the weapon vs armor multiplier from the table.
        /// </summary>
        private static float GetWeaponVsArmorMultiplier(WeaponCategory weapon, ArmorCategory armor)
        {
            return WeaponVsArmorLookupTable.GetMultiplier(weapon, armor);
        }


        /// <summary>
        /// Verify a melee damage calculation against expected value.
        /// </summary>
        public static bool VerifyMeleeDamage(eChimps attacker, eChimps defender, int expectedDamage)
        {
            int calculated = CalculateMeleeDamage(attacker, defender);
            return calculated == expectedDamage;
        }

        /// <summary>
        /// Diagnostic for a specific attacker-defender pair.
        /// </summary>
        public static string DiagnoseMeleeDamage(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null)
                return $"{attacker}: No data in registry";
            if (defenderData == null)
                return $"{defender}: No data in registry";

            var weaponCat = GetWeaponCategory(attackerData);
            var armorCat = GetArmorCategory(defenderData);
            float multiplier = GetWeaponVsArmorMultiplier(weaponCat, armorCat);
            float tagMod = TagVsTagModifierHelper.GetModifier(attackerData, defenderData);

            return $"{attacker} -> {defender}:\n" +
                   $"  Base: {attackerData.BaseMeleeDamage}\n" +
                   $"  Weapon: {weaponCat}, Armor: {armorCat}\n" +
                   $"  WeaponVsArmor: {multiplier:F3}\n" +
                   $"  TagModifier: {tagMod:F3}\n" +
                   $"  Final: {CalculateMeleeDamage(attacker, defender)}";
        }
    }
}
