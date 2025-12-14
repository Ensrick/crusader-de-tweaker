// Systems/Verification/MeleeDamageVerifier.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.Verification
{
    /// <summary>
    /// Verifies melee damage calculations against the game's API.
    /// Compares DamageCalculator output to actual game values.
    /// </summary>
    internal class MeleeDamageVerifier : DamageVerifierBase
    {
        public override string VerifierName => "Melee Damage";

        /// <summary>
        /// Verify all melee damage calculations against the game API.
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

                Plugin.Logger.LogInfo($"Verifying melee damage: {units.Length} x {units.Length} = {units.Length * units.Length} matchups...");

                // Test each attacker-defender pair
                foreach (var attacker in units)
                {
                    foreach (var defender in units)
                    {
                        result.TotalTests++;

                        // Calculate what our formula predicts using ORIGINAL registry data (before TOML modifications)
                        // This ensures verification tests the calculation logic, not the current TOML values
                        int calculated = DamageCalculator.CalculateMeleeDamageWithOriginalData(attacker, defender);

                        if (calculated == -1)
                        {
                            // Missing data in our registry
                            result.SkippedCount++;
                            continue;
                        }

                        // Get the ORIGINAL game default from CSV file (not current game API)
                        // This is the "ground truth" - what the game originally had before any mods
                        int originalGameDefault = CsvMatrixReader.GetOriginalMeleeDamage(attacker, defender);

                        if (originalGameDefault < 0)
                        {
                            // Not found in CSV - skip this test
                            result.SkippedCount++;
                            continue;
                        }

                        // Compare calculated (TOML) vs original game default (CSV)
                        // This verifies that our calculation logic is correct, regardless of:
                        // - User modifications to TOML
                        // - CSV overrides applied to game API
                        if (calculated == originalGameDefault)
                        {
                            // Perfect match - our calculation matches the original game default
                            result.PassedTests++;
                        }
                        else
                        {
                            // Mismatch - our calculation doesn't match the original game default
                            // This indicates a calculation error in our damage system
                            result.FailedTests++;
                            result.AddMismatch(attacker, defender, originalGameDefault, calculated);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Melee verification failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Quick test of specific unit matchups.
        /// </summary>
        public void QuickTest()
        {
            Plugin.Logger.LogInfo("=== Quick Melee Damage Test ===");

            var testPairs = new[]
            {
                (eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_KNIGHT),
                (eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_PEASANT),
                (eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_ARAB_SLAVE),
                (eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_KNIGHT),
                (eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_PEASANT),
                (eChimps.CHIMP_TYPE_LORD, eChimps.CHIMP_TYPE_SWORDSMAN),
                (eChimps.CHIMP_TYPE_LORD, eChimps.CHIMP_TYPE_ARAB_SLAVE),
                (eChimps.CHIMP_TYPE_PIKEMAN, eChimps.CHIMP_TYPE_LADDERMAN),
            };

            foreach (var (attacker, defender) in testPairs)
            {
                int originalDefault = CsvMatrixReader.GetOriginalMeleeDamage(attacker, defender);
                int calculated = DamageCalculator.CalculateMeleeDamageWithOriginalData(attacker, defender);
                bool pass = calculated == originalDefault;
                string status = pass ? "✓ PASS" : "✗ FAIL";
                Plugin.Logger.LogInfo($"{status}: {attacker} -> {defender} | Original={originalDefault}, Calc={calculated}");
            }
        }

        /// <summary>
        /// Print mismatches grouped by attacker for pattern analysis.
        /// </summary>
        public void PrintMismatchesByAttacker(VerificationResult result)
        {
            if (result.Mismatches.Count == 0) return;

            Plugin.Logger.LogWarning("=== Mismatches by Attacker ===");

            var groupedByAttacker = result.Mismatches
                .Where(m => m.Attacker is eChimps)
                .GroupBy(m => (eChimps)m.Attacker)
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var group in groupedByAttacker)
            {
                var attacker = group.Key;
                var attackerData = UnitDamageRegistry.GetUnitData(attacker);
                string attackerInfo = attackerData != null
                    ? $"Base={attackerData.BaseMeleeDamage}, Tags=[{string.Join(",", attackerData.Tags)}]"
                    : "NO DATA";

                Plugin.Logger.LogWarning($"\n{attacker} ({attackerInfo}): {group.Count()} mismatches");

                // Show a few examples
                foreach (var mismatch in group.Take(3))
                {
                    var defenderData = UnitDamageRegistry.GetUnitData(mismatch.Defender);
                    string defenderInfo = defenderData != null
                        ? $"Armor={defenderData.ArmorValue:F2}"
                        : "NO DATA";

                    Plugin.Logger.LogWarning(
                        $"  -> {mismatch.Defender}: Game={mismatch.Expected}, Calc={mismatch.Calculated} ({defenderInfo})"
                    );
                }
            }
        }

        /// <summary>
        /// Print mismatches grouped by defender armor value.
        /// </summary>
        public void PrintMismatchesByDefenderArmor(VerificationResult result)
        {
            if (result.Mismatches.Count == 0) return;

            Plugin.Logger.LogWarning("=== Mismatches by Defender Armor ===");

            var groupedByArmor = result.Mismatches
                .Select(m => new
                {
                    Mismatch = m,
                    DefenderData = UnitDamageRegistry.GetUnitData(m.Defender)
                })
                .Where(x => x.DefenderData != null)
                .GroupBy(x => x.DefenderData.ArmorValue)
                .OrderBy(g => g.Key);

            foreach (var group in groupedByArmor)
            {
                int count = group.Count();
                var avgExpected = group.Average(x => x.Mismatch.Expected);
                var avgCalculated = group.Average(x => x.Mismatch.Calculated);
                float ratio = avgExpected > 0 ? (float)avgCalculated / (float)avgExpected : 0f;

                Plugin.Logger.LogWarning(
                    $"ArmorValue={group.Key:F2}: {count} mismatches, AvgGame={avgExpected:F1}, AvgCalc={avgCalculated:F1}, Ratio={ratio:F3}"
                );
            }
        }
    }
}

