// Data/UnitCategories.cs
// 
// PURPOSE: Provides unit categorization for filtering and skipping logic throughout the codebase.
// 
// USAGE:
// - IsNonModifiable(): Used by all config systems to skip non-modifiable units (UI placeholders, special units, etc.)
// - Category arrays (Crusader, Arab, Bedouin, etc.): Used for reference/organization only, not for damage calculation
// 
// IMPORTANT FOR AI AGENTS:
// - This is NOT used for damage calculation - damage is configured via CSV matrices only
// - Categories are for organizational purposes and filtering logic only
// - NonModable list determines which units are skipped during config loading
//
using System.Collections.Generic;
using System.Linq;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Categorizes units for filtering and organizational purposes.
    /// 
    /// CRITICAL: This class is NOT used for damage calculation. Damage is configured exclusively via CSV matrices.
    /// 
    /// Purpose:
    /// - Provides IsNonModifiable() for skipping non-modifiable units during config loading
    /// - Provides category arrays (Crusader, Arab, Bedouin, etc.) for reference/organization only
    /// 
    /// Categories are based on recruitment source and unit type, NOT damage patterns.
    /// </summary>
    internal static class UnitCategories
    {
        // ========================================
        // NON-MODIFIABLE UNITS
        // ========================================

        /// <summary>
        /// Units that cannot or should not be modified.
        /// Using HashSet for O(1) lookup performance.
        /// </summary>
        private static readonly HashSet<eChimps> _nonModableSet = new HashSet<eChimps>
        {
            eChimps.CHIMP_NUM_TYPES,
            eChimps.CHIMP_TYPE_NULL,
            eChimps.CHIMP_TYPE_MOTHER,
            eChimps.CHIMP_TYPE_CHILD,
            eChimps.CHIMP_TYPE_BURNING_MAN,
            eChimps.CHIMP_TYPE_BURNING_ANIMAL_BIG,
            eChimps.CHIMP_TYPE_BURNING_ANIMAL_SMALL,
            eChimps.CHIMP_TYPE_ARCHER_debug,
            eChimps.CHIMP_TYPE_GHOST,  // Special: takes 1-10 melee, 50 EunuchAOE
            eChimps.CHIMP_SIEGE_TENT, // Unknown purpose. Possibly a UI placeholder.
        };

        /// <summary>
        /// Units that cannot or should not be modified.
        /// Array version for backward compatibility (if needed for iteration).
        /// </summary>
        internal static readonly eChimps[] NonModable = _nonModableSet.ToArray();

        /// <summary>
        /// Check if a unit is non-modifiable (O(1) lookup).
        /// </summary>
        internal static bool IsNonModifiable(eChimps unit)
        {
            return _nonModableSet.Contains(unit);
        }

        // ========================================
        // SPECIAL UNIT EXCEPTIONS
        // ========================================

        /// <summary>
        /// Units that don't attack in melee. These should be excluded from melee damage output.
        /// Arab Ballista doesn't have a melee attack animation and should not appear in melee CSV.
        /// </summary>
        private static readonly HashSet<eChimps> _nonMeleeAttackers = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
        };

        /// <summary>
        /// Check if a unit cannot attack in melee (should be excluded from melee damage output).
        /// </summary>
        internal static bool IsNonMeleeAttacker(eChimps unit)
        {
            return _nonMeleeAttackers.Contains(unit);
        }

        /// <summary>
        /// Units with special ranged damage floor (minimum 10 instead of rounding to 50).
        /// Mantlet (Portable Shield) has a floor of 10 for ranged damage.
        /// </summary>
        private static readonly HashSet<eChimps> _rangedFloorUnits = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_PORTABLE_SHIELD,
        };

        /// <summary>
        /// Check if a unit has special ranged damage floor (floor of 10, not rounded to 50).
        /// </summary>
        internal static bool HasSpecialRangedFloor(eChimps unit)
        {
            return _rangedFloorUnits.Contains(unit);
        }

        // ========================================
        // RECRUITMENT CATEGORIES
        // ========================================

        /// <summary>
        /// European/Crusader units recruited from Barracks.
        /// </summary>
        internal static readonly eChimps[] Crusader =
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
        };

        /// <summary>
        /// Arab units recruited from Mercenary Post.
        /// </summary>
        internal static readonly eChimps[] Arab =
        {
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLAVE,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
        };

        /// <summary>
        /// Bedouin units recruited from Mercenary Post.
        /// </summary>
        internal static readonly eChimps[] Bedouin =
        {
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
        };

        /// <summary>
        /// Religious units (Monk from Cathedral, Temple Guard from Grand Mosque).
        /// </summary>
        internal static readonly eChimps[] Religious =
        {
            eChimps.CHIMP_TYPE_MONK,
        };

        /// <summary>
        /// Support units recruited from Engineer's Guild.
        /// </summary>
        internal static readonly eChimps[] Support =
        {
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_LADDERMAN,
            eChimps.CHIMP_TYPE_TUNNELER,
        };

        /// <summary>
        /// All units that can be recruited with gold cost.
        /// Using HashSet for O(1) lookup performance.
        /// </summary>
        private static readonly HashSet<eChimps> _recruitableSet = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLAVE,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_MONK,
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_LADDERMAN,
            eChimps.CHIMP_TYPE_TUNNELER,
        };

        /// <summary>
        /// All units that can be recruited with gold cost.
        /// Array version for backward compatibility (if needed for iteration).
        /// </summary>
        internal static readonly eChimps[] Recruitable = _recruitableSet.ToArray();

        /// <summary>
        /// Check if a unit is recruitable (O(1) lookup).
        /// </summary>
        internal static bool IsRecruitable(eChimps unit)
        {
            return _recruitableSet.Contains(unit);
        }

        /// <summary>
        /// Siege equipment built from siege tents (not recruited with gold).
        /// </summary>
        internal static readonly eChimps[] SiegeEquipment =
        {
            eChimps.CHIMP_TYPE_CATAPULT,
            eChimps.CHIMP_TYPE_TREBUCHET,
            eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BATTERING_RAM,
            eChimps.CHIMP_TYPE_SIEGE_TOWER,
            eChimps.CHIMP_TYPE_PORTABLE_SHIELD,
            eChimps.CHIMP_TYPE_BALLISTA,
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
        };

        /// <summary>
        /// All military units (Recruitable + SiegeEquipment).
        /// </summary>
        internal static readonly eChimps[] Military = Recruitable
            .Concat(SiegeEquipment)
            .ToArray();

        /// <summary>
        /// Civilian worker units (spawned from buildings, not recruited).
        /// </summary>
        internal static readonly eChimps[] Workers =
        {
            eChimps.CHIMP_TYPE_PEASANT,
            eChimps.CHIMP_TYPE_WOODCUTTER,
            eChimps.CHIMP_TYPE_FLETCHER,
            eChimps.CHIMP_TYPE_HUNTER,
            eChimps.CHIMP_TYPE_QUARRY_MASON,
            eChimps.CHIMP_TYPE_QUARRY_GRUNT,
            eChimps.CHIMP_TYPE_QUARRY_OX,
            eChimps.CHIMP_TYPE_PITCHMAN,
            eChimps.CHIMP_TYPE_FARMER_WHEAT,
            eChimps.CHIMP_TYPE_FARMER_HOPS,
            eChimps.CHIMP_TYPE_FARMER_APPLE,
            eChimps.CHIMP_TYPE_FARMER_CATTLE,
            eChimps.CHIMP_TYPE_MILLER,
            eChimps.CHIMP_TYPE_BAKER,
            eChimps.CHIMP_TYPE_BREWER,
            eChimps.CHIMP_TYPE_POLETURNER,
            eChimps.CHIMP_TYPE_BLACKSMITH,
            eChimps.CHIMP_TYPE_ARMOURER,
            eChimps.CHIMP_TYPE_TANNER,
            eChimps.CHIMP_TYPE_MINER1,
            eChimps.CHIMP_TYPE_MINER2,
        };

        /// <summary>
        /// Wildlife and domestic animals.
        /// </summary>
        internal static readonly eChimps[] Animals =
        {
            eChimps.CHIMP_TYPE_DEER,
            eChimps.CHIMP_TYPE_LION,
            eChimps.CHIMP_TYPE_RABBIT,
            eChimps.CHIMP_TYPE_CAMEL,
            eChimps.CHIMP_TYPE_CROW,
            eChimps.CHIMP_TYPE_SEAGULL,
            eChimps.CHIMP_TYPE_COW,
            eChimps.CHIMP_TYPE_DOG,
            eChimps.CHIMP_TYPE_CHICKEN,
            eChimps.CHIMP_TYPE_WAR_DOG,
            eChimps.CHIMP_TYPE_GOAT,
            eChimps.CHIMP_TYPE_HYENA,
            eChimps.CHIMP_TYPE_CROCODILE,
        };

        /// <summary>
        /// Special units (Lord, Lady, entertainers, traders, etc).
        /// </summary>
        internal static readonly eChimps[] Special =
        {
            eChimps.CHIMP_TYPE_LORD,
            eChimps.CHIMP_TYPE_LADY,
            eChimps.CHIMP_TYPE_PRIEST,
            eChimps.CHIMP_TYPE_HEALER,
            eChimps.CHIMP_TYPE_DRUNKARD,
            eChimps.CHIMP_TYPE_INNKEEPER,
            eChimps.CHIMP_TYPE_TRADER,
            eChimps.CHIMP_TYPE_TRADER_HORSE,
            eChimps.CHIMP_TYPE_FIREMAN,
            eChimps.CHIMP_TYPE_JESTER,
            eChimps.CHIMP_TYPE_JUGGLER,
            eChimps.CHIMP_TYPE_FIREEATER,
        };
    }
}