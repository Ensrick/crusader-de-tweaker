// Systems/Verification/VerificationResult.cs
using CrusaderDETweaker.Data;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrusaderDETweaker.Systems.Verification
{
    internal class VerificationResult
    {
        public string VerificationType { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public int SkippedCount { get; set; }
        public string ErrorMessage { get; set; }

        public List<DamageMismatch> Mismatches { get; } = new List<DamageMismatch>();

        public float SuccessRate => TotalTests > 0 ? (float)PassedTests / (TotalTests - SkippedCount) * 100f : 0f;

        public void AddMismatch(object attacker, eChimps defender, int expected, int calculated)
        {
            Mismatches.Add(new DamageMismatch
            {
                Attacker = attacker,
                Defender = defender,
                Expected = expected,
                Calculated = calculated
            });
        }

        public void MergeFrom(VerificationResult otherResult)
        {
            TotalTests += otherResult.TotalTests;
            PassedTests += otherResult.PassedTests;
            FailedTests += otherResult.FailedTests;
            SkippedCount += otherResult.SkippedCount;
            Mismatches.AddRange(otherResult.Mismatches);
            if (!string.IsNullOrEmpty(otherResult.ErrorMessage))
            {
                ErrorMessage = (ErrorMessage ?? "") + otherResult.ErrorMessage + "; ";
            }
        }

        public void PrintSummary()
        {
            Plugin.Logger.LogInfo($"=== {VerificationType} Verification Results ===");
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                Plugin.Logger.LogError($"Error: {ErrorMessage}");
                return;
            }
            Plugin.Logger.LogInfo($"Total: {TotalTests}, Passed: {PassedTests}, Failed: {FailedTests}, Skipped: {SkippedCount}");
            Plugin.Logger.LogInfo($"Success Rate: {SuccessRate:F2}%");
        }

        public void PrintAllMismatches()
        {
            if (Mismatches.Count == 0)
            {
                Plugin.Logger.LogInfo("No mismatches found!");
                return;
            }

            Plugin.Logger.LogWarning($"=== All {Mismatches.Count} Mismatches for {VerificationType} ===");

            foreach (var mismatch in Mismatches.OrderBy(x => x.Attacker.ToString()).ThenBy(x => x.Defender.ToString()))
            {
                var defenderData = UnitDamageRegistry.GetUnitData(mismatch.Defender);
                string defenderArmor = defenderData?.ArmorValue.ToString("F2") ?? "N/A";
                string defenderTags = defenderData != null ? string.Join(";", defenderData.Tags) : "N/A";

                string attackerBase = "N/A";
                string attackerTags = "N/A";

                if (mismatch.Attacker is eChimps attackerChimp)
                {
                    var attackerData = UnitDamageRegistry.GetUnitData(attackerChimp);
                    if (attackerData != null)
                    {
                        attackerBase = attackerData.BaseMeleeDamage.ToString();
                        attackerTags = string.Join(";", attackerData.Tags);
                    }
                }

                int diff = mismatch.Calculated - mismatch.Expected;
                float ratio = mismatch.Expected > 0 ? (float)mismatch.Calculated / mismatch.Expected : 0f;

                Plugin.Logger.LogWarning(
                    $"  -> Att: {mismatch.Attacker} (Base={attackerBase}, Tags=[{attackerTags}]), " +
                    $"Def: {mismatch.Defender} (Armor={defenderArmor}, Tags=[{defenderTags}]) | " +
                    $"Game={mismatch.Expected}, Calc={mismatch.Calculated}, Diff={diff}, Ratio={ratio:F3}"
                );
            }
        }

    }

    internal class DamageMismatch
    {
        public object Attacker { get; set; }
        public eChimps Defender { get; set; }
        public int Expected { get; set; }
        public int Calculated { get; set; }
    }
}
