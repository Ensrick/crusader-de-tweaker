// Config/DamageMatrix/Core/BuildingMatrixHelper.cs
// AI DEV: Consolidates building filtering for damage matrix operations (mirrors UnitMatrixHelper).
// Used by BuildingFireDamage generator and loader to get/filter modifiable buildings.

using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Helper class to consolidate building filtering for damage matrix operations.
    /// Mirrors UnitMatrixHelper but for eStructs.
    /// </summary>
    internal static class BuildingMatrixHelper
    {
        private static readonly HashSet<eStructs> _nonModableSet =
            new HashSet<eStructs>(StructureCategories.NonModable);

        /// <summary>
        /// Get all modifiable buildings, ordered alphabetically.
        /// </summary>
        public static eStructs[] GetModifiableBuildings()
        {
            return Enum.GetValues(typeof(eStructs))
                .Cast<eStructs>()
                .Where(s => !_nonModableSet.Contains(s))
                .OrderBy(s => s.ToString())
                .ToArray();
        }

        /// <summary>
        /// Check if a building should be skipped (non-modifiable).
        /// </summary>
        public static bool ShouldSkipBuilding(eStructs building)
        {
            return _nonModableSet.Contains(building);
        }
    }
}
