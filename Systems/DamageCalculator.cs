// Systems/DamageCalculator.cs
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    /// <summary>
    /// Calculates melee damage between units using the damage formula.
    /// 
    /// Core Formula: BaseMeleeDamage × ArmorMultiplier × WeaponArmorModifier × TagModifiers
    /// 
    /// Rules (applied in order):
    /// 1. Weak attackers (Base ≤ 2): Deal flat base damage, ignore all modifiers
    /// 2. Beast attackers: Deal flat base damage, ignore armor
    /// 3. SiegeDefense defenders: Most attackers deal minimum damage (2)
    /// 4. All others: Apply armor and modifiers
    /// 
    /// Armor Interaction Rules:
    /// - Armor_Piercing (Lord): Ignores armor reduction (Heavy), gets capped bonus vs unarmored
    /// - Assassin: Ignores reduction, gets huge bonus vs unarmored
    /// - Weapon_Polearm: Ignores Heavy reduction, 5x vs Ladderman
    /// - Weapon_Mace (Blunt) vs Heavy: Additional 0.67x penalty
    /// - Cavalry vs Heavy: 1.25x bonus (after armor reduction)
    /// 
    /// Minimum damage floor: 2 (unless defender is immune)
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
        /// Calculate melee damage from attacker to defender.
        /// Returns -1 if calculation fails (missing data).
        /// </summary>
        public static int CalculateMeleeDamage(eChimps attacker, eChimps defender)
        {
            // Get unit data
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null || defenderData == null)
            {
                return -1; // Missing data
            }

            // RULE 1: Weak attackers (Base ≤ 2) deal flat damage
            if (attackerData.BaseMeleeDamage <= WeakAttackerThreshold)
            {
                return attackerData.BaseMeleeDamage;
            }

            // Calculate tag modifiers (Polearm vs Ladderman, Hunter vs Beast, etc.)
            float tagModifier = CalculateTagModifiers(attackerData, defenderData);

            // RULE 2: Beast attackers deal flat base damage × tag modifiers
            if (HasBeastTag(attackerData))
            {
                int beastDamage = (int)Math.Round(attackerData.BaseMeleeDamage * tagModifier);
                return Math.Max(MinimumDamage, beastDamage);
            }

            // RULE 3: SiegeDefense (Trebuchet) - most attackers deal minimum damage
            if (defenderData.HasTag("SiegeDefense"))
            {
                if (!BypassesSiegeDefense(attackerData))
                {
                    return MinimumDamage;
                }
                // Attackers that bypass SiegeDefense continue with normal calculation
            }

            // NOTE: Weapon_Unarmed attackers use the same formula as normal units
            // They just apply base * armor like everyone else

            // RULE 5: Normal calculation
            float damage = attackerData.BaseMeleeDamage;

            // Apply defender's armor value (with special handling for certain attackers)
            float effectiveArmor = GetEffectiveArmorValue(attackerData, defenderData);
            damage *= effectiveArmor;

            // Apply weapon vs armor type modifier
            float weaponArmorMod = GetWeaponArmorModifier(attackerData, defenderData);
            damage *= weaponArmorMod;

            // Apply tag-based modifiers
            damage *= tagModifier;

            // Apply unit-specific modifier if exists
            float specialModifier = attackerData.GetModifierAgainst(defender);
            damage *= specialModifier;

            // Round and apply minimum damage floor
            int finalDamage = (int)Math.Round(damage);
            return Math.Max(MinimumDamage, finalDamage);
        }

        /// <summary>
        /// Check if attacker has any beast-related tag.
        /// </summary>
        private static bool HasBeastTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Beast");
        }

        /// <summary>
        /// Check if attacker has any polearm-related tag.
        /// </summary>
        private static bool HasPolearmTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Polearm") || attacker.HasTag("Weapon_Polearm");
        }

        /// <summary>
        /// Check if attacker has any armor-piercing tag.
        /// </summary>
        private static bool HasArmorPiercingTag(UnitDamageData attacker)
        {
            return attacker.HasTag("ArmorPiercing") || attacker.HasTag("Armor_Piercing");
        }

        /// <summary>
        /// Check if attacker has the assassin tag.
        /// </summary>
        private static bool HasAssassinTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Assassin");
        }

        /// <summary>
        /// Check if attacker has a blunt weapon tag.
        /// </summary>
        private static bool HasBluntTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Blunt") || attacker.HasTag("Weapon_Mace");
        }

        /// <summary>
        /// Check if attacker has cavalry tag.
        /// </summary>
        private static bool HasCavalryTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Cavalry");
        }

        /// <summary>
        /// Check if attacker has chopping weapon (axes).
        /// </summary>
        private static bool HasChoppingTag(UnitDamageData attacker)
        {
            return attacker.HasTag("Chopping") || attacker.HasTag("Weapon_Axe");
        }

        /// <summary>
        /// Get the effective armor value, considering attacker abilities.
        /// 
        /// Normal: Use defender's ArmorValue directly (e.g., 0.5 for Heavy, 2.0 for unarmored)
        /// 
        /// Beast: Handled separately (flat damage)
        /// 
        /// Armor_Piercing (Lord):
        /// - Ignores armor reduction (Heavy treated as 1.0)
        /// - Gets capped bonus vs unarmored: max 1.33x
        /// 
        /// Assassin:
        /// - Ignores armor reduction (Heavy treated as 1.0)
        /// - Gets huge bonus vs unarmored (up to ~3.1x)
        /// 
        /// Weapon_Polearm:
        /// - Ignores Heavy reduction (treated as 1.0)
        /// - Gets normal bonus vs unarmored
        /// </summary>
        private static float GetEffectiveArmorValue(UnitDamageData attacker, UnitDamageData defender)
        {
            float armorValue = defender.ArmorValue;

            // ARMOR PIERCING (Lord): Ignore reduction, capped bonus
            if (HasArmorPiercingTag(attacker))
            {
                if (armorValue < 1.0f)
                {
                    return 1.0f; // Ignore Heavy reduction
                }
                // Capped bonus: max ~1.33x
                // Data: Lord (150) vs Slave (2.0) = 200, so 200/150 = 1.33
                return Math.Min(armorValue, 1.33f);
            }

            // ASSASSIN: Ignore reduction, huge bonus vs unarmored
            if (HasAssassinTag(attacker))
            {
                if (armorValue < 1.0f)
                {
                    return 1.0f; // Ignore Heavy reduction
                }
                // Huge bonus: ~3.1x vs unarmored (armor 2.0)
                // Data: Assassin (80) vs Slave (2.0) = 250, so 250/80 = 3.125
                // Formula: 1.0 + (armor - 1.0) * 2.125
                return 1.0f + (armorValue - 1.0f) * 2.125f;
            }

            // POLEARM: Ignore Heavy reduction, normal bonus vs unarmored
            if (HasPolearmTag(attacker))
            {
                if (armorValue < 1.0f)
                {
                    return 1.0f; // Ignore Heavy reduction
                }
                // Normal bonus vs unarmored
                return armorValue;
            }

            // Normal attackers: Apply armor directly
            return armorValue;
        }

        /// <summary>
        /// Check if an attacker bypasses Siege Defense (Trebuchet's special armor).
        /// </summary>
        private static bool BypassesSiegeDefense(UnitDamageData attacker)
        {
            // Beast always bypasses
            if (HasBeastTag(attacker))
                return true;

            // Armor Piercing bypasses
            if (HasArmorPiercingTag(attacker))
                return true;

            // Chopping weapons (axes, pickaxes) bypass
            if (HasChoppingTag(attacker))
                return true;

            // Light blunt weapons (Base ≤ 50) bypass - Monk (50) bypasses
            if (HasBluntTag(attacker) && attacker.BaseMeleeDamage <= 50)
                return true;

            return false;
        }

        /// <summary>
        /// Get the weapon vs armor type modifier.
        /// </summary>
        private static float GetWeaponArmorModifier(UnitDamageData attacker, UnitDamageData defender)
        {
            // Check Blunt vs Heavy
            // Data: Maceman (75) vs Knight (0.5) = 25, but 75 * 0.5 = 37.5
            // So modifier = 25/37.5 = 0.67
            if (HasBluntTag(attacker) && defender.ArmorType == ArmorType.Heavy)
            {
                return 0.67f;
            }

            // Check Cavalry vs Heavy
            // Data: Knight (80) vs Knight (0.5) = 50, so 80 * 0.5 * X = 50, X = 1.25
            if (HasCavalryTag(attacker) && defender.ArmorType == ArmorType.Heavy)
            {
                return 1.25f;
            }

            return 1.0f;
        }

        /// <summary>
        /// Calculate modifiers based on attacker/defender tags.
        /// </summary>
        private static float CalculateTagModifiers(UnitDamageData attacker, UnitDamageData defender)
        {
            float modifier = 1.0f;

            // POLEARM vs LADDERMAN: 5x damage bonus
            if (HasPolearmTag(attacker) && defender.HasTag("Ladderman"))
            {
                modifier *= 5.0f;
            }

            // HUNTER vs BEAST: 2x damage bonus
            if (attacker.HasTag("Hunter") && HasBeastTag(defender))
            {
                modifier *= 2.0f;
            }

            // DOG (Predator) vs RABBIT (SmallPrey): 5x damage bonus
            if (attacker.HasTag("Predator") && defender.HasTag("SmallPrey"))
            {
                modifier *= 5.0f;
            }

            return modifier;
        }

        /// <summary>
        /// Verify a melee damage calculation against expected value.
        /// Returns true if calculated matches expected.
        /// </summary>
        public static bool VerifyMeleeDamage(eChimps attacker, eChimps defender, int expectedDamage)
        {
            int calculated = CalculateMeleeDamage(attacker, defender);
            return calculated == expectedDamage;
        }

        /// <summary>
        /// Quick diagnostic for a specific attacker-defender pair.
        /// </summary>
        public static string DiagnoseMeleeDamage(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null)
                return $"{attacker}: No data in registry";
            if (defenderData == null)
                return $"{defender}: No data in registry";

            if (attackerData.BaseMeleeDamage <= WeakAttackerThreshold)
                return $"{attacker} -> {defender}: Weak attacker (Base={attackerData.BaseMeleeDamage}), deals flat {attackerData.BaseMeleeDamage}";

            if (HasBeastTag(attackerData))
                return $"{attacker} -> {defender}: Beast attacker, deals flat {attackerData.BaseMeleeDamage}";

            if (defenderData.HasTag("SiegeDefense") && !BypassesSiegeDefense(attackerData))
                return $"{attacker} -> {defender}: SiegeDefense blocks, deals minimum {MinimumDamage}";

            float baseDamage = attackerData.BaseMeleeDamage;
            float effectiveArmor = GetEffectiveArmorValue(attackerData, defenderData);
            float weaponArmorMod = GetWeaponArmorModifier(attackerData, defenderData);
            float tagMod = CalculateTagModifiers(attackerData, defenderData);
            float specialMod = attackerData.GetModifierAgainst(defender);

            float rawFinal = baseDamage * effectiveArmor * weaponArmorMod * tagMod * specialMod;
            int final = Math.Max(MinimumDamage, (int)Math.Round(rawFinal));

            return $"{attacker} -> {defender}:\n" +
                   $"  BaseDamage: {baseDamage}\n" +
                   $"  × EffectiveArmor: {effectiveArmor:F3} (raw={defenderData.ArmorValue})\n" +
                   $"  × WeaponArmorMod: {weaponArmorMod:F3}\n" +
                   $"  × TagModifier: {tagMod:F3}\n" +
                   $"  × SpecialModifier: {specialMod:F3}\n" +
                   $"  = Raw: {rawFinal:F2}, Final (min {MinimumDamage}): {final}";
        }
    }
}
