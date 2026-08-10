// Config/DamageMatrix/Core/ProjectileApiHelper.cs
//
// PURPOSE: Consolidates projectile API calls to eliminate switch statement duplication.
//
// USAGE:
// - GetRangedDamage(): Used by RangedDamageMatrixGenerator to read from API
// - SetRangedDamage(): Used by RangedDamageMatrixLoader to apply CSV values to game
//
// IMPORTANT FOR AI AGENTS:
// - This is a helper class to avoid duplicating switch statements across multiple files
// - Maps ProjectileType enum to specific API methods (GetRangedArrowDamageTo, GetRangedBoltDamageTo, etc.)
// - Returns -1 on error (indicates API call failed or projectile type not found)
//
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Helper class to consolidate projectile API calls and eliminate switch statement duplication.
    /// 
    /// Used by:
    /// - RangedDamageMatrixGenerator: Generates CSV files with current API values
    /// - RangedDamageMatrixLoader: Applies CSV values to game via API
    /// 
    /// Maps ProjectileType enum values to specific SHCDE-SE API methods.
    /// </summary>
    internal static class ProjectileApiHelper
    {
        /// <summary>
        /// Get ranged damage for a projectile type against a defender.
        /// Returns -1 if the damage cannot be determined.
        /// </summary>
        public static int GetRangedDamage(ProjectileType projectile, eChimps defender)
        {
            try
            {
                switch (projectile)
                {
                    case ProjectileType.Arrow:
                        return Plugin.UnitApi.GetRangedArrowDamageTo(defender);

                    case ProjectileType.Bolt:
                        return Plugin.UnitApi.GetRangedBoltDamageTo(defender);

                    case ProjectileType.Slinger:
                        return Plugin.UnitApi.GetRangedSlingerDamageTo(defender);

                    case ProjectileType.Javelin:
                        return Plugin.UnitApi.GetRangedJavelinDamageTo(defender);

                    default:
                        return -1;
                }
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Set ranged damage for a projectile type against a defender.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public static bool SetRangedDamage(ProjectileType projectile, eChimps defender, int damage)
        {
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
            catch
            {
                return false;
            }
        }
    }
}

