// Tests/LobbyMaxCountsTest.cs
//
// PURPOSE: Unit tests for the lobby MaxCount values (Config/Sync/LobbyMaxCounts.cs, GitLab #5).
//
// TESTS COVER:
// - One box: "" = not set, -1 / 0 / N accepted, out of range and text rejected
// - The synced string: parse (unknown keys and bad values skipped), encode sorted, round trip
// - Overlay on the file caps: lobby N replaces, lobby -1 removes the cap, not set keeps the file's
// - The type lists shown in the tab (recruitable units, no siege engines since 2.11.4; buildings without destroyed towers)
// - SharedCap (2.11.4): the cap every PC of a multiplayer match uses for spawned units
// - Siege placement (2.11.4): mapper -> engine / tent, refusal at engines + tents >= cap
//
// NOTE: pure logic - no game objects, no file I/O. Runs on every launch via CoreTestRunner. The in-game
// behaviour (caps enforced on real units and placements) is covered by Tests/HeadlessSelfTest.cs.
//
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Sync;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Tests
{
    internal static class LobbyMaxCountsTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- LobbyMaxCounts Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_ParseValue();
            Test_ParseString();
            Test_EncodeSortedRoundTrip();
            Test_Overlay();
            Test_TypeLists();
            Test_Label();
            Test_SiegeEngineValueIgnored();
            Test_SharedCap();
            Test_SiegePlacement();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  LobbyMaxCounts: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        private static void Test_ParseValue()
        {
            bool empty = LobbyMaxCounts.TryParseValue("  ", out int? e) && !e.HasValue;
            bool minus = LobbyMaxCounts.TryParseValue("-1", out int? m) && m == -1;
            bool zero = LobbyMaxCounts.TryParseValue("0", out int? z) && z == 0;
            bool n = LobbyMaxCounts.TryParseValue(" 12 ", out int? v) && v == 12;
            bool rejects = !LobbyMaxCounts.TryParseValue("-2", out _) && !LobbyMaxCounts.TryParseValue("abc", out _)
                && !LobbyMaxCounts.TryParseValue("1.5", out _) && !LobbyMaxCounts.TryParseValue((LobbyMaxCounts.MaxValue + 1).ToString(), out _);
            Check("ParseValue", empty && minus && zero && n && rejects, $"empty={empty} -1={minus} 0={zero} 12={n} rejects={rejects}");
        }

        private static void Test_ParseString()
        {
            var problems = new List<string>();
            var v = LobbyMaxCounts.Parse("CHIMP_TYPE_KNIGHT=10; STRUCT_STABLES=-1;CHIMP_TYPE_PEASANT=3;CHIMP_TYPE_ARCHER=x;;STRUCT_WELL=0", problems);
            Check("ParseString", v.Count == 3 && v["CHIMP_TYPE_KNIGHT"] == 10 && v["STRUCT_STABLES"] == -1 && v["STRUCT_WELL"] == 0 && problems.Count == 2,
                $"values={LobbyMaxCounts.Encode(v)} problems={string.Join(" | ", problems)}");
        }

        private static void Test_EncodeSortedRoundTrip()
        {
            var values = new Dictionary<string, int> { ["STRUCT_WELL"] = 2, ["CHIMP_TYPE_KNIGHT"] = 0, ["CHIMP_TYPE_ARCHER"] = -1 };
            string text = LobbyMaxCounts.Encode(values);
            var back = LobbyMaxCounts.Parse(text, null);
            Check("EncodeSortedRoundTrip", text == "CHIMP_TYPE_ARCHER=-1;CHIMP_TYPE_KNIGHT=0;STRUCT_WELL=2" && back.Count == 3 && LobbyMaxCounts.Encode(back) == text
                && LobbyMaxCounts.Encode(new Dictionary<string, int>()) == string.Empty, $"text={text}");
        }

        private static void Test_Overlay()
        {
            var file = new Dictionary<eChimps, int> { [eChimps.CHIMP_TYPE_KNIGHT] = 5, [eChimps.CHIMP_TYPE_ARCHER] = 20, [eChimps.CHIMP_TYPE_MACEMAN] = 1 };
            var lobby = new Dictionary<string, int> { ["CHIMP_TYPE_KNIGHT"] = 2, ["CHIMP_TYPE_MACEMAN"] = -1, ["CHIMP_TYPE_SPEARMAN"] = 0, ["STRUCT_WELL"] = 3 };
            var effective = LobbyMaxCounts.Overlay(file, lobby, k => Enum.TryParse(k, out eChimps u) && k.StartsWith("CHIMP_") ? u : (eChimps?)null);
            bool ok = effective.Count == 3 && effective[eChimps.CHIMP_TYPE_KNIGHT] == 2 && effective[eChimps.CHIMP_TYPE_ARCHER] == 20
                && !effective.ContainsKey(eChimps.CHIMP_TYPE_MACEMAN) && effective[eChimps.CHIMP_TYPE_SPEARMAN] == 0 && file[eChimps.CHIMP_TYPE_KNIGHT] == 5;
            Check("Overlay", ok, string.Join(", ", effective.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        private static void Test_TypeLists()
        {
            var units = LobbyMaxCounts.UnitTypes;
            var buildings = LobbyMaxCounts.BuildingTypes;
            bool ok = units.Contains(eChimps.CHIMP_TYPE_KNIGHT) && !units.Contains(eChimps.CHIMP_TYPE_TREBUCHET) && !units.Contains(eChimps.CHIMP_TYPE_CATAPULT)
                && !units.Contains(eChimps.CHIMP_TYPE_PEASANT) && buildings.Contains(eStructs.STRUCT_SIEGE_TENT_CATAPULT)
                && buildings.Contains(eStructs.STRUCT_STABLES) && buildings.Contains(eStructs.STRUCT_HEALER)
                && !buildings.Any(b => b.ToString().EndsWith("_DESTROYED")) && units.Distinct().Count() == units.Length;
            Check("TypeLists", ok, $"{units.Length} unit types, {buildings.Length} building types");
        }

        private static void Test_Label()
        {
            string a = LobbyMaxCounts.Label("CHIMP_TYPE_ARAB_BOW"), b = LobbyMaxCounts.Label("STRUCT_SIEGE_TENT_CATAPULT");
            Check("Label", a == "Arab Bow" && b == "Siege Tent Catapult", $"{a} / {b}");
        }

        private static void Test_SiegeEngineValueIgnored()
        {
            var problems = new List<string>();
            var v = LobbyMaxCounts.Parse("CHIMP_TYPE_CATAPULT=2;CHIMP_TYPE_KNIGHT=4", problems);
            Check("SiegeEngineValueIgnored", v.Count == 1 && v["CHIMP_TYPE_KNIGHT"] == 4 && problems.Count == 1 && problems[0].Contains("siege engines"),
                $"values={LobbyMaxCounts.Encode(v)} problems={string.Join(" | ", problems)}");
        }

        private static void Test_SharedCap()
        {
            var lobby = new Dictionary<string, int> { ["CHIMP_TYPE_KNIGHT"] = 3, ["CHIMP_TYPE_ARCHER"] = -1 };
            var file = new Dictionary<eChimps, int> { [eChimps.CHIMP_TYPE_KNIGHT] = 9, [eChimps.CHIMP_TYPE_ARCHER] = 5, [eChimps.CHIMP_TYPE_CATAPULT] = 2 };
            bool lobbyWins = LobbyMaxCounts.SharedCap(lobby, file, eChimps.CHIMP_TYPE_KNIGHT, false, out int k) && k == 3;
            bool lobbyUnlimited = !LobbyMaxCounts.SharedCap(lobby, file, eChimps.CHIMP_TYPE_ARCHER, true, out _);
            bool ownFileNotShared = !LobbyMaxCounts.SharedCap(lobby, file, eChimps.CHIMP_TYPE_CATAPULT, false, out _);
            bool hostFileShared = LobbyMaxCounts.SharedCap(lobby, file, eChimps.CHIMP_TYPE_CATAPULT, true, out int c) && c == 2;
            bool none = !LobbyMaxCounts.SharedCap(lobby, file, eChimps.CHIMP_TYPE_MACEMAN, true, out _);
            Check("SharedCap", lobbyWins && lobbyUnlimited && ownFileNotShared && hostFileShared && none,
                $"lobby={lobbyWins} lobby-1={lobbyUnlimited} ownFile={ownFileNotShared} hostFile={hostFileShared} none={none}");
        }

        private static void Test_SiegePlacement()
        {
            bool tent = Config.BepInEx.Systems.Handlers.BuildingCapHandler.TryGetSiegeEngine(eMappers.MAPPER_CATAPULT, out eChimps e1, out eStructs t1)
                && e1 == eChimps.CHIMP_TYPE_CATAPULT && t1 == eStructs.STRUCT_SIEGE_TENT_CATAPULT;
            bool tower = Config.BepInEx.Systems.Handlers.BuildingCapHandler.TryGetSiegeEngine(eMappers.MAPPER_MANGONEL, out eChimps e2, out eStructs t2)
                && e2 == eChimps.CHIMP_TYPE_MANGONEL && t2 == eStructs.STRUCT_NULL;
            bool other = !Config.BepInEx.Systems.Handlers.BuildingCapHandler.TryGetSiegeEngine(eMappers.MAPPER_TOWER1, out _, out _);
            bool rule = !Config.BepInEx.Systems.Handlers.BuildingCapHandler.SiegePlacementBlocked(1, 0, 2)
                && Config.BepInEx.Systems.Handlers.BuildingCapHandler.SiegePlacementBlocked(1, 1, 2)
                && Config.BepInEx.Systems.Handlers.BuildingCapHandler.SiegePlacementBlocked(0, 0, 0);
            Check("SiegePlacement", tent && tower && other && rule, $"tent={tent} tower={tower} other={other} rule={rule}");
        }

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
