// Config/DamageMatrix/UnitFireDamage/UnitFireDamageMatrixGenerator.cs
// AI DEV: Generates the unit fire damage CSV file (1 column "Fire", rows = unit types).
// Rows = modifiable eChimps units, column header = "Fire".

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.UnitFireDamage
{
    internal class UnitFireDamageMatrixGenerator : MatrixGenerator<UnitFireDamageType, eChimps>
    {
        protected override string FilePath => MatrixPaths.UnitFireDamage;

        protected override UnitFireDamageType[] GetAttackers()
        {
            return new[] { UnitFireDamageType.Fire };
        }

        protected override eChimps[] GetDefenders()
        {
            return UnitMatrixHelper.GetModifiableUnits();
        }

        protected override int GetDamageValue(UnitFireDamageType attackType, eChimps unit)
        {
            try
            {
                return Plugin.UnitApi.GetFireDamage(unit);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get fire damage for {unit}: {ex.Message}");
                return -1;
            }
        }

        protected override string GetAttackerHeader(UnitFireDamageType attackType)
        {
            return "Fire";
        }
    }
}
