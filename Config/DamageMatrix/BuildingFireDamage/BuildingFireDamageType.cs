// Config/DamageMatrix/BuildingFireDamage/BuildingFireDamageType.cs
// AI DEV: Single-value enum used as the "attacker" axis in the building fire damage CSV.
// This allows using the standard MatrixGenerator/Loader framework for a 1D per-building table.

namespace CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage
{
    internal enum BuildingFireDamageType
    {
        Fire
    }
}
