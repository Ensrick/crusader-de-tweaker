using System;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Config.DamageMatrix.Ballista
{
    /// <summary>
    /// Generates the Ballista damage matrix CSV file.
    /// Matrix format: Rows = BallistaDamageTarget, Column = BallistaAttacker
    /// </summary>
    internal class BallistaDamageMatrixGenerator : MatrixGenerator<BallistaAttacker, BallistaDamageTarget>
    {
        protected override string FilePath => MatrixPaths.BallistaDamage;

        protected override BallistaAttacker[] GetAttackers()
        {
            return new[] { BallistaAttacker.Ballista };
        }

        protected override BallistaDamageTarget[] GetDefenders()
        {
            return (BallistaDamageTarget[])Enum.GetValues(typeof(BallistaDamageTarget));
        }

        protected override int GetDamageValue(BallistaAttacker attackType, BallistaDamageTarget defender)
        {
            try
            {
                var dict = BallistaDamageHelper.GetMappings();
                var prop = dict[defender];
                return prop != null ? prop.GetValue() : -1;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get Ballista damage for {defender}: {ex.Message}");
                return -1;
            }
        }

        protected override string GetAttackerHeader(BallistaAttacker attackType)
        {
            return "Ballista";
        }
    }
}
