// Systems/DamageCalculation/WeaponDamageCalculatorFactory.cs
using System.Collections.Generic;

namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Factory for creating weapon-specific damage calculators.
    /// Uses singleton instances for each weapon type.
    /// </summary>
    internal static class WeaponDamageCalculatorFactory
    {
        private static readonly Dictionary<WeaponCategory, IWeaponDamageCalculator> _calculators = new Dictionary<WeaponCategory, IWeaponDamageCalculator>
        {
            { WeaponCategory.Sword, new SwordDamageCalculator() },
            { WeaponCategory.Mace, new MaceDamageCalculator() },
            { WeaponCategory.Polearm, new PolearmDamageCalculator() },
            { WeaponCategory.Lance, new LanceDamageCalculator() },
            { WeaponCategory.Axe, new AxeDamageCalculator() },
            { WeaponCategory.Dagger, new DaggerDamageCalculator() },
            { WeaponCategory.Ranged, new RangedMeleeDamageCalculator() },
            { WeaponCategory.Unarmed, new UnarmedDamageCalculator() },
            { WeaponCategory.Beast, new BeastDamageCalculator() },
        };

        /// <summary>
        /// Get the damage calculator for a specific weapon category.
        /// </summary>
        public static IWeaponDamageCalculator GetCalculator(WeaponCategory weaponCategory)
        {
            if (_calculators.TryGetValue(weaponCategory, out var calculator))
            {
                return calculator;
            }
            
            // Default: use standard calculator
            return new StandardWeaponDamageCalculator();
        }
    }
}

