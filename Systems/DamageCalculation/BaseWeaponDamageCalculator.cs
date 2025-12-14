// Systems/DamageCalculation/BaseWeaponDamageCalculator.cs
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Base class for weapon-specific damage calculators.
    /// Provides common functionality and lookup table access.
    /// </summary>
    internal abstract class BaseWeaponDamageCalculator : IWeaponDamageCalculator
    {
        /// <summary>
        /// Get weapon vs armor multiplier from the lookup table.
        /// </summary>
        protected float GetWeaponVsArmorMultiplier(WeaponCategory weapon, ArmorCategory armor)
        {
            return WeaponVsArmorLookupTable.GetMultiplier(weapon, armor);
        }

        /// <summary>
        /// Calculate base damage for this weapon type.
        /// Default implementation uses the standard formula: BaseDamage × WeaponVsArmorMultiplier × ArmorValue
        /// </summary>
        public virtual float CalculateBaseDamage(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory, ArmorCategory armorCategory)
        {
            float weaponVsArmorMultiplier = GetWeaponVsArmorMultiplier(weaponCategory, armorCategory);
            float effectiveArmorValue = GetEffectiveArmorValue(attacker, defender, weaponCategory);
            
            return attacker.BaseMeleeDamage * weaponVsArmorMultiplier * effectiveArmorValue;
        }

        /// <summary>
        /// Apply damage caps specific to this weapon type.
        /// Default implementation: no caps (returns damage unchanged).
        /// </summary>
        public virtual float ApplyDamageCaps(float damage, UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            return damage; // No caps by default
        }

        /// <summary>
        /// Get the effective armor value for damage calculation.
        /// Some weapons ignore armor or have special armor handling.
        /// </summary>
        protected virtual float GetEffectiveArmorValue(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            // Lord (ArmorPiercing) vs Heavy/Medium: ignores armor (treats as 1.0)
            // Lord vs Unarmored: capped at 1.33x (200/150 = 1.33)
            if (attacker.IsArmorPiercing)
            {
                if (defender.ArmorValue >= 2.0f)
                    return 1.33f;  // Cap vs unarmored
                else
                    return 1.0f;  // Ignore armor reduction
            }
            
            return defender.ArmorValue;
        }

    }
}

