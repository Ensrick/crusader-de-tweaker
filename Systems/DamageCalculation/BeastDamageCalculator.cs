// Systems/DamageCalculation/BeastDamageCalculator.cs
using System;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Damage calculator for Beast units.
    /// Large beasts (base >= 50) ignore armor, small beasts (base <= 10) use armor with caps.
    /// </summary>
    internal class BeastDamageCalculator : BaseWeaponDamageCalculator
    {
        protected override float GetEffectiveArmorValue(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            // Large beasts (base >= 50): always deal flat base damage, ignoring armor
            // Small beasts (base <= 10): use armor, but cap at base vs heavy armor
            if (attacker.BaseMeleeDamage >= DamageConstants.LargeBeastThreshold)
            {
                return 1.0f; // Large beasts ignore armor
            }
            else
            {
                // Small beasts use armor, but check for modifiers
                float beastModifier = attacker.GetModifierAgainst(defender.Unit);
                if (beastModifier != 1.0f && defender.HasTag("SiegeDefense"))
                {
                    return 1.0f; // Ignore armor if modifier exists
                }
                else
                {
                    return defender.ArmorValue;
                }
            }
        }

        public override float ApplyDamageCaps(float damage, UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            // Large beasts (base >= 50): already set effectiveArmorValue = 1.0, so no caps needed
            // Small beasts (base <= 10): use armor, but cap at base vs heavy armor and light armor
            if (attacker.BaseMeleeDamage < DamageConstants.LargeBeastThreshold)
            {
                // Small beasts vs Heavy armor: cap at base (don't reduce below base)
                if (defender.ArmorValue <= 0.5f)
                {
                    return Math.Max(damage, attacker.BaseMeleeDamage * 1.0f); // Floor at base damage
                }
                // Small beasts vs Light armor: cap at base (don't increase above base)
                else if (defender.ArmorValue >= 1.5f)
                {
                    return Math.Min(damage, attacker.BaseMeleeDamage * 1.0f); // Cap at base damage
                }
            }
            else
            {
                // Large beasts vs Unarmored: cap at base damage (not double)
                if (defender.ArmorValue >= 2.0f)
                {
                    return Math.Min(damage, attacker.BaseMeleeDamage * 1.0f); // Cap at base damage
                }
                // Large beasts vs Light armor: cap at base damage (not 1.5x)
                else if (defender.ArmorValue >= 1.5f && defender.ArmorValue < 2.0f)
                {
                    return Math.Min(damage, attacker.BaseMeleeDamage * 1.0f); // Cap at base damage
                }
            }

            return damage;
        }
    }
}

