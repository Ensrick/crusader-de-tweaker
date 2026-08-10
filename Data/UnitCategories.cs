// Data/UnitCategories.cs
//
// PURPOSE: Unit categorization used for filtering/skipping logic across the config systems.
//
// USAGE:
// - IsNonModifiable(): skip UI placeholders / special units during config load + generation
//   (config generators/loaders, UnitMatrixHelper, HealthMultiplierHandler).
// - IsRecruitable(): gate the recruit-only properties (GoldCost, WeaponType/ArmorType,
//   RequiresHorse) to units that can actually be recruited.
// - IsNonMeleeAttacker(): exclude units with no melee attack from the melee damage matrix.
//
// NOT used for damage calculation — damage is configured exclusively via the CSV matrices.
//
using System.Collections.Generic;
using System.Linq;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Categorizes units for filtering and skipping logic. NOT used for damage calculation.
    /// </summary>
    internal static class UnitCategories
    {
        // ========================================
        // NON-MODIFIABLE UNITS
        // ========================================

        /// <summary>
        /// Units that cannot or should not be modified (UI placeholders, special/effect units).
        /// HashSet for O(1) lookup.
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
        /// Array view of the non-modifiable set, for the config systems that take an array of
        /// entities to skip (EntityProcessor / ConfigGenerator).
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
        /// Units that don't attack in melee, excluded from the melee damage matrix. The Arab
        /// Ballista has no melee attack animation and should not appear as a melee attacker.
        /// </summary>
        private static readonly HashSet<eChimps> _nonMeleeAttackers = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
        };

        /// <summary>
        /// Check if a unit cannot attack in melee (excluded from melee damage output).
        /// </summary>
        internal static bool IsNonMeleeAttacker(eChimps unit)
        {
            return _nonMeleeAttackers.Contains(unit);
        }

        // Domain note (previously HasSpecialRangedFloor): the Mantlet / Portable Shield uses a
        // ranged-damage floor of 10 rather than the usual rounding to 50. Reinstate a lookup here
        // if a generator ever needs to special-case it again.

        // ========================================
        // RECRUITMENT
        // ========================================

        /// <summary>
        /// All units that can be recruited with a gold cost. HashSet for O(1) lookup.
        ///
        /// NOTE: CHIMP_TYPE_BALLISTA and CHIMP_TYPE_MANGONEL are intentionally NOT here.
        /// GetUnitGoldCost returns 0 for both — their placement cost is stored outside the unit
        /// gold-cost table. They still appear in the Units TOML with Health only (HealthProperty
        /// applies to every non-NonModable unit).
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
        /// Check if a unit is recruitable (O(1) lookup).
        /// </summary>
        internal static bool IsRecruitable(eChimps unit)
        {
            return _recruitableSet.Contains(unit);
        }
    }
}
