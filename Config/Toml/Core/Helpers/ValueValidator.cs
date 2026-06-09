// Config/Toml/Core/ValueValidator.cs
//
// PURPOSE: Value validation utilities for TOML property values.
//
// USAGE:
// - IsInRange(): Validates numeric values are within min/max range
// - IsPositive(): Validates values are positive (greater than zero)
// - IsNonNegative(): Validates values are non-negative (zero or positive)
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just validation
// - Used by property handlers in ValidateValue() methods
// - Returns boolean (true = valid, false = invalid)
// - Extracted from ConfigHelpers to improve modularity
//
namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Utilities for validating property values.
    /// 
    /// Provides common validation operations for TOML property values:
    /// - Range validation (min/max)
    /// - Positive value validation
    /// - Non-negative value validation
    /// 
    /// Used by property handlers in ValidateValue() methods.
    /// </summary>
    internal static class ValueValidator
    {
        /// <summary>
        /// Validate that a numeric value is within a range.
        /// </summary>
        public static bool IsInRange<T>(T value, T min, T max) where T : System.IComparable<T>
        {
            return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
        }

        /// <summary>
        /// Validate that a value is positive (greater than zero).
        /// </summary>
        public static bool IsPositive(int value)
        {
            return value > 0;
        }

        /// <summary>
        /// Validate that a value is positive (greater than zero).
        /// </summary>
        public static bool IsPositive(uint value)
        {
            return value > 0;
        }

        /// <summary>
        /// Validate that a value is positive (greater than zero).
        /// </summary>
        public static bool IsPositive(float value)
        {
            return value > 0f;
        }

        /// <summary>
        /// Validate that a value is non-negative (greater than or equal to zero).
        /// </summary>
        public static bool IsNonNegative(int value)
        {
            return value >= 0;
        }

        /// <summary>
        /// Validate that a value is non-negative (greater than or equal to zero).
        /// </summary>
        public static bool IsNonNegative(float value)
        {
            return value >= 0f;
        }
    }
}

