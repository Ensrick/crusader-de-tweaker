// Systems/DamageCalculation/IWeaponDamageCalculator.cs
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Interface for weapon-specific damage calculation strategies.
    /// Each weapon type (Sword, Mace, Polearm, etc.) has its own calculator
    /// that implements the specific damage formula for that weapon.
    /// </summary>
    internal interface IWeaponDamageCalculator
    {
        /// <summary>
        /// Calculate base damage for this weapon type.
        /// This is the damage before modifiers, caps, and special rules are applied.
        /// </summary>
        /// <param name="attacker">Attacker unit data</param>
        /// <param name="defender">Defender unit data</param>
        /// <param name="weaponCategory">Weapon category (for lookup table)</param>
        /// <param name="armorCategory">Armor category (for lookup table)</param>
        /// <returns>Base damage value</returns>
        float CalculateBaseDamage(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory, ArmorCategory armorCategory);

        /// <summary>
        /// Apply damage caps specific to this weapon type.
        /// </summary>
        /// <param name="damage">Current damage value</param>
        /// <param name="attacker">Attacker unit data</param>
        /// <param name="defender">Defender unit data</param>
        /// <param name="weaponCategory">Weapon category</param>
        /// <returns>Damage after applying caps</returns>
        float ApplyDamageCaps(float damage, UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory);
    }
}

