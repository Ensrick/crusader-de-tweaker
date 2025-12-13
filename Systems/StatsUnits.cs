using System;
using System.Linq;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    internal static class StatsUnits
    {
        // MUST BE DEFINED FIRST - Used by OtherUnits initialization
        internal static readonly eChimps[] NonModableUnits =
        {
            eChimps.CHIMP_NUM_TYPES,
            eChimps.CHIMP_TYPE_NULL,
            eChimps.CHIMP_TYPE_MOTHER,
            eChimps.CHIMP_TYPE_CHILD,
            eChimps.CHIMP_TYPE_BURNING_MAN,
            eChimps.CHIMP_TYPE_BURNING_ANIMAL_BIG,
            eChimps.CHIMP_TYPE_BURNING_ANIMAL_SMALL,
            eChimps.CHIMP_TYPE_BURNING_MAN,
            eChimps.CHIMP_TYPE_ARCHER_debug,
            // add units that probably can't/shouldn't be modded here
        };

        private static readonly eChimps[] SwordUnits =
        {
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_WOODCUTTER,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_LORD,
            // add blade-wielding units here
        };

        private static readonly eChimps[] BluntUnits =
        {
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
            eChimps.CHIMP_TYPE_MONK,
            // add blunt-wielding units here
        };

        private static readonly eChimps[] PolearmUnits =
        {
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            // add polearm-wielding units here
        };

        private static readonly eChimps[] CavalryUnits =
        {
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            // add cavalry units here
        };

        // Any Melee attacker not covered by the above is here.
        // CRITICAL: NonModableUnits must be defined BEFORE this line
        private static readonly eChimps[] OtherUnits = Enum
            .GetValues(typeof(eChimps))
            .Cast<eChimps>()
            .Except(SwordUnits)
            .Except(BluntUnits)
            .Except(PolearmUnits)
            .Except(CavalryUnits)
            .Except(NonModableUnits)
            .ToArray();

        /// <summary>
        /// Applies a damage multiplier to all sword-wielding units attacking the specified defender.
        /// </summary>
        internal static void ApplySwordDamageMultipliers(eChimps defender, float mult)
        {
            ApplyDamageMultipliers(SwordUnits, defender, mult);
        }

        /// <summary>
        /// Applies a damage multiplier to all blunt-weapon units attacking the specified defender.
        /// </summary>
        internal static void ApplyBluntDamageMultipliers(eChimps defender, float mult)
        {
            ApplyDamageMultipliers(BluntUnits, defender, mult);
        }

        /// <summary>
        /// Applies a damage multiplier to all polearm units attacking the specified defender.
        /// </summary>
        internal static void ApplyPolearmDamageMultipliers(eChimps defender, float mult)
        {
            ApplyDamageMultipliers(PolearmUnits, defender, mult);
        }

        /// <summary>
        /// Applies a damage multiplier to all cavalry units attacking the specified defender.
        /// </summary>
        internal static void ApplyCavalryDamageMultipliers(eChimps defender, float mult)
        {
            ApplyDamageMultipliers(CavalryUnits, defender, mult);
        }

        /// <summary>
        /// Applies a damage multiplier to all other melee units attacking the specified defender.
        /// </summary>
        internal static void ApplyOtherDamageMultipliers(eChimps defender, float mult)
        {
            ApplyDamageMultipliers(OtherUnits, defender, mult);
        }

        /// <summary>
        /// Common implementation for applying damage multipliers to a group of attackers.
        /// </summary>
        private static void ApplyDamageMultipliers(eChimps[] attackers, eChimps defender, float multiplier)
        {
            foreach (var attacker in attackers)
            {
                int baseDamage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * multiplier), 1);
                Plugin.UnitApi.SetMeleeDamageFromTo(attacker, defender, finalDamage);
            }
        }
    }
}