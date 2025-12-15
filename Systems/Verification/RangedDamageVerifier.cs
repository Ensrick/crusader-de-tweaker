// Systems/Verification/RangedDamageVerifier.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.Verification
{
    /// <summary>
    /// Verifies ranged damage values against the game's API.
    /// Ranged damage is per-projectile-type per-defender, not calculated from a formula.
    /// This verifier checks that our stored/configured values match the game defaults.
    /// </summary>
    internal class RangedDamageVerifier : DamageVerifierBase
    {
        public override string VerifierName => "Ranged Damage";

        /// <summary>
        /// Verify ranged damage values against the game API.
        /// Compares current game API values against original defaults (from CSV or captured API values).
        /// </summary>
        public override VerificationResult Verify()
        {
            var result = CreateResult(VerifierName);

            try
            {
                // Get all modifiable units
                var units = Enum.GetValues(typeof(eChimps))
                    .Cast<eChimps>()
                    .Where(unit => !UnitCategories.NonModable.Contains(unit))
                    .OrderBy(unit => unit.ToString())
                    .ToArray();

                var projectileTypes = new[] { ProjectileType.Arrow, ProjectileType.Bolt, ProjectileType.Slinger, ProjectileType.Javelin };

                Plugin.Logger.LogInfo($"Verifying ranged damage: {projectileTypes.Length} projectiles x {units.Length} defenders...");

                foreach (var projectile in projectileTypes)
                {
                    foreach (var defender in units)
                    {
                        result.TotalTests++;

                        // Get the current damage from the game API
                        int currentGameApiDamage;
                        try
                        {
                            currentGameApiDamage = GetRangedDamage(projectile, defender);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogDebug($"Could not get {projectile} damage for {defender}: {ex.Message}");
                            result.SkippedCount++;
                            continue;
                        }

                        // Skip invalid entries
                        if (currentGameApiDamage < 0)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        // Get the ORIGINAL game default from CSV file or captured cache
                        // This is the "ground truth" - what the game originally had before any mods
                        int originalGameDefault = Config.DamageMatrix.Core.CsvMatrixReader.GetOriginalRangedDamage(projectile, defender);

                        if (originalGameDefault < 0)
                        {
                            // Not found in CSV/cache - skip this test
                            result.SkippedCount++;
                            continue;
                        }

                        // Compare current game API value vs original game default
                        // This verifies that our CSV/config system matches the original game values
                        if (currentGameApiDamage == originalGameDefault)
                        {
                            // Perfect match - current value matches the original game default
                            result.PassedTests++;
                        }
                        else
                        {
                            // Mismatch - current value differs from the original game default
                            // This indicates either:
                            // 1. User modified the CSV/TOML and it's been applied
                            // 2. There's a discrepancy between our config and the original game values
                            result.FailedTests++;
                            result.AddMismatch(projectile, defender, originalGameDefault, currentGameApiDamage);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Ranged verification failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Get ranged damage from game API for a specific projectile type.
        /// </summary>
        private int GetRangedDamage(ProjectileType projectile, eChimps defender)
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
                    throw new ArgumentException($"Unknown projectile type: {projectile}");
            }
        }

        /// <summary>
        /// Print ranged damage patterns for analysis.
        /// </summary>
        public void PrintRangedDamagePatterns()
        {
            Plugin.Logger.LogInfo("=== Ranged Damage Patterns ===");

            var sampleUnits = new[]
            {
                eChimps.CHIMP_TYPE_PEASANT,
                eChimps.CHIMP_TYPE_ARCHER,
                eChimps.CHIMP_TYPE_SWORDSMAN,
                eChimps.CHIMP_TYPE_KNIGHT,
                eChimps.CHIMP_TYPE_ARAB_SLAVE,
                eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            };

            var projectiles = new[] { ProjectileType.Arrow, ProjectileType.Bolt, ProjectileType.Slinger, ProjectileType.Javelin };

            foreach (var defender in sampleUnits)
            {
                var unitData = UnitDamageRegistry.GetUnitData(defender);
                string armorInfo = unitData != null ? $"Armor={unitData.ArmorValue:F2}" : "NO DATA";

                var damages = projectiles.Select(p =>
                {
                    try { return $"{p}={GetRangedDamage(p, defender)}"; }
                    catch { return $"{p}=N/A"; }
                });

                Plugin.Logger.LogInfo($"{defender} ({armorInfo}): {string.Join(", ", damages)}");
            }
        }
    }
}

