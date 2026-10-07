// Tests/TeamColorsTest.cs
//
// PURPOSE: Unit tests for the ["Team Colors"] GameplaySettings section (Config/Core/TeamColors.cs).
//
// TESTS COVER:
// - Value parsing: [R, G, B], "#RRGGBB" / "RRGGBB", -1 and "" (game colour), clamping, rejects
// - Migration formatting keeps the user's value as written
// - Palette apply: only entries holding the game's colour for the slot are recoloured (lord palette yes,
//   knight-horse palette no), restore-then-apply N times == once, removing the value restores the game colour
// - Interface table apply keeps alpha
//
// NOTE: pure logic on plain arrays - no game objects, no file I/O. Runs on every launch via CoreTestRunner.
//
using System.Collections.Generic;
using CrusaderDETweaker.Config.Core;
using Tomlyn.Model;
using UnityEngine;

namespace CrusaderDETweaker.Tests
{
    internal static class TeamColorsTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- TeamColors Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_Parse_Array();
            Test_Parse_Hex();
            Test_Parse_GameColour();
            Test_Parse_ClampsOutOfRange();
            Test_Parse_RejectsInvalid();
            Test_Format_KeepsUserText();
            Test_Apply_OnlyGameColourEntries();
            Test_Apply_NTimesEqualsOnce_AndRevert();
            Test_ApplyTable_KeepsAlpha();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  TeamColors: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        // ===================================================
        // Parsing
        // ===================================================

        private static void Test_Parse_Array()
        {
            bool ok = TeamColors.TryParse(new TomlArray { 255L, 0L, 128L }, out var v, out var problem);
            Check("Parse_Array", ok && problem == null && Is(v, 255, 0, 128), $"ok={ok} v={v} problem={problem}");
        }

        private static void Test_Parse_Hex()
        {
            bool ok1 = TeamColors.TryParse("#00FF80", out var v1, out _);
            bool ok2 = TeamColors.TryParse(" 00ff80 ", out var v2, out _);
            Check("Parse_Hex", ok1 && ok2 && Is(v1, 0, 255, 128) && Is(v2, 0, 255, 128), $"v1={v1} v2={v2}");
        }

        private static void Test_Parse_GameColour()
        {
            bool ok1 = TeamColors.TryParse(-1L, out var v1, out var p1);
            bool ok2 = TeamColors.TryParse("", out var v2, out var p2);
            Check("Parse_GameColour", ok1 && ok2 && !v1.HasValue && !v2.HasValue && p1 == null && p2 == null,
                $"v1={v1} v2={v2}");
        }

        private static void Test_Parse_ClampsOutOfRange()
        {
            bool ok = TeamColors.TryParse(new TomlArray { 300L, -5L, 10.4 }, out var v, out var problem);
            Check("Parse_ClampsOutOfRange", ok && Is(v, 255, 0, 10) && problem != null, $"v={v} problem={problem}");
        }

        private static void Test_Parse_RejectsInvalid()
        {
            bool shortArray = TeamColors.TryParse(new TomlArray { 1L, 2L }, out _, out var p1);
            bool badHex = TeamColors.TryParse("zz0000", out _, out var p2);
            bool number = TeamColors.TryParse(5L, out _, out var p3);
            bool boolean = TeamColors.TryParse(true, out _, out var p4);
            Check("Parse_RejectsInvalid", !shortArray && !badHex && !number && !boolean
                && p1 != null && p2 != null && p3 != null && p4 != null,
                $"{shortArray}/{badHex}/{number}/{boolean}");
        }

        private static void Test_Format_KeepsUserText()
        {
            string a = TeamColors.FormatForToml(new TomlArray { 255L, 0L, 0L });
            string s = TeamColors.FormatForToml("#FF0000");
            string n = TeamColors.FormatForToml(-1L);
            string none = TeamColors.FormatForToml(null);
            Check("Format_KeepsUserText", a == "[255, 0, 0]" && s == "\"#FF0000\"" && n == "-1" && none == "-1",
                $"{a} | {s} | {n} | {none}");
        }

        // ===================================================
        // Apply
        // ===================================================

        private static Color[] Vanilla() => new[]
        {
            new Color(0.69f, 0.59f, 0.17f), new Color(0.8f, 0.109f, 0.219f), new Color(0.058f, 0.572f, 0.831f),
        };

        private static void Test_Apply_OnlyGameColourEntries()
        {
            var journal = new BaselineJournal();
            var def = Vanilla();
            var lord = Vanilla();
            lord[0] = lord[2];                                   // lords: slot 0 differs, slot 1 = game red
            var horse = new[] { Color.white, new Color(0.6698f, 0.6698f, 0.6698f), Color.white };
            var overrides = new Dictionary<int, Rgb> { [1] = new Rgb(0, 255, 0) };

            journal.RestoreAll();
            int written = TeamColors.ApplyToPalettes(journal, new List<Color[]> { def, lord, horse, def }, def, overrides);

            Check("Apply_OnlyGameColourEntries",
                written == 2 && Near(def[1], 0, 1, 0) && Near(lord[1], 0, 1, 0)
                && Near(horse[1], 0.6698f, 0.6698f, 0.6698f) && Near(def[2], 0.058f, 0.572f, 0.831f),
                $"written={written} def1={def[1]} lord1={lord[1]} horse1={horse[1]}");
        }

        private static void Test_Apply_NTimesEqualsOnce_AndRevert()
        {
            var journal = new BaselineJournal();
            var def = Vanilla();
            var overrides = new Dictionary<int, Rgb> { [2] = new Rgb(255, 0, 0) };

            for (int i = 0; i < 3; i++)
            {
                journal.RestoreAll();
                TeamColors.ApplyToPalettes(journal, new List<Color[]> { def }, def, overrides);
            }
            bool applied = Near(def[2], 1, 0, 0) && Near(def[1], 0.8f, 0.109f, 0.219f);

            journal.RestoreAll();
            TeamColors.ApplyToPalettes(journal, new List<Color[]> { def }, def, new Dictionary<int, Rgb>());
            bool reverted = Near(def[2], 0.058f, 0.572f, 0.831f);

            Check("Apply_NTimesEqualsOnce_AndRevert", applied && reverted, $"applied={applied} reverted={reverted} def2={def[2]}");
        }

        private static void Test_ApplyTable_KeepsAlpha()
        {
            var journal = new BaselineJournal();
            var table = new[] { Color.white, new Color(0.5f, 0.5f, 0.5f, 0.25f) };
            int written = TeamColors.ApplyToTable(journal, table, new Dictionary<int, Rgb> { [1] = new Rgb(255, 0, 0), [5] = new Rgb(1, 2, 3) },
                (c, v) => new Color(v.R / 255f, v.G / 255f, v.B / 255f, c.a));
            journal.RestoreAll();
            bool restored = Near(table[1], 0.5f, 0.5f, 0.5f);
            TeamColors.ApplyToTable(journal, table, new Dictionary<int, Rgb> { [1] = new Rgb(255, 0, 0) },
                (c, v) => new Color(v.R / 255f, v.G / 255f, v.B / 255f, c.a));

            Check("ApplyTable_KeepsAlpha", written == 1 && restored && Near(table[1], 1, 0, 0) && Mathf.Abs(table[1].a - 0.25f) < 1e-4f,
                $"written={written} restored={restored} t1={table[1]}");
        }

        // ===================================================
        // Helpers
        // ===================================================

        private static bool Is(Rgb? v, int r, int g, int b) => v.HasValue && v.Value.R == r && v.Value.G == g && v.Value.B == b;

        private static bool Near(Color c, float r, float g, float b) =>
            Mathf.Abs(c.r - r) < 1e-3f && Mathf.Abs(c.g - g) < 1e-3f && Mathf.Abs(c.b - b) < 1e-3f;

        private static void Check(string name, bool condition, string detail)
        {
            if (condition)
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
