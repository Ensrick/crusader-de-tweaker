// Config/Toml/Core/Helpers/TypeConverter.cs
//
// PURPOSE: Type conversion utilities for TOML value parsing.
//
// USAGE:
// - TryParseEnum(): Enum parsing from string values (e.g. WeaponType/ArmorType goods)
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just type conversion
// - Returns false on failure (doesn't throw exceptions)
//
using System;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Utilities for converting TOML values to target types.
    ///
    /// Handles type conversion from TOML values to the target types needed by
    /// property handlers (currently enum parsing for the resource-type handlers).
    /// </summary>
    internal static class TypeConverter
    {
        /// <summary>
        /// Safely get an enum value from a string, returning default if parse fails.
        /// </summary>
        public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            result = default;

            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                result = (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
