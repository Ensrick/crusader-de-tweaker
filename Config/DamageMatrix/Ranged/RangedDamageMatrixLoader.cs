// Config/DamageMatrix/Ranged/RangedDamageMatrixLoader.cs
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Ranged
{
    /// <summary>
    /// Loads and applies the ranged damage matrix from CSV file.
    /// Matrix format: Rows = Defenders, Columns = Projectile types (Arrow, Bolt, Slinger, Javelin)
    /// </summary>
    internal class RangedDamageMatrixLoader : MatrixLoader<ProjectileType, eChimps>
    {
        protected override string FilePath => MatrixPaths.RangedDamage;

        /// <summary>
        /// Parse projectile type headers from CSV columns.
        /// </summary>
        protected override Nullable<ProjectileType>[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<ProjectileType>(headers);
        }

        /// <summary>
        /// Parse defender unit types from CSV rows.
        /// </summary>
        protected override Nullable<eChimps>[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        /// <summary>
        /// Apply a ranged damage value to the game (from CSV override), unscaled.
        /// </summary>
        protected override bool ApplyDamageValue(ProjectileType projectile, eChimps defender, int damage)
        {
            // Note: Skip checks are now done in base class Load() method before calling this
            // This method assumes valid, modifiable entities

            // RangedDamageTakenMultiplier is NOT applied here: RangedDamageMultiplierHandler applies it
            // at hit time. (Before 2.7.0 this scaled the CSV value too; at launch that never ran because
            // the multipliers are bound after the matrices load, but any re-apply, e.g. the multiplayer
            // host config sync, would have scaled ranged damage twice.)
            if (!ProjectileApiHelper.SetRangedDamage(projectile, defender, damage))
            {
                Plugin.Logger.LogWarning($"Failed to set {projectile} damage for {defender}");
                return false;
            }


            return true;
        }

        protected override string BaselineKey(ProjectileType projectile, eChimps defender) => $"ranged|{projectile}|{defender}";

        protected override Action CaptureRestore(ProjectileType projectile, eChimps defender)
        {
            int original = ProjectileApiHelper.GetRangedDamage(projectile, defender);
            if (original < 0) return null;
            return () => ProjectileApiHelper.SetRangedDamage(projectile, defender, original);
        }


        /// <summary>
        /// Check if a defender should be skipped.
        /// </summary>
        protected override bool ShouldSkipDefender(eChimps defender)
        {
            return UnitMatrixHelper.ShouldSkipUnit(defender);
        }

    }
}