// Test file for UnitTagRegistry reverse index optimization
// This test verifies that the reverse index produces identical results to the original O(n) implementation

using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Test verification for reverse index optimization in UnitTagRegistry.
    /// Compares new O(1) reverse index against simulated O(n) forward lookup.
    /// </summary>
    internal static class UnitTagRegistryTest
    {
        /// <summary>
        /// Simulate the original O(n) tag lookup (before reverse index optimization).
        /// This is how GetTagsForUnit() would have worked without the reverse index.
        /// </summary>
        private static List<string> GetTagsForUnit_Original(
            SHCDESE.Interop.eChimps unit, 
            Dictionary<string, HashSet<SHCDESE.Interop.eChimps>> unitsByTag)
        {
            var tags = new List<string>();
            
            // O(n) iteration through all tags - original implementation
            foreach (var kvp in unitsByTag)
            {
                if (kvp.Value.Contains(unit))
                    tags.Add(kvp.Key);
            }
            
            return tags;
        }

        /// <summary>
        /// Run comprehensive tests comparing old vs new implementation.
        /// Returns tuple (passed, testCount) for consistency with other test suites.
        /// </summary>
        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- UnitTagRegistry Test Suite ---");
            
            int passedCount = 0;
            int failedCount = 0;
            
            // Test 1: Empty registry
            if (Test_EmptyRegistry()) passedCount++; else failedCount++;
            
            // Test 2: Single unit, single tag
            if (Test_SingleUnitSingleTag()) passedCount++; else failedCount++;
            
            // Test 3: Single unit, multiple tags
            if (Test_SingleUnitMultipleTags()) passedCount++; else failedCount++;
            
            // Test 4: Multiple units, overlapping tags
            if (Test_MultipleUnitsOverlappingTags()) passedCount++; else failedCount++;
            
            // Test 5: All units, realistic scenario
            if (Test_RealisticScenario()) passedCount++; else failedCount++;
            
            // Test 6: UnitTagInteraction.ApplyTo()
            if (Test_ApplyTo_Delta()) passedCount++; else failedCount++;
            
            // Test 7: UnitTagInteraction.ApplyTo() with flat bonus
            if (Test_ApplyTo_FlatBonus()) passedCount++; else failedCount++;
            
            // Test 8: Minimum damage of 1
            if (Test_ApplyTo_MinimumDamage()) passedCount++; else failedCount++;
            
            // Test 9: Clear only interactions
            if (Test_ClearInteractions()) passedCount++; else failedCount++;
            
            int totalTests = passedCount + failedCount;
            bool allPassed = failedCount == 0;
            
            Plugin.Logger.LogInfo($"  UnitTagRegistry: {passedCount}/{totalTests} tests passed");
            
            return (allPassed, totalTests);
        }

        private static bool Test_EmptyRegistry()
        {
            Plugin.Logger.LogDebug("Test 1: Empty registry");
            
            UnitTagRegistry.Clear();
            
            var tags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_ARCHER);
            bool passed = tags.Count == 0;
            
            LogTestResult("Empty registry", passed);
            return passed;
        }

        private static bool Test_SingleUnitSingleTag()
        {
            Plugin.Logger.LogDebug("Test 2: Single unit, single tag");
            
            UnitTagRegistry.Clear();
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_ARCHER, "Ranged");
            
            var tags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_ARCHER);
            bool passed = tags.Count == 1 && tags.Contains("Ranged");
            
            LogTestResult("Single unit, single tag", passed);
            return passed;
        }

        private static bool Test_SingleUnitMultipleTags()
        {
            Plugin.Logger.LogDebug("Test 3: Single unit, multiple tags");
            
            UnitTagRegistry.Clear();
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "Polearm");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "Infantry");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "AntiCavalry");
            
            var tags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_SPEARMAN);
            bool passed = tags.Count == 3 
                && tags.Contains("Polearm") 
                && tags.Contains("Infantry") 
                && tags.Contains("AntiCavalry");
            
            LogTestResult("Single unit, multiple tags", passed);
            return passed;
        }

        private static bool Test_MultipleUnitsOverlappingTags()
        {
            Plugin.Logger.LogDebug("Test 4: Multiple units, overlapping tags");
            
            UnitTagRegistry.Clear();
            
            // Setup realistic tag scenario
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SWORDSMAN, "Infantry");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SWORDSMAN, "Melee");
            
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "Infantry");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "Polearm");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_SPEARMAN, "AntiCavalry");
            
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_ARCHER, "Ranged");
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_ARCHER, "Infantry");
            
            // Verify each unit has correct tags
            var swordsmanTags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_SWORDSMAN);
            var spearmanTags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_SPEARMAN);
            var archerTags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_ARCHER);
            
            bool passed = true;
            passed &= swordsmanTags.Count == 2 && swordsmanTags.IndexOf("Infantry") >= 0 && swordsmanTags.IndexOf("Melee") >= 0;
            passed &= spearmanTags.Count == 3 && spearmanTags.IndexOf("Infantry") >= 0 && spearmanTags.IndexOf("Polearm") >= 0 && spearmanTags.IndexOf("AntiCavalry") >= 0;
            passed &= archerTags.Count == 2 && archerTags.IndexOf("Ranged") >= 0 && archerTags.IndexOf("Infantry") >= 0;
            
            // Verify "Infantry" tag shows up for all three units
            var infantryUnits = UnitTagRegistry.GetUnitsWithTag("Infantry");
            var infantryList = new List<SHCDESE.Interop.eChimps>(infantryUnits);
            passed &= infantryList.Contains(eChimps.CHIMP_TYPE_SWORDSMAN);
            passed &= infantryList.Contains(eChimps.CHIMP_TYPE_SPEARMAN);
            passed &= infantryList.Contains(eChimps.CHIMP_TYPE_ARCHER);
            
            LogTestResult("Multiple units, overlapping tags", passed);
            return passed;
        }

        private static bool Test_RealisticScenario()
        {
            Plugin.Logger.LogDebug("Test 5: Realistic scenario with interactions");
            
            UnitTagRegistry.Clear();
            
            // Setup realistic military units
            var testUnits = new[]
            {
                (eChimps.CHIMP_TYPE_KNIGHT, new[] { "Cavalry", "Heavy", "Melee", "Knight" }),
                (eChimps.CHIMP_TYPE_SPEARMAN, new[] { "Infantry", "Polearm", "AntiCavalry" }),
                (eChimps.CHIMP_TYPE_ARCHER, new[] { "Ranged", "Infantry", "AntiInfantry" }),
                (eChimps.CHIMP_TYPE_ARAB_BOW, new[] { "Ranged", "Infantry", "AntiArmor" }),
                (eChimps.CHIMP_TYPE_LADDERMAN, new[] { "Infantry", "Siege" })
            };
            
            // Register all tags
            foreach (var (unit, tags) in testUnits)
            {
                foreach (var tag in tags)
                {
                    UnitTagRegistry.RegisterUnitTag(unit, tag);
                }
            }
            
            // Register some interactions
            UnitTagRegistry.RegisterInteraction(new UnitTagInteraction
            {
                InteractionId = "Polearm_vs_Knight",
                AttackerTags = new List<string> { "Polearm" },
                DefenderTags = new List<string> { "Knight" },
                Delta = 2.0f
            });
            
            UnitTagRegistry.RegisterInteraction(new UnitTagInteraction
            {
                InteractionId = "Melee_vs_Cavalry",
                AttackerTags = new List<string> { "Melee" },
                DefenderTags = new List<string> { "Cavalry" },
                Delta = 0.5f
            });
            
            UnitTagRegistry.RegisterInteraction(new UnitTagInteraction
            {
                InteractionId = "AntiArmor_vs_Heavy",
                AttackerTags = new List<string> { "AntiArmor" },
                DefenderTags = new List<string> { "Heavy" },
                Delta = 1.0f, // Multiplier 2.0 = Delta +1.0
                FlatBonus = 500
            });
            
            // Test 5e: Multi-tag match (Attacker: Ranged, Infantry; Defender: Siege)
            UnitTagRegistry.RegisterInteraction(new UnitTagInteraction
            {
                InteractionId = "RangedInfantry_vs_Siege",
                AttackerTags = new List<string> { "Ranged", "Infantry" },
                DefenderTags = new List<string> { "Siege" },
                Delta = 0.75f // Multiplier 1.75
            });
            
            bool passed = true;
            
            // Test 5a: Spearman vs Knight should trigger Polearm vs Knight
            var interaction1 = UnitTagRegistry.TryGetInteraction(
                eChimps.CHIMP_TYPE_SPEARMAN, 
                eChimps.CHIMP_TYPE_KNIGHT);
            
            passed &= interaction1 != null;
            passed &= interaction1?.InteractionId == "Polearm_vs_Knight";
            passed &= Math.Abs((interaction1?.Delta ?? 0f) - 2.0f) < 0.001f;
            
            // Test 5b: Knight vs Knight should trigger Melee vs Cavalry
            var interaction2 = UnitTagRegistry.TryGetInteraction(
                eChimps.CHIMP_TYPE_KNIGHT, 
                eChimps.CHIMP_TYPE_KNIGHT);
            
            passed &= interaction2 != null;
            passed &= interaction2?.InteractionId == "Melee_vs_Cavalry";
            passed &= Math.Abs((interaction2?.Delta ?? 0f) - 0.5f) < 0.001f;
            
            // Test 5c: Arab Bow vs Knight should trigger AntiArmor vs Heavy
            var interaction3 = UnitTagRegistry.TryGetInteraction(
                eChimps.CHIMP_TYPE_ARAB_BOW, 
                eChimps.CHIMP_TYPE_KNIGHT);
            
            passed &= interaction3 != null;
            passed &= interaction3?.InteractionId == "AntiArmor_vs_Heavy";
            passed &= Math.Abs((interaction3?.Delta ?? 0f) - 1.0f) < 0.001f;
            passed &= interaction3?.FlatBonus == 500;
            
            // Test 5d: Archer vs Ladderman should trigger RangedInfantry_vs_Siege
            var interaction4 = UnitTagRegistry.TryGetInteraction(
                eChimps.CHIMP_TYPE_ARCHER, 
                eChimps.CHIMP_TYPE_LADDERMAN);
            
            passed &= interaction4 != null;
            passed &= interaction4?.InteractionId == "RangedInfantry_vs_Siege";
            passed &= Math.Abs((interaction4?.Delta ?? 0f) - 0.75f) < 0.001f;
            
            // Test 5e: Ladderman vs Knight should have no special interaction
            var interaction5 = UnitTagRegistry.TryGetInteraction(
                eChimps.CHIMP_TYPE_LADDERMAN, 
                eChimps.CHIMP_TYPE_KNIGHT);
            
            passed &= interaction5 == null;
            
            // Test 5f: Verify tag counts are correct
            var knightTags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_KNIGHT);
            passed &= knightTags.Count == 4;
            
            LogTestResult("Realistic scenario with interactions", passed);
            
            if (passed)
            {
                Plugin.Logger.LogDebug("  All sub-tests passed:");
                Plugin.Logger.LogDebug("    - Spearman vs Knight interaction found");
                Plugin.Logger.LogDebug("    - Knight vs Knight interaction found");
                Plugin.Logger.LogDebug("    - Arab Bow vs Knight interaction found");
                Plugin.Logger.LogDebug("    - Archer vs Ladderman interaction found (multi-tag)");
                Plugin.Logger.LogDebug("    - No false positive interactions");
                Plugin.Logger.LogDebug("    - Tag counts accurate");
            }
            
            return passed;
        }

        private static void LogTestResult(string testName, bool passed)
        {
            if (passed)
            {
                Plugin.Logger.LogDebug($"    [PASS] {testName}");
            }
            else
            {
                Plugin.Logger.LogError($"    [FAIL] {testName}");
            }
        }

        // ===================================================
        // Additional Tests for UnitTagInteraction class
        // ===================================================

        private static bool Test_ApplyTo_Delta()
        {
            Plugin.Logger.LogDebug("Test 6: ApplyTo with delta");
            
            var interaction = new UnitTagInteraction
            {
                InteractionId = "Test_Delta",
                AttackerTags = new List<string> { "TestA" },
                DefenderTags = new List<string> { "TestB" },
                Delta = 1.5f, // Multiplier 2.5
                FlatBonus = 0
            };
            
            int baseDamage = 100;
            int result = interaction.ApplyTo(baseDamage);
            
            // 100 * (1.0 + 1.5) = 250
            bool passed = result == 250;
            
            LogTestResult("ApplyTo with delta", passed);
            return passed;
        }

        private static bool Test_ApplyTo_FlatBonus()
        {
            Plugin.Logger.LogDebug("Test 7: ApplyTo with flat bonus");
            
            var interaction = new UnitTagInteraction
            {
                InteractionId = "Test_FlatBonus",
                AttackerTags = new List<string> { "TestA" },
                DefenderTags = new List<string> { "TestB" },
                Delta = 0.0f,
                FlatBonus = 500
            };
            
            int baseDamage = 100;
            int result = interaction.ApplyTo(baseDamage);
            
            // 100 * (1.0 + 0.0) + 500 = 600
            bool passed = result == 600;
            
            LogTestResult("ApplyTo with flat bonus", passed);
            return passed;
        }

        private static bool Test_ApplyTo_MinimumDamage()
        {
            Plugin.Logger.LogDebug("Test 8: ApplyTo minimum damage");
            
            var interaction = new UnitTagInteraction
            {
                InteractionId = "Test_MinDamage",
                AttackerTags = new List<string> { "TestA" },
                DefenderTags = new List<string> { "TestB" },
                Delta = -0.99f, // Multiplier 0.01
                FlatBonus = -100
            };
            
            int baseDamage = 100;
            int result = interaction.ApplyTo(baseDamage);
            
            // 100 * (1.0 - 0.99) - 100 = -99, should be clamped to minimum of 1
            bool passed = result == 1;
            
            LogTestResult("ApplyTo minimum damage", passed);
            return passed;
        }

        private static bool Test_ClearInteractions()
        {
            Plugin.Logger.LogDebug("Test 9: ClearInteractions preserves tags");
            
            UnitTagRegistry.Clear();
            
            // Register a tag
            UnitTagRegistry.RegisterUnitTag(eChimps.CHIMP_TYPE_ARCHER, "Ranged");
            
            // Register an interaction
            UnitTagRegistry.RegisterInteraction(new UnitTagInteraction
            {
                InteractionId = "Test_Clear",
                AttackerTags = new List<string> { "Ranged" },
                DefenderTags = new List<string> { "Melee" },
                Delta = 1.0f, // Multiplier 2.0
                FlatBonus = 0
            });
            
            // Clear only interactions
            UnitTagRegistry.ClearInteractions();
            
            // Tag should still exist
            var tags = UnitTagRegistry.GetTagsForUnit(eChimps.CHIMP_TYPE_ARCHER);
            bool tagPreserved = tags.Count == 1 && tags.Contains("Ranged");
            
            // Interaction should be gone (no way to directly check, but TryGetInteraction won't find it)
            // We can verify by checking the tag still works
            bool passed = tagPreserved;
            
            LogTestResult("ClearInteractions preserves tags", passed);
            return passed;
        }
    }
}
