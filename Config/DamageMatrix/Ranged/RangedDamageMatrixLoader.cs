// Config/DamageMatrix/Ranged/RangedDamageMatrixLoader.cs
using System;
using System.Linq;
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
        /// Apply a ranged damage value to the game.
        /// Applies the BepInEx ranged damage multiplier if configured.
        /// </summary>
        protected override bool ApplyDamageValue(ProjectileType projectile, eChimps defender, int damage)
        {
            // Skip non-modifiable units
            if (ShouldSkipDefender(defender))
                return false;

            // Apply BepInEx ranged damage multiplier
#pragma warning disable CS0618 // Type or member is obsolete
            if (ConfigManagerBepinex.UnitRangedDamageTakenMultiplier != null && 
                !Mathf.Approximately(ConfigManagerBepinex.UnitRangedDamageTakenMultiplier.Value, 1.0f))
            {
                damage = (int)(damage * ConfigManagerBepinex.UnitRangedDamageTakenMultiplier.Value);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            try
            {
                switch (projectile)
                {
                    case ProjectileType.Arrow:
                        Plugin.UnitApi.SetRangedArrowDamageTo(defender, damage);
                        return true;

                    case ProjectileType.Bolt:
                        Plugin.UnitApi.SetRangedBoltDamageTo(defender, damage);
                        return true;

                    case ProjectileType.Slinger:
                        Plugin.UnitApi.SetRangedSlingerDamageTo(defender, damage);
                        return true;

                    case ProjectileType.Javelin:
                        Plugin.UnitApi.SetRangedJavelinDamageTo(defender, damage);
                        return true;

                    default:
                        Plugin.Logger.LogWarning($"Unknown projectile type: {projectile}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set {projectile} damage for {defender}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a defender should be skipped.
        /// </summary>
        protected override bool ShouldSkipDefender(eChimps defender)
        {
            return UnitCategories.NonModable.Contains(defender);
        }

        /// <summary>
        /// Get the current ranged damage value from the game API.
        /// Used to determine if CSV value differs from current game state.
        /// </summary>
        protected override bool TryGetCurrentValue(ProjectileType projectile, eChimps defender, out int currentValue)
        {
            try
            {
                switch (projectile)
                {
                    case ProjectileType.Arrow:
                        currentValue = Plugin.UnitApi.GetRangedArrowDamageTo(defender);
                        return true;

                    case ProjectileType.Bolt:
                        currentValue = Plugin.UnitApi.GetRangedBoltDamageTo(defender);
                        return true;

                    case ProjectileType.Slinger:
                        currentValue = Plugin.UnitApi.GetRangedSlingerDamageTo(defender);
                        return true;

                    case ProjectileType.Javelin:
                        currentValue = Plugin.UnitApi.GetRangedJavelinDamageTo(defender);
                        return true;

                    default:
                        currentValue = 0;
                        return false;
                }
            }
            catch
            {
                currentValue = 0;
                return false;
            }
        }
    }
}