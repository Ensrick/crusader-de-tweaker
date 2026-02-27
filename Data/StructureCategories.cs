// Data/StructureCategories.cs
//
// PURPOSE: Provides structure categorization for filtering and type detection.
//
// USAGE:
// - NonModable: Used by all config systems to skip non-modifiable structures (UI placeholders, ruins, etc.)
// - IsWall/IsTower/IsGatehouse/IsCivilStructure: Used by BepInEx damage multipliers and TOML cost properties
//
// IMPORTANT FOR AI AGENTS:
// - NonModable determines which structures are skipped during config loading
// - Structure type helpers (delegated to StructureTypeHelpers) are used for BepInEx multiplier logic
// - Walls are in NonModable because they use BepInEx cost multipliers, not TOML properties
//
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Categorizes structures for filtering and type detection.
    /// 
    /// Purpose:
    /// - NonModable: Lists structures that should be skipped during config loading (UI placeholders, ruins, etc.)
    /// - Structure type queries: Delegated to StructureTypeHelpers for BepInEx multiplier logic
    /// 
    /// Note: Walls are in NonModable because they use BepInEx cost multipliers, not TOML properties.
    /// </summary>
    internal static class StructureCategories
    {
        // ========================================
        // NON-MODIFIABLE STRUCTURES
        // ========================================

        /// <summary>
        /// Structures that cannot or should not be modified.
        /// </summary>
        internal static readonly eStructs[] NonModable =
        {
            eStructs.STRUCT_NULL,
            eStructs.STRUCT_QUARRYPILE, // Can't see any modifiable property this would have yet
            eStructs.STRUCT_RUINS,
            eStructs.STRUCT_KEEP_ONE,
            eStructs.STRUCT_KEEP_TWO,
            eStructs.STRUCT_KEEP_THREE,
            eStructs.STRUCT_KEEP_FOUR,
            eStructs.STRUCT_KEEP_FIVE,
            eStructs.STRUCT_GATE_POSTERN, //Not sure, maybe UI for some gatehouse option
            eStructs.STRUCT_SIGNPOST,
            eStructs.STRUCT_GATEHOUSE, //Gatehouse UI button, probably
            eStructs.STRUCT_TOWER, //Tower UI button, probably
            eStructs.STRUCT_KEEPDOOR_LEFT, // UI probably
            eStructs.STRUCT_KEEPDOOR_RIGHT,
            eStructs.STRUCT_KEEPDOOR,
            eStructs.STRUCT_TUNNEL_CONSTRUCTION,
            eStructs.STRUCT_DOCK,
            eStructs.STRUCT_MAX, // UI probably
            eStructs.STRUCT_STONE_WALL, // Walls use global cost multipliers (BepInEx config), not regular properties
            eStructs.STRUCT_CRENAL_WALL, // Walls use global cost multipliers (BepInEx config), not regular properties
            eStructs.STRUCT_WOOD_WALL, // Deprecated from old Stronghold, not used in Crusader
            eStructs.STRUCT_WAS_WALL,
            eStructs.STRUCT_BEE_HIVE,
            eStructs.STRUCT_STAIRS,
            eStructs.STRUCT_BRAZIER,
            eStructs.STRUCT_MANGONEL,
            eStructs.STRUCT_BALLISTA,
            eStructs.STRUCT_HEAD_ON_SPIKE,
            eStructs.STRUCT_GARDEN_SMALL,
            eStructs.STRUCT_GARDEN_MED,
            eStructs.STRUCT_GARDEN_LARGE,
            eStructs.STRUCT_POND_SMALL,
            eStructs.STRUCT_POND_LARGE,
            eStructs.STRUCT_FLAG1,
            eStructs.STRUCT_FLAG2,
            eStructs.STRUCT_FLAG3,
            eStructs.STRUCT_FLAG4,
            eStructs.STRUCT_GATE_WOOD1A,
            eStructs.STRUCT_GATE_WOOD1B,
            eStructs.STRUCT_GATE_WOOD1C,
            eStructs.STRUCT_GATE_WOOD1D,
            eStructs.STRUCT_GATE_STONE1A,
            eStructs.STRUCT_GATE_STONE1B,
            eStructs.STRUCT_GATE_STONE2A,
            eStructs.STRUCT_GATE_STONE2B,
            eStructs.STRUCT_RUINS01,
            eStructs.STRUCT_RUINS02,
            eStructs.STRUCT_RUINS03,
            eStructs.STRUCT_RUINS04,
            eStructs.STRUCT_RUINS05,
            eStructs.STRUCT_RUINS06,
            eStructs.STRUCT_RUINS07,
            eStructs.STRUCT_RUINS08,
            eStructs.STRUCT_RUINS09,
            eStructs.STRUCT_RUINS10,
            eStructs.STRUCT_RUINS11,
            eStructs.STRUCT_RUINS12,
            eStructs.STRUCT_RUINS13,
            eStructs.STRUCT_RUINS14,
            eStructs.STRUCT_RUINS15,
            eStructs.STRUCT_RUINS16,
            eStructs.STRUCT_RUINS17,
            eStructs.STRUCT_RUINS18,
            eStructs.STRUCT_RUINS19,
            eStructs.STRUCT_RUINS20,
            eStructs.STRUCT_RUINS21,
            eStructs.STRUCT_RUINS22,
            eStructs.STRUCT_RUINS23,
            eStructs.STRUCT_RUINS24,
            eStructs.STRUCT_RUINS25,
            eStructs.STRUCT_RUINS26,
            eStructs.STRUCT_RUINS27,
            eStructs.STRUCT_RUINS28,
            eStructs.STRUCT_RUINS29,
            eStructs.STRUCT_RUINS30,
            eStructs.STRUCT_RUINS31,
            eStructs.STRUCT_RUINS32,
            eStructs.STRUCT_RUINS33,
            eStructs.STRUCT_RUINS34,
            eStructs.STRUCT_PEOPLE_ARCHERS,
            eStructs.STRUCT_PEOPLE_SPEARMEN,
            eStructs.STRUCT_PEOPLE_PIKEMEN,
            eStructs.STRUCT_PEOPLE_MACEMEN,
            eStructs.STRUCT_PEOPLE_XBOWMEN,
            eStructs.STRUCT_PEOPLE_SWORDSMEN,
            eStructs.STRUCT_PEOPLE_KNIGHTS,
            eStructs.STRUCT_PEOPLE_LADDERMEN,
            eStructs.STRUCT_PEOPLE_ENGINEERS,
            eStructs.STRUCT_PEOPLE_ENGINEERS_POTS,
            eStructs.STRUCT_PEOPLE_MONKS,
            eStructs.STRUCT_PEOPLE_CATAPULTS,
            eStructs.STRUCT_PEOPLE_TREBUCHETS,
            eStructs.STRUCT_PEOPLE_BATTERING_RAMS,
            eStructs.STRUCT_PEOPLE_SIEGE_TOWERS,
            eStructs.STRUCT_PEOPLE_PORTABLE_SHIELDS,
            eStructs.STRUCT_PEOPLE_TUNNELERS,
            eStructs.STRUCT_PEOPLE_ARAB_BOW,
            eStructs.STRUCT_PEOPLE_ARAB_SLAVE,
            eStructs.STRUCT_PEOPLE_ARAB_SLINGER,
            eStructs.STRUCT_PEOPLE_ARAB_ASSASIN,
            eStructs.STRUCT_PEOPLE_ARAB_HORSEMAN,
            eStructs.STRUCT_PEOPLE_ARAB_SWORDSMAN,
            eStructs.STRUCT_PEOPLE_ARAB_GRENADIER,
            eStructs.STRUCT_PEOPLE_ARAB_BALLISTA,
            eStructs.STRUCT_PEOPLE_BEDOUIN_CAMEL_LANCER,
            eStructs.STRUCT_PEOPLE_BEDOUIN_HEALER,
            eStructs.STRUCT_PEOPLE_BEDOUIN_EUNUCH,
            eStructs.STRUCT_PEOPLE_BEDOUIN_AMBUSHER,
            eStructs.STRUCT_PEOPLE_BEDOUIN_SKIRMISHER,
            eStructs.STRUCT_PEOPLE_BEDOUIN_HEAVY_CAMEL,
            eStructs.STRUCT_PEOPLE_BEDOUIN_SAPPER,
            eStructs.STRUCT_PEOPLE_BEDOUIN_DEMOLISHER,
            eStructs.STRUCT_NEW_DIG_MOAT,
            eStructs.STRUCT_NEW_FILL_MOAT,
            eStructs.STRUCT_MARKER_POINT1,
            eStructs.STRUCT_MARKER_POINT2,
            eStructs.STRUCT_MARKER_POINT3,
            eStructs.STRUCT_MARKER_POINT4,
            eStructs.STRUCT_MARKER_POINT5,
            eStructs.STRUCT_MARKER_POINT6,
            eStructs.STRUCT_MARKER_POINT7,
            eStructs.STRUCT_MARKER_POINT8,
            eStructs.STRUCT_MARKER_POINT9,
            eStructs.STRUCT_MARKER_POINT10,
            eStructs.STRUCT_POND5,
            eStructs.STRUCT_POND6,
            eStructs.STRUCT_POND7,
            eStructs.STRUCT_POND8,
            eStructs.STRUCT_IN_REPORTS,
            eStructs.STRUCT_SUB_MENU_TOWERS,
            eStructs.STRUCT_SUB_MENU_MILITARY,
            eStructs.STRUCT_SUB_MENU_GATEHOUSES,
            eStructs.STRUCT_SUB_MENU_GATEHOUSES_STONESMALL,
            eStructs.STRUCT_SUB_MENU_GATEHOUSES_WOOD,
            eStructs.STRUCT_SUB_MENU_GATEHOUSES_STONESMALL,
            eStructs.STRUCT_SUB_MENU_GATEHOUSES_STONELARGE,
            eStructs.STRUCT_SUB_MENU_KEEPS,
            eStructs.STRUCT_SUB_MENU_GOOD,
            eStructs.STRUCT_SUB_MENU_BAD,
            eStructs.STRUCT_NEW_EDITOR_DELETE,
            eStructs.STRUCT_MENU_RETURN_TOWERS,
            eStructs.STRUCT_SUB_MENU_BAD,
            eStructs.STRUCT_MENU_RETURN_GOOD,
            eStructs.STRUCT_MENU_RETURN_BAD,
            eStructs.STRUCT_MENU_RETURN_GATEHOUSES,
            eStructs.STRUCT_MENU_RETURN_MILITARY,
            eStructs.STRUCT_MENU_RETURN_KEEPS,
            eStructs.STRUCT_NEW_DELETE,
            eStructs.STRUCT_TUNNEL_ENTERANCE,
            eStructs.STRUCT_PARADEGROUND_OIL,
            eStructs.STRUCT_PARADEGROUND_ENG,
            eStructs.STRUCT_CAMPGROUND,
            eStructs.STRUCT_PARADEGROUND_MISS,
            eStructs.STRUCT_PARADEGROUND_LGT,
            eStructs.STRUCT_PARADEGROUND_HVY,
            eStructs.STRUCT_PARADEGROUND_TUN,
            eStructs.STRUCT_GATE_WOOD, // not in the game
            eStructs.STRUCT_GOODS_YARD, // no modable properties, unless I guess someone wants to add a cost or something
            // add structures that aren't fully supported by SHCDE-SE or modifaible
        };

        // ========================================
        // STRUCTURE TYPE QUERIES
        // ========================================
        // Delegated to StructureTypeHelpers for better organization
        // ========================================

        /// <summary>
        /// Checks if a structure is a wall type.
        /// </summary>
        internal static bool IsWall(eStructs structure) => StructureTypeHelpers.IsWall(structure);

        /// <summary>
        /// Checks if a structure is a tower type.
        /// </summary>
        internal static bool IsTower(eStructs structure) => StructureTypeHelpers.IsTower(structure);

        /// <summary>
        /// Checks if a structure is a gatehouse type.
        /// </summary>
        internal static bool IsGatehouse(eStructs structure) => StructureTypeHelpers.IsGatehouse(structure);

        /// <summary>
        /// Checks if a structure is a civilian structure (not a fortification).
        /// </summary>
        internal static bool IsCivilStructure(eStructs structure) => StructureTypeHelpers.IsCivilStructure(structure);
    }
}

