// Config/DamageMatrix/Ranged/RangedDamageMatrixLoader.cs
using System;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;
using UnityEngine;

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
        /// Apply a ranged damage value to the game (from CSV override).
        /// Applies the BepInEx ranged damage multiplier if configured.
        /// </summary>
        protected override bool ApplyDamageValue(ProjectileType projectile, eChimps defender, int damage)
        {
            // Note: Skip checks are now done in base class Load() method before calling this
            // This method assumes valid, modifiable entities
            
            // Apply BepInEx ranged damage multiplier
            var unitMultipliers = BepInExConfigManager.UnitMultipliers;
            if (unitMultipliers?.RangedDamageTakenMultiplier != null && 
                !Mathf.Approximately(unitMultipliers.RangedDamageTakenMultiplier.Value, 1.0f))
            {
                damage = (int)(damage * unitMultipliers.RangedDamageTakenMultiplier.Value);
            }

            if (!ProjectileApiHelper.SetRangedDamage(projectile, defender, damage))
            {
                Plugin.Logger.LogWarning($"Failed to set {projectile} damage for {defender}");
                return false;
            }


            return true;
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