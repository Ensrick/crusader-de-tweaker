// Config/DamageMatrix/Core/CsvMatrixFallback.cs
//
// PURPOSE: Fallback mechanism for getting original game defaults when API capture fails.
//
// USAGE:
// - GetMeleeDamage(): Fallback if MeleeDefaultsCapture didn't capture a value
// - GetRangedDamage(): Fallback if RangedDefaultsCapture didn't capture a value
// - GetEunuchAoeDamage(): Fallback if EunuchAoeDefaultsCapture didn't capture a value
//
// IMPORTANT FOR AI AGENTS:
// - This is a FALLBACK only - primary source is API capture (MeleeDefaultsCapture, etc.)
// - Only used if API capture fails or returns -1 for a specific matchup
// - Loads values from CSV files (which contain original game defaults)
// - Caches loaded values to avoid repeated file I/O
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.Ballista;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Provides fallback loading of damage values from CSV files when original defaults weren't captured from API.
    /// 
    /// This is a safety mechanism: if the API capture fails for any reason, we can still
    /// get original defaults by reading them from the CSV files (which were generated from
    /// original game values).
    /// 
    /// Used by CsvMatrixReader when API capture returns -1 (not found).
    /// </summary>
    internal static class CsvMatrixFallback
    {
        private static Dictionary<(eChimps attacker, eChimps defender), int> _meleeCache;
        private static Dictionary<(ProjectileType projectile, eChimps defender), int> _rangedCache;
        private static Dictionary<eChimps, int> _eunuchAoeCache;
        private static Dictionary<BallistaDamageTarget, int> _ballistaCache;

        /// <summary>
        /// Get melee damage from CSV file (fallback). Returns -1 if not found.
        /// </summary>
        public static int GetMeleeDamage(eChimps attacker, eChimps defender)
        {
            if (_meleeCache == null)
                _meleeCache = LoadMeleeMatrix();

            return _meleeCache.TryGetValue((attacker, defender), out int damage) ? damage : -1;
        }

        /// <summary>
        /// Get ranged damage from CSV file (fallback). Returns -1 if not found.
        /// </summary>
        public static int GetRangedDamage(ProjectileType projectile, eChimps defender)
        {
            if (_rangedCache == null)
                _rangedCache = LoadRangedMatrix();

            return _rangedCache.TryGetValue((projectile, defender), out int damage) ? damage : -1;
        }

        /// <summary>
        /// Get Eunuch AOE damage from CSV file (fallback). Returns -1 if not found.
        /// </summary>
        public static int GetEunuchAoeDamage(eChimps defender)
        {
            if (_eunuchAoeCache == null)
                _eunuchAoeCache = LoadEunuchAoeMatrix();

            return _eunuchAoeCache.TryGetValue(defender, out int damage) ? damage : -1;
        }

        /// <summary>
        /// Get Ballista damage from CSV file (fallback). Returns -1 if not found.
        /// </summary>
        public static int GetBallistaDamage(BallistaDamageTarget target)
        {
            if (_ballistaCache == null)
                _ballistaCache = LoadBallistaMatrix();

            return _ballistaCache.TryGetValue(target, out int damage) ? damage : -1;
        }

        private static Dictionary<(eChimps attacker, eChimps defender), int> LoadMeleeMatrix()
        {
            return LoadMatrix<eChimps, eChimps, (eChimps, eChimps)>(
                MatrixPaths.MeleeDamage,
                "melee",
                (rowHeaders, colHeaders) => (
                    ParseHeaders<eChimps>(rowHeaders),
                    ParseHeaders<eChimps>(colHeaders)
                ),
                (defender, attacker, matrix, row, col) => ((attacker, defender), matrix[row, col])
            );
        }

        private static Dictionary<(ProjectileType projectile, eChimps defender), int> LoadRangedMatrix()
        {
            return LoadMatrix<ProjectileType, eChimps, (ProjectileType, eChimps)>(
                MatrixPaths.RangedDamage,
                "ranged",
                (rowHeaders, colHeaders) => (
                    ParseHeaders<eChimps>(rowHeaders),
                    ParseHeaders<ProjectileType>(colHeaders)
                ),
                (defender, projectile, matrix, row, col) => ((projectile, defender), matrix[row, col])
            );
        }

        private static Dictionary<eChimps, int> LoadEunuchAoeMatrix()
        {
            var cache = new Dictionary<eChimps, int>();
            string filePath = MatrixPaths.EunuchAoeDamage;

            if (!File.Exists(filePath))
            {
                Plugin.Logger.LogWarning($"Eunuch AOE damage CSV file not found: {filePath}");
                return cache;
            }

            try
            {
                var (rowHeaders, _, matrix) = CsvHelper.ReadMatrix(filePath);
                var defenders = ParseHeaders<eChimps>(rowHeaders);

                for (int row = 0; row < defenders.Length && row < matrix.GetLength(0); row++)
                {
                    if (!defenders[row].HasValue)
                        continue;

                    int damage = matrix[row, 0];
                    if (damage >= 0)
                        cache[defenders[row].Value] = damage;
                }

                Plugin.Logger.LogInfo($"Loaded {cache.Count} original Eunuch AOE damage values from CSV");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load Eunuch AOE damage CSV: {ex.Message}");
            }

            return cache;
        }

        private static Dictionary<BallistaDamageTarget, int> LoadBallistaMatrix()
        {
            var cache = new Dictionary<BallistaDamageTarget, int>();
            string filePath = MatrixPaths.BallistaDamage;

            if (!File.Exists(filePath))
            {
                Plugin.Logger.LogWarning($"Ballista damage CSV file not found: {filePath}");
                return cache;
            }

            try
            {
                var (rowHeaders, _, matrix) = CsvHelper.ReadMatrix(filePath);
                var defenders = ParseHeaders<BallistaDamageTarget>(rowHeaders);

                for (int row = 0; row < defenders.Length && row < matrix.GetLength(0); row++)
                {
                    if (!defenders[row].HasValue)
                        continue;

                    int damage = matrix[row, 0];
                    if (damage >= 0)
                        cache[defenders[row].Value] = damage;
                }

                Plugin.Logger.LogInfo($"Loaded {cache.Count} original Ballista damage values from CSV");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load Ballista damage CSV: {ex.Message}");
            }

            return cache;
        }

        private static Dictionary<TKey, int> LoadMatrix<TAttacker, TDefender, TKey>(
            string filePath,
            string typeName,
            Func<string[], string[], (TDefender?[] defenders, TAttacker?[] attackers)> parseHeaders,
            Func<TDefender, TAttacker, int[,], int, int, (TKey key, int damage)> buildEntry)
            where TAttacker : struct
            where TDefender : struct
        {
            var cache = new Dictionary<TKey, int>();

            if (!File.Exists(filePath))
            {
                Plugin.Logger.LogWarning($"{typeName} damage CSV file not found: {filePath}");
                return cache;
            }

            try
            {
                var (rowHeaders, columnHeaders, matrix) = CsvHelper.ReadMatrix(filePath);
                var (defenders, attackers) = parseHeaders(rowHeaders, columnHeaders);

                for (int row = 0; row < defenders.Length && row < matrix.GetLength(0); row++)
                {
                    if (!defenders[row].HasValue)
                        continue;

                    for (int col = 0; col < attackers.Length && col < matrix.GetLength(1); col++)
                    {
                        if (!attackers[col].HasValue)
                            continue;

                        var (key, damage) = buildEntry(defenders[row].Value, attackers[col].Value, matrix, row, col);
                        if (damage >= 0)
                            cache[key] = damage;
                    }
                }

                Plugin.Logger.LogInfo($"Loaded {cache.Count} original {typeName} damage values from CSV");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load {typeName} damage CSV: {ex.Message}");
            }

            return cache;
        }

        private static TEnum?[] ParseHeaders<TEnum>(string[] headers) where TEnum : struct
        {
            return headers.Select(h =>
            {
                if (Enum.TryParse<TEnum>(h, out var parsed))
                    return (TEnum?)parsed;
                return null;
            }).ToArray();
        }
    }
}

