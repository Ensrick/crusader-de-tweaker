// Config/DamageMatrix/BedouinHeal/BedouinHealMatrixLoader.cs
// AI DEV: Loads the Bedouin heal CSV and applies all values via Plugin.UnitApi.SetBedouinHeal.

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BedouinHeal
{
    internal class BedouinHealMatrixLoader : MatrixLoader<BedouinHealType, eChimps>
    {
        protected override string FilePath => MatrixPaths.BedouinHeal;

        protected override BedouinHealType?[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<BedouinHealType>(headers);
        }

        protected override eChimps?[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        protected override bool ApplyDamageValue(BedouinHealType attackType, eChimps unit, int heal)
        {
            try
            {
                Plugin.UnitApi.SetBedouinHeal(unit, heal);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set Bedouin heal for {unit}: {ex.Message}");
                return false;
            }
        }

        protected override bool ShouldSkipDefender(eChimps unit)
        {
            return UnitMatrixHelper.ShouldSkipUnit(unit);
        }

    }
}
