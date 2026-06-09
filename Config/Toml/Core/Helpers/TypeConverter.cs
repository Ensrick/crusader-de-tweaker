// Config/Toml/Core/TypeConverter.cs
//
// PURPOSE: Type conversion utilities for TOML value parsing.
//
// USAGE:
// - TryConvert(): Generic type conversion from TOML values
// - TryConvertToFloat(): Special handling for float conversion
// - TryParseEnum(): Enum parsing from string values
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just type conversion
// - Handles common conversions: direct cast, numeric conversion, enum parsing
// - Returns false on failure (doesn't throw exceptions)
// - Used by ConfigLoader when parsing TOML values
// - Extracted from ConfigHelpers to improve modularity
//
using System;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Utilities for converting TOML values to target types.
    /// 
    /// Handles type conversion from TOML values (which are typically strings or numbers)
    /// to the target types needed by property handlers.
    /// 
    /// Used by ConfigLoader when parsing TOML property values.
    /// </summary>
    internal static class TypeConverter
    {
        /// <summary>
        /// Try to convert a TOML value to the target type.
        /// </summary>
        public static bool TryConvert<T>(object value, out T result)
        {
            result = default;

            if (value == null)
                return false;

            try
            {
                // Direct cast if types match
                if (value is T directValue)
                {
                    result = directValue;
                    return true;
                }

                // Use Convert for numeric types
                result = (T)Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Failed to convert value '{value}' to type {typeof(T).Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to convert a TOML value object to a float.
        /// Handles common numeric types that TOML might return.
        /// </summary>
        public static bool TryConvertToFloat(object value, out float result)
        {
            result = 0f;

            switch (value)
            {
                case double doubleValue:
                    result = (float)doubleValue;
                    return true;
                case float floatValue:
                    result = floatValue;
                    return true;
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                default:
                    return false;
            }
        }

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

