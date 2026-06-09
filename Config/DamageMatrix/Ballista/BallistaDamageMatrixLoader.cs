using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Config.DamageMatrix.Ballista
{
    internal class BallistaDamageMatrixLoader : MatrixLoader<BallistaAttacker, BallistaDamageTarget>
    {
        protected override string FilePath => MatrixPaths.BallistaDamage;

        protected override Nullable<BallistaAttacker>[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<BallistaAttacker>(headers);
        }

        protected override Nullable<BallistaDamageTarget>[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<BallistaDamageTarget>(headers);
        }

        protected override bool ApplyDamageValue(BallistaAttacker attackType, BallistaDamageTarget defender, int damage)
        {
            try
            {
                var dict = BallistaDamageHelper.GetMappings();
                var prop = dict[defender];
                if (prop != null && damage >= 0)
                {
                    prop.SetValue((ushort)damage);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set Ballista damage for {defender}: {ex.Message}");
                return false;
            }
        }

        protected override bool ShouldSkipDefender(BallistaDamageTarget defender)
        {
            return false;
        }

    }
}
