// Config/DamageMatrix/BuildingFireDamage/BuildingFireDamageMatrixGenerator.cs
// AI DEV: Generates the building fire damage CSV file (1 column "Fire", rows = building types).
// Rows = modifiable eStructs buildings, column header = "Fire".
// GetBuildingFireDamage returns short; stored as int in the matrix.

using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage
{
    internal class BuildingFireDamageMatrixGenerator : MatrixGenerator<BuildingFireDamageType, eStructs>
    {
        protected override string FilePath => MatrixPaths.BuildingFireDamage;

        protected override BuildingFireDamageType[] GetAttackers()
        {
            return new[] { BuildingFireDamageType.Fire };
        }

        protected override eStructs[] GetDefenders()
        {
            return BuildingMatrixHelper.GetModifiableBuildings();
        }

        protected override int GetDamageValue(BuildingFireDamageType attackType, eStructs building)
        {
            try
            {
                return Plugin.BuildingApi.GetBuildingFireDamage(building);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get building fire damage for {building}: {ex.Message}");
                return -1;
            }
        }

        protected override string GetAttackerHeader(BuildingFireDamageType attackType)
        {
            return "Fire";
        }
    }
}
