// Config/DamageMatrix/BuildingFireDamage/BuildingFireDamageMatrixLoader.cs
// AI DEV: Loads the building fire damage CSV and applies all values via Plugin.BuildingApi.SetBuildingFireDamage.
// API uses short; CSV stores int. Clamps to short range on apply.

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage
{
    internal class BuildingFireDamageMatrixLoader : MatrixLoader<BuildingFireDamageType, eStructs>
    {
        protected override string FilePath => MatrixPaths.BuildingFireDamage;

        protected override BuildingFireDamageType?[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<BuildingFireDamageType>(headers);
        }

        protected override eStructs?[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eStructs>(headers);
        }

        protected override bool ApplyDamageValue(BuildingFireDamageType attackType, eStructs building, int damage)
        {
            try
            {
                // Clamp to short range (API uses short)
                short clamped = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, damage));
                Plugin.BuildingApi.SetBuildingFireDamage(building, clamped);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set building fire damage for {building}: {ex.Message}");
                return false;
            }
        }

        protected override bool ShouldSkipDefender(eStructs building)
        {
            return BuildingMatrixHelper.ShouldSkipBuilding(building);
        }

    }
}
