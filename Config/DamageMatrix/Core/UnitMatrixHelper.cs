// Config/DamageMatrix/Core/UnitMatrixHelper.cs
//
// PURPOSE: Consolidates common unit filtering patterns for damage matrix operations.
//
// USAGE:
// - GetModifiableUnits(): Used by all matrix generators to get list of units for CSV headers
// - ShouldSkipUnit(): Used by all matrix loaders to skip non-modifiable units
//
// IMPORTANT FOR AI AGENTS:
// - This helper eliminates duplication - all matrix generators/loaders need to filter units the same way
// - Uses UnitCategories.IsNonModifiable() for consistent filtering logic
// - GetModifiableUnits() orders units alphabetically for consistent CSV file generation
//
using System;
using System.Linq;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Helper class to consolidate common patterns for unit-based damage matrices.
    /// 
    /// Eliminates duplication across Melee, Ranged, and EunuchAoe matrix generators/loaders.
    /// 
    /// All damage matrices work with units as defenders, so they all need:
    /// - List of modifiable units (for CSV headers)
    /// - Logic to skip non-modifiable units (during CSV loading)
    /// 
    /// This class centralizes that logic to ensure consistency.
    /// </summary>
    internal static class UnitMatrixHelper
    {
        /// <summary>
        /// Get all modifiable units, ordered alphabetically.
        /// Used by all matrix generators that work with unit defenders.
        /// </summary>
        public static eChimps[] GetModifiableUnits()
        {
            return Enum.GetValues(typeof(eChimps))
                .Cast<eChimps>()
                .Where(unit => !Data.UnitCategories.IsNonModifiable(unit))
                .OrderBy(unit => unit.ToString())
                .ToArray();
        }

        /// <summary>
        /// Check if a unit should be skipped (non-modifiable).
        /// Used by all matrix loaders that work with unit defenders.
        /// </summary>
        public static bool ShouldSkipUnit(eChimps unit)
        {
            return Data.UnitCategories.IsNonModifiable(unit);
        }
    }
}

