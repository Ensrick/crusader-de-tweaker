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
            { (WeaponCategory.Sword, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Sword, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Heavy), 0.5f },
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
            { (WeaponCategory.Lance, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Lance, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Heavy), 0.625f },  // 0.5 * 1.25
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

            float damage;

            // Special handling for Assassin - uses armor value directly
            if (weaponCategory == WeaponCategory.Dagger)
            {
                damage = CalculateAssassinDamage(attackerData, defenderData);
            }
            // Special handling for Ranged units in melee - uses defender armor value
            else if (weaponCategory == WeaponCategory.Ranged)
            {
                damage = CalculateRangedMeleeDamage(attackerData, defenderData);
            }
            // Special handling for Unarmed units - uses defender armor value
            else if (weaponCategory == WeaponCategory.Unarmed)
            {
                damage = CalculateUnarmedDamage(attackerData, defenderData);
            }
            // Normal calculation using lookup table
            else
            {
                // Look up the base multiplier
                float multiplier = GetWeaponVsArmorMultiplier(weaponCategory, armorCategory);

                // Calculate base damage
                damage = attackerData.BaseMeleeDamage * multiplier;

                // Apply tag vs tag modifiers (Polearm vs Ladderman, etc.)
                float tagModifier = GetTagVsTagModifier(attackerData, defenderData);
                damage *= tagModifier;

                // Apply unit-specific modifier if exists
                float specialModifier = attackerData.GetModifierAgainst(defender);
                damage *= specialModifier;
            }

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
        /// Special damage calculation for Assassin.
        /// The Assassin uses defender armor value directly with special scaling.
        /// </summary>
        private static float CalculateAssassinDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float armorValue = defender.ArmorValue;
            float baseDamage = attacker.BaseMeleeDamage;

            // Assassin formula from game data:
            // vs Heavy (0.5): 80 → 80 (1.0x)
            // vs Medium (1.0): 80 → 80 (1.0x)
            // vs Light (1.5): 80 → 150-250 (varies by unit)
            // vs Unarmored (2.0): 80 → 250 (3.125x)
            
            if (armorValue <= 1.0f)
            {
                return baseDamage; // 1.0x vs Heavy/Medium
            }
            else if (armorValue >= 2.0f)
            {
                // Unarmored: 3.125x multiplier
                return baseDamage * 3.125f;
            }
            else
            {
                // Light armor (1.5): varies, but generally high
                // For Arab Slinger (1.5): 250 = 80 * 3.125
                // For Bedouin Eunuch (1.5): 150 = 80 * 1.875
                // Use a formula that gives ~2.5x average for 1.5 armor
                // Formula: base * (1.0 + (armor - 1.0) * 3.0)
                float multiplier = 1.0f + (armorValue - 1.0f) * 3.0f;
                return baseDamage * multiplier;
            }
        }

        /// <summary>
        /// Special damage calculation for Ranged units in melee combat.
        /// Uses defender armor value directly.
        /// </summary>
        private static float CalculateRangedMeleeDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float armorValue = defender.ArmorValue;
            float baseDamage = attacker.BaseMeleeDamage;

            // Ranged units in melee formula from game data:
            // ARAB_BOW (10) vs ARAB_SLAVE (2.0): Game=30
            // ARAB_BOW (10) vs ARAB_SLINGER (1.5): Game=30
            // ARAB_BOW (10) vs ARAB_ASSASIN (1.0): Game=20
            // ARAB_BOW (10) vs MACEMAN (1.0): Game=15
            // ARAB_BOW (10) vs KNIGHT (0.5): Game=15
            
            // Pattern analysis:
            // vs 2.0: 10 * 2.0 * 1.5 = 30 ✓
            // vs 1.5: 10 * 1.5 * 2.0 = 30 ✓
            // vs 1.0: 10 * 1.0 * 2.0 = 20 ✓ (but some show 15)
            // vs 0.5: 10 * 0.5 * 3.0 = 15 ✓
            
            // Formula: base * armorValue * multiplier
            // Multiplier varies: 1.5 for high armor (>=1.5), 2.0 for medium (1.0), 3.0 for low (<=0.5)
            float multiplier;
            if (armorValue >= 1.5f)
                multiplier = 1.5f;
            else if (armorValue >= 1.0f)
                multiplier = 2.0f;
            else
                multiplier = 3.0f;
            
            return baseDamage * armorValue * multiplier;
        }

        /// <summary>
        /// Special damage calculation for Unarmed units.
        /// Uses defender armor value directly.
        /// </summary>
        private static float CalculateUnarmedDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float armorValue = defender.ArmorValue;
            float baseDamage = attacker.BaseMeleeDamage;

            // Unarmed units: base * defenderArmorValue
            // ARAB_BALLISTA (10) vs ARAB_SLAVE (2.0): 10 * 2.0 = 20 ✓
            // ARAB_BALLISTA (10) vs TREBUCHET (0.4): 10 * 0.4 = 4, but min is 2 ✓
            
            return baseDamage * armorValue;
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
