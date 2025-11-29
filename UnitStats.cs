using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SHCDESE.Interop;

namespace CrusaderDETweaker
{
    internal static class UnitStats
    {
        static readonly eChimps[] SwordUnits =
        {
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_WOODCUTTER,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_LORD,
            // add blade-weilding units here
        };
        static readonly eChimps[] BluntUnits =
        {
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
            eChimps.CHIMP_TYPE_MONK,
            // add blunt-weilding units here
        };
        static readonly eChimps[] PolearmUnits =
        {
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            // add polearm-weilding units here
        };
        static readonly eChimps[] CavalryUnits =
        {
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            // add cavalry units here
        };
        // Any Melee attacker not covered by the above is here.
        static readonly eChimps[] OtherUnits = Enum
            .GetValues(typeof(eChimps))
            .Cast<eChimps>()
            .Except(SwordUnits)
            .Except(BluntUnits)
            .Except(PolearmUnits)
            .Except(CavalryUnits)
            .ToArray();
        internal static void ApplySwordDamageMultipliers(eChimps defender, float mult)
        {
            foreach (var attacker in SwordUnits)
            {
                int baseDamage = Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * mult), 1);

                Plugin.UnitApi.SetMeeleDamageFromTo(attacker, defender, finalDamage);
            }
        }
        internal static void ApplyBluntDamageMultipliers(eChimps defender, float mult)
        {
            foreach (var attacker in BluntUnits)
            {
                int baseDamage = Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * mult), 1);

                Plugin.UnitApi.SetMeeleDamageFromTo(attacker, defender, finalDamage);
            }
        }
        internal static void ApplyPolearmDamageMultipliers(eChimps defender, float mult)
        {
            foreach (var attacker in PolearmUnits)
            {
                int baseDamage = Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * mult), 1);

                Plugin.UnitApi.SetMeeleDamageFromTo(attacker, defender, finalDamage);
            }
        }
        internal static void ApplyCavalryDamageMultipliers(eChimps defender, float mult)
        {
            foreach (var attacker in CavalryUnits)
            {
                int baseDamage = Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * mult), 1);

                Plugin.UnitApi.SetMeeleDamageFromTo(attacker, defender, finalDamage);
            }
        }
        internal static void ApplyOtherDamageMultipliers(eChimps defender, float mult)
        {
            Plugin.Logger.LogInfo($"=== OTHER units attacking {defender}, count: {OtherUnits.Length} ===");
            foreach (var attacker in OtherUnits)
            {
                int baseDamage = Plugin.UnitApi.GetMeeleDamageFromTo(attacker, defender);
                int finalDamage = Math.Max((int)Math.Round(baseDamage * mult), 1);
                Plugin.Logger.LogInfo($"  {attacker}: {baseDamage} * {mult} = {finalDamage}");
                Plugin.UnitApi.SetMeeleDamageFromTo(attacker, defender, finalDamage);
            }
        }
    }
}
