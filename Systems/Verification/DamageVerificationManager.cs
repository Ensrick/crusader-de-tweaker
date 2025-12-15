// Systems/Verification/DamageVerificationManager.cs
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Systems.Verification
{
    /// <summary>
    /// Manages and orchestrates all damage verification systems.
    /// </summary>
    internal static class DamageVerificationManager
    {
        private static readonly List<IDamageVerifier> _verifiers = new List<IDamageVerifier>();

        /// <summary>
        /// Initialize all verifiers.
        /// </summary>
        static DamageVerificationManager()
        {
            _verifiers.Add(new MeleeDamageVerifier());
            _verifiers.Add(new RangedDamageVerifier());
            _verifiers.Add(new EunuchAoeDamageVerifier());
        }

        /// <summary>
        /// Run all verifications and return combined results.
        /// </summary>
        public static VerificationResult VerifyAll()
        {
            var combinedResult = new VerificationResult { VerificationType = "All Damage Systems" };

            Plugin.Logger.LogInfo("=== Starting Full Damage System Verification ===");

            foreach (var verifier in _verifiers)
            {
                Plugin.Logger.LogInfo($"\n--- Running {verifier.VerifierName} Verification ---");

                var result = verifier.Verify();
                result.PrintSummary();

                if (result.FailedTests > 0)
                {
                    result.PrintAllMismatches();
                }

                combinedResult.MergeFrom(result);
            }

            Plugin.Logger.LogInfo("\n=== Combined Verification Results ===");
            combinedResult.VerificationType = "Combined (All Systems)";
            combinedResult.PrintSummary();

            return combinedResult;
        }

        /// <summary>
        /// Run only melee damage verification with detailed analysis.
        /// </summary>
        public static VerificationResult VerifyMeleeDamage()
        {
            var verifier = new MeleeDamageVerifier();
            var result = verifier.Verify();

            result.PrintSummary();

            if (result.FailedTests > 0)
            {
                result.PrintAllMismatches();
                verifier.PrintMismatchesByAttacker(result);
                verifier.PrintMismatchesByDefenderArmor(result);
            }

            verifier.QuickTest();

            return result;
        }

        /// <summary>
        /// Run only ranged damage verification with detailed analysis.
        /// </summary>
        public static VerificationResult VerifyRangedDamage()
        {
            var verifier = new RangedDamageVerifier();
            var result = verifier.Verify();

            result.PrintSummary();

            if (result.FailedTests > 0)
            {
                result.PrintAllMismatches();
            }

            verifier.PrintRangedDamagePatterns();

            return result;
        }

        /// <summary>
        /// Run only Eunuch AOE damage verification with detailed analysis.
        /// </summary>
        public static VerificationResult VerifyEunuchAoeDamage()
        {
            var verifier = new EunuchAoeDamageVerifier();
            var result = verifier.Verify();

            result.PrintSummary();

            if (result.FailedTests > 0)
            {
                result.PrintAllMismatches();
            }

            verifier.PrintAoeDamagePatterns();

            return result;
        }

        /// <summary>
        /// Quick diagnostic test of key damage interactions.
        /// </summary>
        public static void QuickDiagnostic()
        {
            Plugin.Logger.LogInfo("=== Quick Damage System Diagnostic ===");

            // Test melee
            var meleeVerifier = new MeleeDamageVerifier();
            meleeVerifier.QuickTest();

            // Print ranged patterns
            var rangedVerifier = new RangedDamageVerifier();
            rangedVerifier.PrintRangedDamagePatterns();

            // Print AOE patterns
            var aoeVerifier = new EunuchAoeDamageVerifier();
            aoeVerifier.PrintAoeDamagePatterns();
        }
    }
}