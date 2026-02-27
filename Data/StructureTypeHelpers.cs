// Data/StructureTypeHelpers.cs
//
// PURPOSE: Provides structure type detection methods for BepInEx damage multipliers.
//
// USAGE:
// - IsWall(): Used by StructureTypeDetector and StructureCostProperties to identify walls
// - IsTower(): Used by StructureTypeDetector to identify towers for TowerDamageTakenMultiplier
// - IsGatehouse(): Used by StructureTypeDetector to identify gatehouses
// - IsCivilStructure(): Used by StructureTypeDetector for CivilStructureDamageTakenMultiplier
//
// IMPORTANT FOR AI AGENTS:
// - These methods are used by BepInEx event handlers to apply different damage multipliers
// - NOT used for damage calculation - only for determining which multiplier to apply
// - Walls use BepInEx cost multipliers, not TOML properties
//
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Helper methods for structure type queries.
    /// 
    /// Extracted from StructureCategories to reduce class size and improve organization.
    /// 
    /// Used by:
    /// - StructureTypeDetector: Detects structure types for BepInEx damage multipliers
    /// - StructureCostProperties: Skips walls (they use BepInEx cost multipliers)
    /// 
    /// These methods are NOT used for damage calculation - only for determining which
    /// BepInEx multiplier to apply during gameplay.
    /// </summary>
    internal static class StructureTypeHelpers
    {
        /// <summary>
        /// Checks if a structure is a wall type.
        /// Only stone and crenel walls are considered (wood walls are deprecated from old Stronghold).
        /// </summary>
        public static bool IsWall(eStructs structure)
        {
            return structure == eStructs.STRUCT_STONE_WALL ||
                   structure == eStructs.STRUCT_CRENAL_WALL;
        }

        /// <summary>
        /// Checks if a structure is a tower type.
        /// Includes both active towers (levels 1-5) and destroyed tower remnants.
        /// </summary>
        public static bool IsTower(eStructs structure)
        {
            return structure == eStructs.STRUCT_TOWER1 ||
                   structure == eStructs.STRUCT_TOWER2 ||
                   structure == eStructs.STRUCT_TOWER3 ||
                   structure == eStructs.STRUCT_TOWER4 ||
                   structure == eStructs.STRUCT_TOWER5 ||
                   structure == eStructs.STRUCT_TOWER1_DESTROYED ||
                   structure == eStructs.STRUCT_TOWER2_DESTROYED ||
                   structure == eStructs.STRUCT_TOWER3_DESTROYED ||
                   structure == eStructs.STRUCT_TOWER4_DESTROYED ||
                   structure == eStructs.STRUCT_TOWER5_DESTROYED;
        }

        /// <summary>
        /// Checks if a structure is a gatehouse type.
        /// Only includes moddable gatehouse structures (not UI placeholders or non-game structures).
        /// Drawbridge is not included as it's not attackable (only cost is modifiable).
        /// </summary>
        public static bool IsGatehouse(eStructs structure)
        {
            // Only check moddable gatehouses (exclude structures in NonModable)
            // Drawbridge is excluded as it's not attackable
            return structure == eStructs.STRUCT_GATE_MAIN ||
                   structure == eStructs.STRUCT_GATE_INNER;
        }

        /// <summary>
        /// Checks if a structure is a civilian structure (not a fortification).
        /// Civilian structures are those that are NOT towers and NOT gatehouses.
        /// </summary>
        public static bool IsCivilStructure(eStructs structure)
        {
            // Civil structures are anything that's not a tower and not a gatehouse
            return !IsTower(structure) && !IsGatehouse(structure);
        }
    }
}

