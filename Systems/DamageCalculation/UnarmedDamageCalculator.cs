// Systems/DamageCalculation/UnarmedDamageCalculator.cs
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Damage calculator for Unarmed units.
    /// Unarmed units: flat base damage to most, double to unarmored (but cap at base for some units).
    /// </summary>
    internal class UnarmedDamageCalculator : BaseWeaponDamageCalculator
    {
        public override float CalculateBaseDamage(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory, ArmorCategory armorCategory)
        {
            // Unarmed units: flat base damage to most, double to unarmored (but cap at base for some units)
            if (defender.ArmorValue >= 2.0f)
            {
                // Unarmored: double damage for most, but cap at base for some defenders
                // BEDOUIN_HEALER seems to be an exception - cap at base
                if (defender.Unit == eChimps.CHIMP_TYPE_BEDOUIN_HEALER)
                {
                    return attacker.BaseMeleeDamage; // Cap at base
                }
                else
                {
                    return attacker.BaseMeleeDamage * 2.0f; // Double damage
                }
            }
            else
            {
                // Everyone else: flat base damage
                // Unit-specific modifiers will be applied later for special cases (e.g., ARAB_SLAVE vs Light)
                return attacker.BaseMeleeDamage;
            }
        }
    }
}

