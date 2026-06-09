// Config/BepInEx/Core/BepInExConfigHelper.cs
//
// PURPOSE: Consolidates common BepInEx config validation patterns.
//
// USAGE:
// - ValidateMultiplier(): Validates a single multiplier value
// - ValidateMultipliers(): Validates multiple multiplier values at once
//
// IMPORTANT FOR AI AGENTS:
// - This is a helper class to eliminate duplication across BepInEx config systems
// - Validates multiplier values (checks for negative, NaN, Infinity)
// - Logs warnings/errors for invalid values
// - Used by all multiplier config systems (UnitMultipliersConfig, StructureMultipliersConfig, etc.)
//
using BepInEx.Configuration;

namespace CrusaderDETweaker.Config.BepInEx.Core
{
    /// <summary>
    /// Helper class to consolidate common BepInEx config patterns and eliminate duplication.
    /// 
    /// Provides validation utilities for multiplier values used across all BepInEx config systems.
    /// Used by UnitMultipliersConfig, StructureMultipliersConfig, and WallCostConfig.
    /// </summary>
    internal static class BepInExConfigHelper
    {
        /// <summary>
        /// Validates a multiplier value and logs warnings/errors for invalid values.
        /// This method is used by all multiplier config systems to ensure consistent validation.
        /// </summary>
        /// <param name="name">The name of the multiplier (for logging)</param>
        /// <param name="value">The multiplier value to validate</param>
        public static void ValidateMultiplier(string name, float value)
        {
            if (value < 0.0f)
            {
                Plugin.Logger.LogWarning($"{name} is negative ({value}). Negative multipliers may cause unexpected behavior. Consider using a positive value.");
            }
            else if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Plugin.Logger.LogError($"{name} is invalid ({value}). Using default value of 1.0.");
            }
        }

        /// <summary>
        /// Validates multiple ConfigEntry multipliers and logs warnings/errors for invalid values.
        /// Convenience method for validating multiple entries at once.
        /// </summary>
        /// <param name="entries">Array of (name, entry) tuples to validate</param>
        public static void ValidateMultipliers(params (string name, ConfigEntry<float> entry)[] entries)
        {
            foreach (var (name, entry) in entries)
            {
                ValidateMultiplier(name, entry.Value);
            }
        }
    }
}

