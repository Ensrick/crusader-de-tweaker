// Tests/ApothecaryHealingTest.cs
//
// PURPOSE: Unit tests for the ["Apothecary Healing"] GameplaySettings section (Config/Core/ApothecaryHealing.cs).
//
// TESTS COVER:
// - Parsing: missing section / -1 / 0 = off, percent to basis points (decimals), clamps, wrong types
// - Heal amount: percent of max health (integer maths), raw HP, both added, minimum 1 while on
// - Distance from a unit tile to the building footprint (inside = 0, edges, corners)
// - Which unit types are healed (soldiers and the Lord, not workers / siege engines / animals)
//
// NOTE: pure logic - no game objects, no file I/O. Runs on every launch via CoreTestRunner. The in-game
// behaviour (real apothecary, real units, combat delay) is covered by Tests/HeadlessSelfTest.cs.
//
using System.Collections.Generic;
using CrusaderDETweaker.Config.Core;
using SHCDESE.Interop;
using Tomlyn.Model;

namespace CrusaderDETweaker.Tests
{
    internal static class ApothecaryHealingTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- ApothecaryHealing Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_Parse_MissingSectionIsOff();
            Test_Parse_MinusOneAndZeroAreOff();
            Test_Parse_PercentDecimals();
            Test_Parse_ClampsAndWarns();
            Test_Parse_WrongTypes();
            Test_HealAmount();
            Test_Distance();
            Test_Healable();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  ApothecaryHealing: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        private static ApothecaryHealing.Settings Parse(TomlTable t, List<string> warnings) =>
            ApothecaryHealing.Parse(t, w => warnings.Add(w));

        private static void Test_Parse_MissingSectionIsOff()
        {
            var w = new List<string>();
            var s = Parse(null, w);
            Check("Parse_MissingSectionIsOff", !s.Enabled && s.RadiusTiles == ApothecaryHealing.DefaultRadiusTiles
                && s.IntervalTicks == ApothecaryHealing.DefaultIntervalTicks && s.NeedsWorker && w.Count == 0, $"settings={s} warnings={w.Count}");
        }

        private static void Test_Parse_MinusOneAndZeroAreOff()
        {
            var w = new List<string>();
            var a = Parse(new TomlTable { ["HealPercent"] = -1L, ["HealHitPoints"] = -1L, ["RadiusTiles"] = -1L, ["IntervalTicks"] = -1L }, w);
            var b = Parse(new TomlTable { ["HealPercent"] = 0.0, ["HealHitPoints"] = 0L }, w);
            Check("Parse_MinusOneAndZeroAreOff", !a.Enabled && !b.Enabled && a.RadiusTiles == ApothecaryHealing.DefaultRadiusTiles
                && a.IntervalTicks == ApothecaryHealing.DefaultIntervalTicks && w.Count == 0, $"a={a} b={b} warnings={string.Join(" | ", w)}");
        }

        private static void Test_Parse_PercentDecimals()
        {
            var w = new List<string>();
            var s = Parse(new TomlTable { ["HealPercent"] = 2.5, ["RadiusTiles"] = 12L, ["IntervalTicks"] = 40L, ["OutOfCombatTicks"] = 0L, ["NeedsWorker"] = false }, w);
            var i = Parse(new TomlTable { ["HealPercent"] = 10L }, w);
            Check("Parse_PercentDecimals", s.Enabled && s.HealBasisPoints == 250 && s.HealHitPoints == -1 && s.RadiusTiles == 12 && s.IntervalTicks == 40
                && s.OutOfCombatTicks == 0 && !s.NeedsWorker && i.HealBasisPoints == 1000 && w.Count == 0, $"s={s} bp={s.HealBasisPoints} i.bp={i.HealBasisPoints}");
        }

        private static void Test_Parse_ClampsAndWarns()
        {
            var w = new List<string>();
            var s = Parse(new TomlTable { ["HealPercent"] = 250L, ["RadiusTiles"] = 5000L, ["IntervalTicks"] = 0L, ["OutOfCombatTicks"] = -5L }, w);
            Check("Parse_ClampsAndWarns", s.HealBasisPoints == 10000 && s.RadiusTiles == ApothecaryHealing.MaxRadiusTiles && s.IntervalTicks == 1
                && s.OutOfCombatTicks == 0 && w.Count == 4, $"s={s} warnings={w.Count}: {string.Join(" | ", w)}");
        }

        private static void Test_Parse_WrongTypes()
        {
            var w = new List<string>();
            var s = Parse(new TomlTable { ["HealPercent"] = "5", ["HealHitPoints"] = 300L, ["RadiusTiles"] = 2.5, ["NeedsWorker"] = "no" }, w);
            Check("Parse_WrongTypes", s.HealBasisPoints == -1 && s.HealHitPoints == 300 && s.RadiusTiles == ApothecaryHealing.DefaultRadiusTiles
                && s.NeedsWorker && w.Count == 3, $"s={s} warnings={w.Count}: {string.Join(" | ", w)}");
        }

        private static void Test_HealAmount()
        {
            var pct = new ApothecaryHealing.Settings { HealBasisPoints = 1000 };
            var raw = new ApothecaryHealing.Settings { HealHitPoints = 300 };
            var both = new ApothecaryHealing.Settings { HealBasisPoints = 250, HealHitPoints = 100 };
            var tiny = new ApothecaryHealing.Settings { HealBasisPoints = 1 };
            var off = new ApothecaryHealing.Settings();
            int a = ApothecaryHealing.HealAmount(15000, pct), b = ApothecaryHealing.HealAmount(15000, raw);
            int c = ApothecaryHealing.HealAmount(10001, both), d = ApothecaryHealing.HealAmount(50, tiny), e = ApothecaryHealing.HealAmount(15000, off);
            int big = ApothecaryHealing.HealAmount(int.MaxValue, new ApothecaryHealing.Settings { HealBasisPoints = 10000, HealHitPoints = int.MaxValue });
            Check("HealAmount", a == 1500 && b == 300 && c == 350 && d == 1 && e == 0 && big == int.MaxValue,
                $"10% of 15000={a}, 300 HP={b}, 2.5% of 10001 + 100={c}, 0.01% of 50={d}, off={e}, overflow={big}");
        }

        private static void Test_Distance()
        {
            // Footprint 10..14 x 20..24.
            long inside = ApothecaryHealing.DistanceSquaredToFootprint(12, 22, 10, 20, 14, 24);
            long west = ApothecaryHealing.DistanceSquaredToFootprint(6, 22, 10, 20, 14, 24);
            long corner = ApothecaryHealing.DistanceSquaredToFootprint(17, 28, 10, 20, 14, 24);
            long edge = ApothecaryHealing.DistanceSquaredToFootprint(14, 25, 10, 20, 14, 24);
            Check("Distance", inside == 0 && west == 16 && corner == 25 && edge == 1, $"inside={inside} west={west} corner={corner} edge={edge}");
        }

        private static void Test_Healable()
        {
            bool soldiers = ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_SPEARMAN) && ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_KNIGHT)
                && ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_BEDOUIN_HEALER) && ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_LORD);
            bool others = ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_PEASANT) || ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_HEALER)
                || ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_CATAPULT) || ApothecaryHealing.IsHealable(eChimps.CHIMP_TYPE_COW);
            Check("Healable", soldiers && !others, $"soldiers+Lord={soldiers}, workers/siege/animals={others}");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                _passedCount++;
                Plugin.Logger.LogDebug($"    [PASS] {name}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"    [FAIL] {name}: {detail}");
            }
        }
    }
}
