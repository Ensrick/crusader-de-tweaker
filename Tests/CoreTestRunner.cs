// Tests/CoreTestRunner.cs
//
// PURPOSE: Orchestrates all unit tests for core logic components.
//
// USAGE:
// - Called by Plugin.cs during initialization as an on-load QA self-check
// - Returns true if all tests pass, false otherwise
// - Individual test suites can be run independently
//
// TEST SUITES:
// - PropertyHandler, PropertyRegistry, EntityProcessor: Core system tests
//
// IMPORTANT FOR AI AGENTS:
// - Tests use BepInEx logger for output (visible in LogOutput.log)
// - No external test framework - uses simple assertion pattern
//
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Central test runner that orchestrates all unit test suites.
    /// </summary>
    internal static class CoreTestRunner
    {
        /// <summary>
        /// Run all unit tests for core logic components.
        /// Returns true if all tests pass.
        /// </summary>
        public static bool RunAllTests()
        {
            Plugin.Logger.LogInfo("========================================");
            Plugin.Logger.LogInfo("=== CORE LOGIC UNIT TEST SUITE ===");
            Plugin.Logger.LogInfo("========================================");

            var results = new List<(string suiteName, bool passed, int testCount)>
            {
                // Run each test suite
                RunSuite("PropertyHandler", PropertyHandlerTest.RunTests),
                RunSuite("PropertyRegistry", PropertyRegistryTest.RunTests),
                RunSuite("EntityProcessor", EntityProcessorTest.RunTests),
                RunSuite("CsvHelper", CsvHelperTest.RunTests)
            };

            // Summary
            Plugin.Logger.LogInfo("========================================");
            Plugin.Logger.LogInfo("=== TEST SUMMARY ===");

            int totalPassed = 0;
            int totalFailed = 0;

            foreach (var (suiteName, passed, testCount) in results)
            {
                string status = passed ? "[PASS]" : "[FAIL]";
                Plugin.Logger.LogInfo($"  {status} {suiteName}: {testCount} test(s)");

                if (passed)
                    totalPassed++;
                else
                    totalFailed++;
            }

            Plugin.Logger.LogInfo("========================================");

            if (totalFailed == 0)
            {
                Plugin.Logger.LogInfo($"=== ALL {totalPassed} TEST SUITES PASSED ===");
                return true;
            }
            else
            {
                Plugin.Logger.LogError($"=== {totalFailed} TEST SUITE(S) FAILED ===");
                return false;
            }
        }

        private static (string, bool, int) RunSuite(string name, Func<(bool passed, int testCount)> runTests)
        {
            try
            {
                var (passed, testCount) = runTests();
                return (name, passed, testCount);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Test suite '{name}' threw exception: {ex.Message}");
                Plugin.Logger.LogDebug(ex.StackTrace);
                return (name, false, 0);
            }
        }
    }
}
