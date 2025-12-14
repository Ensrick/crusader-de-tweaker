// Systems/DamageCalculation/WeaponVsArmorLookupTable.cs
using System.Collections.Generic;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Weapon vs Armor multiplier lookup table.
    /// Maps (WeaponCategory, ArmorCategory) to damage multipliers.
    /// Derived from analyzing CrusaderDETweaker_MeleeDamage.csv game data.
    /// </summary>
    internal static class WeaponVsArmorLookupTable
    {
        /// <summary>
        /// Weapon vs Armor multiplier lookup table.
        /// Key: (WeaponCategory, ArmorCategory), Value: Damage multiplier
        /// </summary>
        public static readonly Dictionary<(WeaponCategory, ArmorCategory), float> Table = new Dictionary<(WeaponCategory, ArmorCategory), float>
        {
            // UNARMED (civilians, weak units) - flat base damage to everyone
            { (WeaponCategory.Unarmed, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Siege), 1.0f },

            // SWORD - good all-around
            { (WeaponCategory.Sword, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Siege), 0.4f },

            // MACE - terrible vs heavy armor
            { (WeaponCategory.Mace, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Heavy), 0.33f },
            { (WeaponCategory.Mace, ArmorCategory.Siege), 0.5f },

            // POLEARM - ignores heavy armor
            { (WeaponCategory.Polearm, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Siege), 0.4f },

            // LANCE (Cavalry) - bonus vs heavy
            { (WeaponCategory.Lance, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Heavy), 1.25f },
            { (WeaponCategory.Lance, ArmorCategory.Siege), 0.4f },

            // AXE - good vs siege
            { (WeaponCategory.Axe, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Siege), 1.0f },  // Bypasses siege defense

            // DAGGER (Assassin) - huge bonus vs unarmored, ignores heavy reduction
            { (WeaponCategory.Dagger, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Light), 2.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Siege), 0.025f },

            // RANGED (bows, crossbows, slings in melee) - bonus vs heavy and light
            { (WeaponCategory.Ranged, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Ranged, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Heavy), 1.5f },
            { (WeaponCategory.Ranged, ArmorCategory.Siege), 0.2f },

            // BEAST - flat damage, ignores all armor
            { (WeaponCategory.Beast, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Siege), 1.0f },
        };

        /// <summary>
        /// Look up the weapon vs armor multiplier from the table.
        /// </summary>
        public static float GetMultiplier(WeaponCategory weapon, ArmorCategory armor)
        {
            if (Table.TryGetValue((weapon, armor), out float multiplier))
            {
                return multiplier;
            }
            return 1.0f; // Default if not found
        }
    }
}

