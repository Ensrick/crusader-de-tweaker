// Systems/DamageCalculation/RangedMeleeDamageCalculator.cs
using System;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Damage calculator for Ranged units in melee combat.
    /// Uses a different formula when fighting in melee.
    /// </summary>
    internal class RangedMeleeDamageCalculator : BaseWeaponDamageCalculator
    {
        public override float CalculateBaseDamage(UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory, ArmorCategory armorCategory)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;
            bool defenderIsRanged = defender.IsRangedUnit;
            
            float multiplier;
            if (armorValue >= 2.0f)
            {
                // Unarmored: 1.0x multiplier
                multiplier = 1.0f;
            }
            else if (armorValue >= 1.5f)
            {
                // Light armor: 1.33x if defender is ranged, 1.0x if not
                multiplier = defenderIsRanged ? DamageConstants.RangedMelee.LightArmorRangedDefenderMultiplier : 1.0f;
            }
            else if (armorValue >= 1.0f)
            {
                // Medium armor: Always 1.0x multiplier
                multiplier = 1.0f;
            }
            else
            {
                // Heavy (0.5): 3.0x
                multiplier = DamageConstants.RangedMelee.HeavyArmorMultiplier;
            }
            
            float damage = baseDamage * armorValue * multiplier;
            
            // Apply tag vs tag modifiers (Hunter vs Beast, etc.) BEFORE caps
            // This allows caps to account for tag modifiers
            float tagModifier = TagVsTagModifierHelper.GetModifier(attacker, defender);
            damage *= tagModifier;
            
            return damage;
        }

        public override float ApplyDamageCaps(float damage, UnitDamageData attacker, UnitDamageData defender, WeaponCategory weaponCategory)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;
            bool defenderIsRanged = defender.IsRangedUnit;
            bool isHunter = attacker.HasTag("Hunter");
            bool isBedouinAmbusher = attacker.Unit == eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER;
            
            float cap;
            if (armorValue >= 2.0f)
            {
                // Unarmored: cap varies by unit
                if (isHunter)
                {
                    cap = defender.Unit == eChimps.CHIMP_TYPE_BEDOUIN_HEALER
                        ? baseDamage * DamageConstants.DamageCaps.RangedHunterLightCap
                        : baseDamage * DamageConstants.DamageCaps.RangedHunterUnarmoredCap;
                }
                else if (isBedouinAmbusher)
                {
                    cap = baseDamage * 2.0f; // Allow up to 2.0x
                }
                else
                {
                    cap = baseDamage <= 10 
                        ? baseDamage * DamageConstants.DamageCaps.RangedUnarmoredSmallCap 
                        : baseDamage * DamageConstants.DamageCaps.RangedUnarmoredLargeCap;
                }
            }
            else if (armorValue >= 1.5f)
            {
                // Light armor: cap varies by unit
                if (isHunter)
                {
                    if (defender.HasTag("Beast"))
                    {
                        cap = baseDamage * 1.0f; // HUNTER vs Beast: cap at base
                    }
                    else if (defender.Unit == eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH)
                    {
                        cap = baseDamage * 1.0f; // HUNTER vs BEDOUIN_EUNUCH: cap at base
                    }
                    else
                    {
                        cap = baseDamage * DamageConstants.DamageCaps.RangedHunterLightCap;
                    }
                }
                else if (isBedouinAmbusher)
                {
                    cap = baseDamage * 1.0f; // BEDOUIN_AMBUSHER: cap at base
                }
                else if (baseDamage <= 10 && !defenderIsRanged)
                {
                    cap = baseDamage * 1.0f; // Small ranged units vs non-ranged Light armor: cap at base
                }
                else
                {
                    cap = baseDamage <= 10 
                        ? baseDamage * DamageConstants.DamageCaps.RangedLightSmallCap 
                        : baseDamage * DamageConstants.DamageCaps.RangedLightLargeCap;
                }
            }
            else if (armorValue >= 1.0f)
            {
                // Medium armor: no cap (except for HUNTER vs Beast)
                if (isHunter && defender.HasTag("Beast"))
                {
                    // CAMEL and CROCODILE should be capped even though they're large beasts
                    if (defender.Unit == eChimps.CHIMP_TYPE_CAMEL || 
                        defender.Unit == eChimps.CHIMP_TYPE_CROCODILE ||
                        defender.BaseMeleeDamage < 50)
                    {
                        cap = baseDamage * 1.0f; // Cap at base for CAMEL, CROCODILE, and small beasts
                    }
                    else
                    {
                        cap = float.MaxValue; // No cap for HYENA, LION, WAR_DOG
                    }
                }
                else
                {
                    cap = float.MaxValue;
                }
            }
            else
            {
                // Heavy armor (0.5): cap at base damage for small units
                cap = baseDamage <= 10 ? baseDamage * 1.0f : float.MaxValue;
            }
            
            return Math.Min(damage, cap);
        }
    }
}

