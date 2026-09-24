// Config/DamageMatrix/UnitFireDamage/UnitFireDamageMatrixLoader.cs
// AI DEV: Loads the unit fire damage CSV and applies all values via Plugin.UnitApi.SetFireDamage.

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.UnitFireDamage
{
    internal class UnitFireDamageMatrixLoader : MatrixLoader<UnitFireDamageType, eChimps>
    {
        protected override string FilePath => MatrixPaths.UnitFireDamage;

        protected override UnitFireDamageType?[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<UnitFireDamageType>(headers);
        }

        protected override eChimps?[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        protected override bool ApplyDamageValue(UnitFireDamageType attackType, eChimps unit, int damage)
        {
            try
            {
                Plugin.UnitApi.SetFireDamage(unit, damage);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set fire damage for {unit}: {ex.Message}");
                return false;
            }
        }

        protected override string BaselineKey(UnitFireDamageType attackType, eChimps unit) => MatrixBaselineKeys.UnitFire(unit);

        protected override Action CaptureRestore(UnitFireDamageType attackType, eChimps unit)
        {
            int original = Plugin.UnitApi.GetFireDamage(unit);
            return () => Plugin.UnitApi.SetFireDamage(unit, original);
        }

        protected override bool ShouldSkipDefender(eChimps unit)
        {
            return UnitMatrixHelper.ShouldSkipUnit(unit);
        }

    }
}
