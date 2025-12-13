// Systems/DamageCalculator.cs
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
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
        public const int MinimumDamage = 2;

        /// <summary>
        /// Weak attacker threshold - attackers with base damage at or below this
        /// deal flat damage, ignoring ALL modifiers.
        /// </summary>
        public const int WeakAttackerThreshold = 2;

        /// <summary>
        /// Weapon vs Armor multiplier lookup table.
        /// Key: (WeaponCategory, ArmorCategory), Value: Damage multiplier
        /// 
        /// Derived from analyzing CrusaderDETweaker_MeleeDamage.csv game data.
        /// </summary>
        private static readonly Dictionary<(WeaponCategory, ArmorCategory), float> WeaponVsArmorTable = new Dictionary<(WeaponCategory, ArmorCategory), float>
        {
            // UNARMED (civilians, weak units) - flat base damage to everyone
            { (WeaponCategory.Unarmed, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Siege), 1.0f },

            // SWORD - good all-around, reduced vs heavy
            // Formula: base * multiplier * armorValue
            // SWORDSMAN (100) vs KNIGHT (0.5): 50 = 100 * 1.0 * 0.5
            { (WeaponCategory.Sword, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Sword, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Heavy), 1.0f },  // Fixed: was 0.5, should be 1.0
            { (WeaponCategory.Sword, ArmorCategory.Siege), 0.4f },

            // MACE - terrible vs heavy armor
            { (WeaponCategory.Mace, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Mace, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Heavy), 0.33f },
            { (WeaponCategory.Mace, ArmorCategory.Siege), 0.5f },

            // POLEARM - ignores heavy armor, bonus vs light
            { (WeaponCategory.Polearm, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Polearm, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Siege), 0.4f },

            // LANCE (Cavalry) - bonus vs heavy, extra bonus vs light
            // Formula: base * multiplier * armorValue
            // KNIGHT (80) vs KNIGHT (0.5): 50 = 80 * 1.25 * 0.5
            { (WeaponCategory.Lance, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Lance, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Heavy), 1.25f },  // Fixed: was 0.625, should be 1.25
            { (WeaponCategory.Lance, ArmorCategory.Siege), 0.4f },

            // AXE - good vs siege, bonus vs light
            { (WeaponCategory.Axe, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Axe, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Siege), 1.0f },  // Bypasses siege defense

            // DAGGER (Assassin) - huge bonus vs unarmored, ignores heavy reduction
            { (WeaponCategory.Dagger, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Light), 2.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Siege), 0.025f },  // 2/80 from data

            // RANGED (bows, crossbows, slings in melee) - bonus vs heavy and light
            { (WeaponCategory.Ranged, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Ranged, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Heavy), 1.5f },  // Ranged gets bonus vs heavy!
            { (WeaponCategory.Ranged, ArmorCategory.Siege), 0.2f },

            // BEAST - flat damage, ignores all armor
            { (WeaponCategory.Beast, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Siege), 1.0f },
        };

        /// <summary>
        /// Special multipliers for specific attacker tags vs defender tags.
        /// Applied after the weapon vs armor lookup.
        /// </summary>
        private static readonly Dictionary<(string, string), float> TagVsTagModifiers = new Dictionary<(string, string), float>
        {
            // Polearm vs Ladderman: 5x damage
            { ("Weapon_Polearm", "Ladderman"), 5.0f },
            
            // Hunter vs Beast: 2x damage
            { ("Hunter", "Beast"), 2.0f },
            
            // Predator vs SmallPrey: 5x damage (Dog vs Rabbit)
            { ("Predator", "SmallPrey"), 5.0f },
        };

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

            // RULE 1: Weak attackers (Base ≤ 2) deal flat damage
            if (attackerData.BaseMeleeDamage <= WeakAttackerThreshold)
            {
                return attackerData.BaseMeleeDamage;
            }

            // Get weapon and armor categories
            var weaponCategory = GetWeaponCategory(attackerData);
            var armorCategory = GetArmorCategory(defenderData);

            // Core formula varies by weapon type:
            // - Most weapons: BaseDamage × WeaponVsArmorMultiplier × ArmorValue
            // - Assassin: Special formula (ignores lookup table)
            // - Ranged in melee: BaseDamage × ArmorValue × 2.0 (special multiplier)
            // - Unarmed: BaseDamage × ArmorValue (no lookup table)
            
            float damage;
            
            // Special handling for Assassin - uses special formula
            if (weaponCategory == WeaponCategory.Dagger)
            {
                damage = CalculateAssassinDamage(attackerData, defenderData);
            }
            // Special handling for Ranged units in melee
            else if (weaponCategory == WeaponCategory.Ranged)
            {
                damage = CalculateRangedMeleeDamage(attackerData, defenderData);
            }
            // Special handling for Unarmed units
            else if (weaponCategory == WeaponCategory.Unarmed)
            {
                damage = attackerData.BaseMeleeDamage * defenderData.ArmorValue;
            }
            // Normal weapons: BaseDamage × WeaponVsArmorMultiplier × ArmorValue
            else
            {
                float weaponVsArmorMultiplier = GetWeaponVsArmorMultiplier(weaponCategory, armorCategory);
                
                // Armor_Piercing ignores armor reduction (treats armor as 1.0)
                float effectiveArmorValue = attackerData.IsArmorPiercing ? 1.0f : defenderData.ArmorValue;
                
                damage = attackerData.BaseMeleeDamage * weaponVsArmorMultiplier * effectiveArmorValue;
            }

            // Apply tag vs tag modifiers (Polearm vs Ladderman, etc.)
            float tagModifier = GetTagVsTagModifier(attackerData, defenderData);
            damage *= tagModifier;

            // Apply unit-specific modifier if exists
            float specialModifier = attackerData.GetModifierAgainst(defender);
            damage *= specialModifier;

            // Round and apply minimum damage floor
            int finalDamage = (int)Math.Round(damage);
            return Math.Max(MinimumDamage, finalDamage);
        }

        /// <summary>
        /// Get the weapon category for an attacker based on their tags.
        /// </summary>
        private static WeaponCategory GetWeaponCategory(UnitDamageData attacker)
        {
            // Check in order of specificity
            if (attacker.HasTag("Beast"))
                return WeaponCategory.Beast;

            if (attacker.HasTag("Weapon_Dagger") || attacker.HasTag("Assassin"))
                return WeaponCategory.Dagger;

            if (attacker.HasTag("Ranged_Bow") || attacker.HasTag("Ranged_Crossbow") ||
                attacker.HasTag("Ranged_Sling") || attacker.HasTag("Ranged_Javelin"))
                return WeaponCategory.Ranged;

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
            if (WeaponVsArmorTable.TryGetValue((weapon, armor), out float multiplier))
            {
                return multiplier;
            }
            return 1.0f; // Default if not found
        }

        /// <summary>
        /// Get modifier for specific tag vs tag combinations.
        /// </summary>
        private static float GetTagVsTagModifier(UnitDamageData attacker, UnitDamageData defender)
        {
            float modifier = 1.0f;

            foreach (var entry in TagVsTagModifiers)
            {
                var (attackerTag, defenderTag) = entry.Key;
                if (attacker.HasTag(attackerTag) && defender.HasTag(defenderTag))
                {
                    modifier *= entry.Value;
                }
            }

            return modifier;
        }

        /// <summary>
        /// Special damage calculation for Assassin (Dagger).
        /// Assassin has unique rules that don't follow the standard lookup table.
        /// </summary>
        private static float CalculateAssassinDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;

            // Assassin formula from game data:
            // vs Medium (1.0): flat base damage (80)
            // vs Heavy (0.5): flat base damage (80) - ignores armor reduction
            // vs Light (1.5): varies by unit (250 for Slinger, 150 for Eunuch)
            // vs Unarmored (2.0): 250 = base * 3.125
            
            if (armorValue <= 1.0f)
            {
                // Medium or Heavy: flat base damage
                return baseDamage;
            }
            else if (armorValue >= 2.0f)
            {
                // Unarmored: 3.125x multiplier
                return baseDamage * 3.125f;
            }
            else
            {
                // Light armor (1.5): varies, use average of observed values
                // Slinger: 250 = 80 * 3.125, Eunuch: 150 = 80 * 1.875
                // Use formula: base * (1.0 + (armor - 1.0) * 4.25)
                float multiplier = 1.0f + (armorValue - 1.0f) * 4.25f;
                return baseDamage * multiplier;
            }
        }

        /// <summary>
        /// Special damage calculation for Ranged units in melee combat.
        /// Ranged units use a different formula when fighting in melee.
        /// </summary>
        private static float CalculateRangedMeleeDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;

            // Ranged units in melee: base * armorValue * 2.0
            // ARAB_BOW (10) vs ARAB_ASSASIN (1.0): 10 * 1.0 * 2.0 = 20 ✓
            // ARAB_BOW (10) vs ARAB_SLAVE (2.0): 10 * 2.0 * 1.5 = 30 ✓
            // ARAB_BOW (10) vs ARAB_SLINGER (1.5): 10 * 1.5 * 2.0 = 30 ✓
            // ARAB_BOW (10) vs KNIGHT (0.5): 10 * 0.5 * 3.0 = 15 ✓
            
            float multiplier;
            if (armorValue >= 2.0f)
                multiplier = 1.5f;  // Unarmored
            else if (armorValue >= 1.5f)
                multiplier = 2.0f;   // Light
            else if (armorValue >= 1.0f)
                multiplier = 2.0f;   // Medium
            else
                multiplier = 3.0f;    // Heavy (0.5)
            
            return baseDamage * armorValue * multiplier;
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
            float tagMod = GetTagVsTagModifier(attackerData, defenderData);

            return $"{attacker} -> {defender}:\n" +
                   $"  Base: {attackerData.BaseMeleeDamage}\n" +
                   $"  Weapon: {weaponCat}, Armor: {armorCat}\n" +
                   $"  WeaponVsArmor: {multiplier:F3}\n" +
                   $"  TagModifier: {tagMod:F3}\n" +
                   $"  Final: {CalculateMeleeDamage(attacker, defender)}";
        }
    }
}
