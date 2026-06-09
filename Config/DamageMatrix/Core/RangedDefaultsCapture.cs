// Config/DamageMatrix/Core/RangedDefaultsCapture.cs
//
// PURPOSE: Captures and caches original ranged damage defaults from game API.
//
// USAGE:
// - Capture(): Called by OriginalDefaultsCapture to capture all ranged damage values
// - Get(): Called by CsvMatrixReader to retrieve original default for comparison
//
// IMPORTANT FOR AI AGENTS:
// - Captures 4 projectile types: Arrow, Bolt, Slinger, Javelin
// - Uses ProjectileApiHelper.GetRangedDamage() to map projectile types to API calls
// - Only captures modifiable units (non-modifiable units are skipped)
// - Caches values in dictionary for O(1) lookup performance
// - Returns -1 if value not found (indicates API call failed or unit not modifiable)
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Captures and provides access to original ranged damage defaults.
    /// 
    /// Reads damage values directly from game API before any mods are applied.
    /// Captures all 4 projectile types (Arrow, Bolt, Slinger, Javelin) against all modifiable units.
    /// Caches values for fast lookup during CSV matrix loading.
    /// </summary>
    internal static class RangedDefaultsCapture
    {
        private static Dictionary<(ProjectileType projectile, eChimps defender), int> _cache;
        private static readonly ProjectileType[] ProjectileTypes = 
        {
            ProjectileType.Arrow,
            ProjectileType.Bolt,
            ProjectileType.Slinger,
            ProjectileType.Javelin
        };

        /// <summary>
        /// Capture original ranged damage defaults from game API.
        /// </summary>
        public static void Capture(eChimps[] units)
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<(ProjectileType projectile, eChimps defender), int>();
            
            Plugin.Logger.LogInfo("Capturing original ranged damage defaults from game API...");
            int captured = 0;
            int errors = 0;

            foreach (var projectile in ProjectileTypes)
            {
                foreach (var defender in units)
                {
                    try
                    {
                        int damage = GetRangedDamage(projectile, defender);
                        if (damage >= 0)
                        {
                            _cache[(projectile, defender)] = damage;
                            captured++;
                        }
                    }
                    catch (Exception)
                    {
                        errors++;
                    }
                }
            }

            Plugin.Logger.LogInfo($"Captured {captured} original ranged damage defaults ({(errors > 0 ? $"{errors} errors" : "no errors")})");
        }

        /// <summary>
        /// Get original ranged damage default. Returns -1 if not found.
        /// </summary>
        public static int Get(ProjectileType projectile, eChimps defender)
        {
            return _cache != null && _cache.TryGetValue((projectile, defender), out int damage) 
                ? damage 
                : -1;
        }

        private static int GetRangedDamage(ProjectileType projectile, eChimps defender)
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
    }
}

