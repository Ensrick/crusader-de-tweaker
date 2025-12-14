// Systems/DamageCalculation/SwordDamageCalculator.cs
using System;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Damage calculator for Sword weapons.
    /// Handles special cases like damage caps for weak sword units.
    /// </summary>
    internal class SwordDamageCalculator : BaseWeaponDamageCalculator
    {
        public override float ApplyDamageCaps(float damage, UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            // Only apply caps for weak sword units (base <= 20) and not BEDOUIN_EUNUCH
            if (attacker.BaseMeleeDamage > DamageConstants.WeakSwordSiegeDefenseThreshold || 
                attacker.Unit == eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH)
            {
                return damage;
            }

            // Unarmored: cap at base * 1.5 (or 1.25 for cavalry)
            if (defender.ArmorValue >= 2.0f)
            {
                float cap = attacker.HasTag("Cavalry") 
                    ? attacker.BaseMeleeDamage * DamageConstants.DamageCaps.SwordCavalryUnarmoredCap
                    : attacker.BaseMeleeDamage * DamageConstants.DamageCaps.SwordUnarmoredCap;
                return Math.Min(damage, cap);
            }
            // Light armor: cap at base * 1.25 for weak Sword units
            else if (defender.ArmorValue >= 1.5f)
            {
                if (defender.Unit == eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH || attacker.HasTag("Cavalry"))
                {
                    return Math.Min(damage, attacker.BaseMeleeDamage * DamageConstants.DamageCaps.SwordLightCap);
                }
            }

            return damage;
        }
    }
}

