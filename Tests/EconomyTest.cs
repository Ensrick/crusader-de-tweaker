// Tests/EconomyTest.cs
//
// PURPOSE: Unit tests for the economy settings' pure logic: the Units file GoodYieldMultiplier scaling
//          (Config/Core/GoodYield.cs) and the GameplaySettings ["Skirmish Starting Troops"] parsing and apply decision
//          (Config/Core/SkirmishStartingTroops.cs).
//
// TESTS COVER:
// - GoodYield.Scale: whole multipliers, fractional multipliers carried over trips (1.25 on 3 = 3, 4, 4, 4), halves,
//   the 16-bit clamp, the game's multiplier and a zero base left untouched
// - Starting troops: parsing (counts, -1, clamp, unknown keys, wrong types, ApplyToAI), the slot list (no unread
//   slot, no duplicates) and the OnStartMap Post decision (queue rebuilt, custom skirmish only, start option 1-3)
//
// NOTE: pure logic - no game memory, no file I/O. Runs on every launch via CoreTestRunner. The game-memory side is
//       covered by the windowless self-test (Tests/HeadlessSelfTest.cs).
//
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using SHCDESE.Interop;
using Tomlyn.Model;

namespace CrusaderDETweaker.Tests
{
    internal static class EconomyTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- Economy Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_Yield_Whole();
            Test_Yield_FractionCarriesOver();
            Test_Yield_Half();
            Test_Yield_Clamp();
            Test_Yield_GameAndZeroUntouched();
            Test_Troops_Parse();
            Test_Troops_ParseProblems();
            Test_Troops_Slots();
            Test_Troops_Decision();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  Economy: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        // ===================================================
        // GoodYield
        // ===================================================

        private static int[] Trips(int baseAmount, int milli, int trips)
        {
            int remainder = 0;
            return Enumerable.Range(0, trips).Select(_ => GoodYield.Scale(baseAmount, milli, ref remainder)).ToArray();
        }

        private static void Test_Yield_Whole()
        {
            int[] got = Trips(12, 2000, 3);
            Check("Yield_Whole", got.All(v => v == 24), string.Join(",", got));
        }

        private static void Test_Yield_FractionCarriesOver()
        {
            int[] got = Trips(3, 1250, 4);
            Check("Yield_FractionCarriesOver", got.SequenceEqual(new[] { 3, 4, 4, 4 }), string.Join(",", got));
        }

        private static void Test_Yield_Half()
        {
            int[] got = Trips(3, 500, 4);
            Check("Yield_Half", got.SequenceEqual(new[] { 1, 2, 1, 2 }), string.Join(",", got));
        }

        private static void Test_Yield_Clamp()
        {
            int remainder = 0;
            int got = GoodYield.Scale(30000, 2000, ref remainder);
            Check("Yield_Clamp", got == GoodYield.MaxAmount && remainder == 0, $"{got} rem {remainder}");
        }

        private static void Test_Yield_GameAndZeroUntouched()
        {
            int r1 = 7, r2 = 7;
            int game = GoodYield.Scale(12, GoodYield.GameMilli, ref r1);
            int zero = GoodYield.Scale(0, 2000, ref r2);
            Check("Yield_GameAndZeroUntouched", game == 12 && zero == 0 && r1 == 7 && r2 == 7, $"game {game} zero {zero} rem {r1}/{r2}");
        }

        // ===================================================
        // Skirmish starting troops
        // ===================================================

        private static TomlTable Model(params (string level, string key, object value)[] cells)
        {
            var section = new TomlTable();
            foreach (var (level, key, value) in cells)
            {
                if (level == null) { section[key] = value; continue; }
                if (!section.TryGetValue(level, out var t)) section[level] = t = new TomlTable();
                ((TomlTable)t)[key] = value;
            }
            return new TomlTable { [SkirmishStartingTroops.Section] = section };
        }

        private static int Slot(string key) => SkirmishStartingTroops.Slots.First(s => s.Key == key).Slot;

        private static void Test_Troops_Parse()
        {
            var problems = new List<string>();
            var s = SkirmishStartingTroops.Parse(Model(
                (null, "ApplyToAI", true),
                ("Normal", "Archer", 20L), ("Normal", "Spearman", -1L),
                ("Crusader", "Knight", 0L),
                ("Deathmatch", "HeavyCamel", 3L)), problems);
            bool ok = problems.Count == 0 && s.ApplyToAI && s.Any
                && s.Counts[0][Slot("Archer")] == 20 && s.Counts[0][Slot("Spearman")] == -1
                && s.Counts[1][Slot("Knight")] == 0 && s.Counts[2][Slot("HeavyCamel")] == 3
                && s.Counts[0].Count(c => c >= 0) == 1;
            Check("Troops_Parse", ok, $"problems [{string.Join("; ", problems)}]");

            var none = SkirmishStartingTroops.Parse(Model(("Normal", "Archer", -1L)), problems);
            var missing = SkirmishStartingTroops.Parse(new TomlTable(), problems);
            Check("Troops_Parse_AllGame", !none.Any && !missing.Any && !missing.ApplyToAI, "a file of -1 sets nothing");
        }

        private static void Test_Troops_ParseProblems()
        {
            var problems = new List<string>();
            var s = SkirmishStartingTroops.Parse(Model(
                (null, "ApplyToAI", "yes"),
                ("Normal", "Archers", 5L), ("Normal", "Knight", 2.5), ("Normal", "Swordsman", 5000L)), problems);
            bool ok = problems.Count == 4 && !s.ApplyToAI
                && s.Counts[0][Slot("Knight")] == -1
                && s.Counts[0][Slot("Swordsman")] == SkirmishStartingTroops.MaxCount;
            Check("Troops_ParseProblems", ok, $"{problems.Count} problem(s): {string.Join("; ", problems)}");
        }

        private static void Test_Troops_Slots()
        {
            var slots = SkirmishStartingTroops.Slots;
            bool ok = slots.Select(s => s.Key).Distinct().Count() == slots.Length
                && slots.Select(s => s.Slot).Distinct().Count() == slots.Length
                && slots.All(s => s.Slot >= 0 && s.Slot < SkirmishStartingTroops.SlotCount)
                && !slots.Any(s => s.Slot == 7 || s.Slot == 18 || s.Slot == 19)
                && slots.Length == 25
                && slots.First(s => s.Slot == 0).Unit == eChimps.CHIMP_TYPE_ARCHER
                && slots.First(s => s.Slot == 27).Unit == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER;
            Check("Troops_Slots", ok, $"{slots.Length} slots");
        }

        private static void Test_Troops_Decision()
        {
            bool notRebuilt = SkirmishStartingTroops.ShouldApply(false, eSkirmishGameMode.SKIRMISH_GAME_CUSTOM, 1, out _);
            bool trail = SkirmishStartingTroops.ShouldApply(true, eSkirmishGameMode.SKIRMISH_GAME_TRAIL, 1, out _);
            bool notSkirmish = SkirmishStartingTroops.ShouldApply(true, eSkirmishGameMode.SKIRMISH_GAME_NOT_SKIRMISH, 1, out _);
            bool badLevel = SkirmishStartingTroops.ShouldApply(true, eSkirmishGameMode.SKIRMISH_GAME_CUSTOM, 4, out _);
            bool custom = SkirmishStartingTroops.ShouldApply(true, eSkirmishGameMode.SKIRMISH_GAME_CUSTOM, 2, out string reason);
            Check("Troops_Decision", !notRebuilt && !trail && !notSkirmish && !badLevel && custom && reason == null,
                $"notRebuilt={notRebuilt} trail={trail} notSkirmish={notSkirmish} badLevel={badLevel} custom={custom}");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                _passedCount++;
                Plugin.Logger.LogInfo($"  [PASS] {name}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"  [FAIL] {name}: {detail}");
            }
        }
    }
}
