// Config/DamageMatrix/BedouinHeal/BedouinHealMatrixGenerator.cs
// AI DEV: Generates the Bedouin heal CSV file (1 column "BedouinHeal", rows = unit types).
// Rows = modifiable eChimps units, column header = "BedouinHeal".

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BedouinHeal
{
    internal class BedouinHealMatrixGenerator : MatrixGenerator<BedouinHealType, eChimps>
    {
        protected override string FilePath => MatrixPaths.BedouinHeal;

        protected override BedouinHealType[] GetAttackers()
        {
            return new[] { BedouinHealType.BedouinHeal };
        }

        protected override eChimps[] GetDefenders()
        {
            return UnitMatrixHelper.GetModifiableUnits();
        }

        protected override int GetDamageValue(BedouinHealType attackType, eChimps unit)
        {
            try
            {
                return Plugin.UnitApi.GetBedouinHeal(unit);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get Bedouin heal for {unit}: {ex.Message}");
                return -1;
            }
        }

        protected override string GetAttackerHeader(BedouinHealType attackType)
        {
            return "BedouinHeal";
        }
    }
}
