// Config/Toml/Units/Properties/ArmorMultiplierPropertyBase.cs
//
// PURPOSE: Shared base class for armor multiplier properties (Melee and Ranged).
//
// ARCHITECTURE:
// - Provides common validation, parsing, and formatting logic
// - Reduces code duplication between MeleeArmorMultiplierProperty and RangedArmorMultiplierProperty
// - Subclasses implement specific TryGetFromAPI and SetToAPI methods
// - Mismatch tracking remains in subclasses (static fields can't be shared via inheritance)
//
using System;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Base class for armor multiplier properties (Melee and Ranged).
    /// Provides shared validation, formatting, and utility methods.
    /// </summary>
    internal abstract class ArmorMultiplierPropertyBase : PropertyHandler<eChimps, float>
    {
        protected ArmorMultiplierPropertyBase(string propertyName) : base(propertyName)
        {
        }

        /// <summary>
        /// Validate the armor multiplier.
        /// Range: 0.01 (extremely heavy armor) to 10.0 (extremely vulnerable)
        /// </summary>
        internal override bool ValidateValue(float multiplier)
        {
            return multiplier > 0.0f && multiplier <= 10.0f;
        }

        /// <summary>
        /// Determine if this property can be applied to the given unit.
        /// Only apply to modifiable units (not civilians, special units, etc.)
        /// </summary>
        internal override bool CanApplyTo(eChimps unit)
        {
            return !UnitCategories.IsNonModifiable(unit);
        }

        /// <summary>
        /// Try to get the original/default armor multiplier for this unit.
        /// Uses the current API value as default when generating config.
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out float defaultValue)
        {
            return TryGetFromAPI(unit, out defaultValue);
        }

        /// <summary>
        /// Parse armor multiplier from TOML value.
        /// Expects a float value.
        /// </summary>
        internal override bool TryParseValue(object tomlValue, out float result)
        {
            if (TypeConverter.TryConvertToFloat(tomlValue, out result))
            {
                return true;
            }
            
            result = 1.0f;
            return false;
        }

        /// <summary>
        /// Format the armor multiplier for TOML output.
        /// Shows 2 decimal places to allow user customization with sufficient precision.
        /// </summary>
        internal override string FormatValue(float multiplier)
        {
            return $"{multiplier:F2}";
        }

        /// <summary>
        /// Round a calculated damage value to the specified granularity.
        /// </summary>
        protected static int RoundDamage(int damage, int granularity)
        {
            return (int)(Math.Round(damage / (double)granularity) * granularity);
        }

        /// <summary>
        /// Round a multiplier to 1 decimal place for cleaner config output.
        /// </summary>
        protected static float RoundMultiplier(float multiplier)
        {
            return (float)Math.Round(multiplier, 1);
        }
    }
}
