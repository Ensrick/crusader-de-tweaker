// Systems/DamageCalculation/DaggerDamageCalculator.cs
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Damage calculator for Dagger (Assassin) weapons.
    /// Uses a special formula that doesn't follow the standard lookup table.
    /// </summary>
    internal class DaggerDamageCalculator : BaseWeaponDamageCalculator
    {
        public override float CalculateBaseDamage(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory, ArmorCategory armorCategory)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;

            // Assassin formula from game data:
            // vs Medium (1.0): flat base damage (80)
            // vs Heavy (0.5): flat base damage (80) - ignores armor reduction
            // vs Light (1.5): varies by unit (250 for Slinger, 150 for Eunuch)
            // vs Unarmored (2.0): 250 = base * 3.125
            // vs Siege (0.4): 2 (minimum damage)
            
            if (armorValue <= 0.5f)
            {
                // Heavy or Siege: flat base damage (but minimum damage applies)
                return baseDamage;
            }
            else if (armorValue <= 1.0f)
            {
                // Medium: flat base damage
                return baseDamage;
            }
            else if (armorValue >= 2.0f)
            {
                // Unarmored: 3.125x multiplier
                return baseDamage * DamageConstants.Assassin.UnarmoredMultiplier;
            }
            else
            {
                // Light armor (1.5): varies by defender type
                // Use average: base * 2.5
                return baseDamage * DamageConstants.Assassin.LightArmorMultiplier;
            }
        }
    }
}

