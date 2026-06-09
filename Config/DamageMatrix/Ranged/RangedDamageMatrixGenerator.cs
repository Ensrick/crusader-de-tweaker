// Config/DamageMatrix/Ranged/RangedDamageMatrixGenerator.cs
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Ranged
{
    /// <summary>
    /// Generates the ranged damage matrix CSV file.
    /// Matrix format: Rows = Defenders, Columns = Projectile types (Arrow, Bolt, Slinger, Javelin)
    /// </summary>
    internal class RangedDamageMatrixGenerator : MatrixGenerator<ProjectileType, eChimps>
    {
        protected override string FilePath => MatrixPaths.RangedDamage;

        /// <summary>
        /// Get all projectile types as "attackers" (columns).
        /// </summary>
        protected override ProjectileType[] GetAttackers()
        {
            return (ProjectileType[])Enum.GetValues(typeof(ProjectileType));
        }

        /// <summary>
        /// Get all units that can take ranged damage (defenders = rows).
        /// Excludes non-modifiable units.
        /// </summary>
        protected override eChimps[] GetDefenders()
        {
            return UnitMatrixHelper.GetModifiableUnits();
        }

        /// <summary>
        /// Get the ranged damage value for a projectile type against a defender.
        /// </summary>
        protected override int GetDamageValue(ProjectileType projectile, eChimps defender)
        {
            int damage = ProjectileApiHelper.GetRangedDamage(projectile, defender);
            if (damage < 0)
            {
                Plugin.Logger.LogDebug($"Could not get {projectile} damage for {defender}");
            }
            return damage;
        }

        /// <summary>
        /// Customize projectile header names if needed.
        /// </summary>
        protected override string GetAttackerHeader(ProjectileType projectile)
        {
            return projectile.ToString();
        }

    }
}