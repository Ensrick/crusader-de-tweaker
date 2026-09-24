// Tests/TemplateBaselineTest.cs
//
// PURPOSE: Unit tests for the template baseline journal (Config/Core/TemplateBaseline.cs, BaselineJournal),
//          the core of ConfigLoader.ReapplyTemplateConfigs, the one path that writes the game tables.
//
// TESTS COVER:
// - First write wins (the remembered value is the game's own), restore runs newest-first,
//   unreadable cells are counted, not restored
// - The one template write path is idempotent: applying N times (with or without SE map-unload resets
//   between) equals applying once, including the fire / heal / wall-cost multipliers that scale the
//   current value; the ranged multiplier is applied once (at hit time only, not by the matrix loader)
//
// NOTE: pure logic only - no file I/O, no game API. Runs on every launch via CoreTestRunner.
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.Core;

namespace CrusaderDETweaker.Tests
{
    internal static class TemplateBaselineTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- TemplateBaseline Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_Baseline_FirstWriteWinsAndRestoreOrder();
            Test_Reapply_NTimesEqualsOnce();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  TemplateBaseline: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        private static void Test_Baseline_FirstWriteWinsAndRestoreOrder()
        {
            var journal = new BaselineJournal();
            var restoredOrder = new List<string>();
            int cell = 10; // "game value"

            journal.BeforeWrite("a", () => { int original = cell; return () => { cell = original; restoredOrder.Add("a"); }; });
            cell = 20; // first mod write
            journal.BeforeWrite("a", () => { int original = cell; return () => { cell = original; restoredOrder.Add("a2"); }; });
            cell = 30; // second mod write of the same cell must not replace the remembered 10
            journal.BeforeWrite("b", () => () => restoredOrder.Add("b"));
            journal.BeforeWrite("unreadable", () => null);

            var (restored, failed) = journal.RestoreAll();
            Check("Baseline_FirstWriteWinsAndRestoreOrder",
                cell == 10 && restored == 2 && failed == 0 && journal.Count == 2 && journal.UnreadableCount == 1
                && restoredOrder.Count == 2 && restoredOrder[0] == "b" && restoredOrder[1] == "a",
                $"cell={cell} restored={restored} order={string.Join(",", restoredOrder)}");
        }

        /// <summary>
        /// Models the real writers: raw CSV cells (melee, ranged: the ranged loader writes the CSV value
        /// unscaled since 2.6.8), a cell the CSV skips (-1) that the fire multiplier scales, a CSV cell the
        /// multiplier then scales, and a value SE never resets (wall cost). Applying N times, with and without
        /// SE resets between, must equal applying once, and a ranged hit must see the multiplier exactly once.
        /// </summary>
        private static void Test_Reapply_NTimesEqualsOnce()
        {
            var vanilla = new Dictionary<string, double> { { "melee", 50 }, { "ranged", 40 }, { "fireSkip", 10 }, { "fireCsv", 12 }, { "wallCost", 0.25 } };
            var table = new Dictionary<string, double>(vanilla);
            var seManaged = new[] { "melee", "ranged", "fireSkip", "fireCsv" };   // ClearOverrides on unload
            const double fireMultiplier = 2.0, rangedMultiplier = 1.5;
            var journal = new BaselineJournal();

            void Write(string key, double value)
            {
                journal.BeforeWrite(key, () => { double original = table[key]; return () => table[key] = original; });
                table[key] = value;
            }
            var steps = new Action[]
            {
                () => { Write("melee", 100); Write("ranged", 80); Write("fireCsv", 30); },   // matrices (fireSkip is -1)
                () => { Write("fireSkip", table["fireSkip"] * fireMultiplier); Write("fireCsv", table["fireCsv"] * fireMultiplier); Write("wallCost", 0.5); }
            };
            void SeUnloadReset() { foreach (var k in seManaged) table[k] = vanilla[k]; }

            journal.Reapply(steps);
            var once = new Dictionary<string, double>(table);

            for (int i = 0; i < 5; i++) journal.Reapply(steps);                        // no reset between
            bool sameWithoutReset = DictEqual(table, once);
            for (int i = 0; i < 5; i++) { SeUnloadReset(); journal.Reapply(steps); }  // reset between
            bool sameWithReset = DictEqual(table, once);

            double rangedHit = table["ranged"] * rangedMultiplier;                    // hit-time hook applies it once
            Check("Reapply_NTimesEqualsOnce",
                sameWithoutReset && sameWithReset
                && once["melee"] == 100 && once["ranged"] == 80 && once["fireSkip"] == 20 && once["fireCsv"] == 60 && once["wallCost"] == 0.5
                && rangedHit == 120,
                $"once: fireSkip={once["fireSkip"]} fireCsv={once["fireCsv"]}; now: fireSkip={table["fireSkip"]} fireCsv={table["fireCsv"]} ranged={table["ranged"]}");
        }

        private static bool DictEqual(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
                if (!b.TryGetValue(kvp.Key, out double v) || v != kvp.Value) return false;
            return true;
        }

        private static void Check(string testName, bool condition, string failureDetail)
        {
            if (condition)
            {
                _passedCount++;
                Plugin.Logger.LogDebug($"    [PASS] {testName}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"    [FAIL] {testName}: {failureDetail}");
            }
        }
    }
}
