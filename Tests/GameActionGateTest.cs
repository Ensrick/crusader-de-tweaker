// Tests/GameActionGateTest.cs
//
// PURPOSE: Unit tests for the recruit gate's two interception paths (2.11.12, GitLab #17, plan FWP-U-20):
//          the mod's own GameAction hook and the APIShared GameActionEvents adapter must decide identically.
//
// TESTS COVER:
// - AllowedAmount equals the pre-2.11.12 arithmetic (copied below as the reference) over a grid of inputs
// - The adapter binds to argument types with APIShared's member names, and refuses types that lack one
//   (capability, not version: a too-old or changed APIShared falls back to the own hook)
// - Pre effects: refuse -> veto, trim -> StructureId, allowed -> untouched; nothing for other commands or a
//   request another participant already vetoed
// - Accepted: the reserved amount is the approved one unless another participant changed the request
//
// NOTE: pure logic with test doubles - no game API, no APIShared assembly, no reservation is made (the gate's
// reservations are live state the real recruit buttons use). Runs on every launch via CoreTestRunner.
//
using System;
using CrusaderDETweaker.Config.BepInEx.Systems.Handlers;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Tests
{
    internal static class GameActionGateTest
    {
        private static int _passedCount;
        private static int _failedCount;

        private const global::Enums.GameActionCommand MakeTroop = global::Enums.GameActionCommand.MakeTroop;
        private const global::Enums.GameActionCommand OtherCommand = (global::Enums.GameActionCommand)1;

        // Test doubles with the member names and types of APIShared's GameActionPreEventArgs / AcceptedEventArgs.
        internal sealed class FakePre
        {
            public global::Enums.GameActionCommand Command { get; set; }
            public int StructureId { get; set; }
            public int ActionState { get; set; }
            public bool SkipOriginalFunction { get; set; }
            public object State { get; set; }
        }

        internal sealed class FakePreWithoutVeto
        {
            public global::Enums.GameActionCommand Command { get; set; }
            public int StructureId { get; set; }
            public int ActionState { get; set; }
            public bool SkipOriginalFunction { get; }
            public object State { get; set; }
        }

        internal sealed class FakeAccepted
        {
            public FakeAccepted(int structureId, int actionState, object state) { StructureId = structureId; ActionState = actionState; State = state; }
            public int StructureId { get; }
            public int ActionState { get; }
            public object State { get; }
        }

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- GameActionGate Test Suite ---");
            _passedCount = 0;
            _failedCount = 0;

            Test_AllowedAmount_SameAsBefore();
            Test_Bind_Capability();
            Test_Pre_Effects();
            Test_Pre_PassThrough();
            Test_Accepted_ReserveAmount();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  GameActionGate: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        // The arithmetic of MakeTroopRecruitHook.Detour in 2.11.11, kept here as the reference.
        private static int ReferenceAllowed(int amount, bool capped, int cap, int live, int pending, bool horseGated, int freeHorses)
        {
            bool recruitMax = amount >= 1000;
            int allowed = recruitMax ? int.MaxValue : amount;
            if (capped) allowed = Math.Min(allowed, cap - (live + pending));
            if (horseGated) allowed = Math.Min(allowed, freeHorses);
            if (allowed == int.MaxValue) allowed = amount;
            return allowed;
        }

        private static void Test_AllowedAmount_SameAsBefore()
        {
            int cases = 0, mismatches = 0;
            string first = null;
            foreach (int amount in new[] { 1, 5, 7, 999, 1000 })
            foreach (bool capped in new[] { false, true })
            foreach (int cap in new[] { 0, 3, 20 })
            foreach (int live in new[] { 0, 2, 25 })
            foreach (int pending in new[] { 0, 1 })
            foreach (bool horse in new[] { false, true })
            foreach (int free in new[] { -1, 0, 4 })
            {
                if (!capped && !horse) continue; // ungated requests never reach the arithmetic
                cases++;
                int got = MakeTroopRecruitHook.AllowedAmount(amount, capped, cap, live, pending, horse, free);
                int want = ReferenceAllowed(amount, capped, cap, live, pending, horse, free);
                if (got != want && mismatches++ == 0)
                    first = $"amount={amount} capped={capped} cap={cap} live={live} pending={pending} horse={horse} free={free}: {got} != {want}";
            }
            Check("AllowedAmount_SameAsBefore", mismatches == 0 && cases > 0, $"{mismatches} of {cases} differ; first: {first}");
        }

        private static void Test_Bind_Capability()
        {
            bool ok = APISharedGameActionAdapter.TryBind(typeof(FakePre), typeof(FakeAccepted), out var pre, out var acc, out string missing)
                && pre != null && acc != null && missing.Length == 0;
            bool refused = !APISharedGameActionAdapter.TryBind(typeof(FakePreWithoutVeto), typeof(FakeAccepted), out var pre2, out var acc2, out string missing2)
                && pre2 == null && acc2 == null && missing2.Contains("SkipOriginalFunction set");
            Check("Bind_Capability", ok && refused, $"ok={ok} ({missing}) refusedWithoutVeto={refused} ({missing2})");
        }

        private static MakeTroopRecruitHook.RecruitDecision Decision(int requested, int allowed) => new MakeTroopRecruitHook.RecruitDecision
        {
            Unit = eChimps.CHIMP_TYPE_KNIGHT, Capped = true, HorseGated = false, Requested = requested, Allowed = allowed, Detail = " test"
        };

        private static void Test_Pre_Effects()
        {
            APISharedGameActionAdapter.TryBind(typeof(FakePre), typeof(FakeAccepted), out var access, out _, out _);

            var blocked = new FakePre { Command = MakeTroop, StructureId = 5, ActionState = (int)eChimps.CHIMP_TYPE_KNIGHT };
            APISharedGameActionAdapter.ApplyDecision(access, blocked, 5, Decision(5, 0));
            bool blockOk = blocked.SkipOriginalFunction && blocked.StructureId == 5 && blocked.State == null;

            var trimmed = new FakePre { Command = MakeTroop, StructureId = 1000, ActionState = (int)eChimps.CHIMP_TYPE_KNIGHT };
            APISharedGameActionAdapter.ApplyDecision(access, trimmed, 1000, Decision(1000, 3));
            bool trimOk = !trimmed.SkipOriginalFunction && trimmed.StructureId == 3
                && trimmed.State is APISharedGameActionAdapter.Approval t && t.ForwardedStructureId == 3 && t.Decision.Allowed == 3;

            var allowed = new FakePre { Command = MakeTroop, StructureId = 5, ActionState = (int)eChimps.CHIMP_TYPE_KNIGHT };
            APISharedGameActionAdapter.ApplyDecision(access, allowed, 5, Decision(5, 5));
            bool allowOk = !allowed.SkipOriginalFunction && allowed.StructureId == 5
                && allowed.State is APISharedGameActionAdapter.Approval a && a.ForwardedStructureId == 5;

            // The own hook reads a payload of 0 or less as 1 and then forwards the payload as it came.
            var zero = new FakePre { Command = MakeTroop, StructureId = 0, ActionState = (int)eChimps.CHIMP_TYPE_KNIGHT };
            APISharedGameActionAdapter.ApplyDecision(access, zero, 0, Decision(1, 1));
            bool zeroOk = zero.StructureId == 0 && zero.State is APISharedGameActionAdapter.Approval z && z.ForwardedStructureId == 0;

            Check("Pre_Effects", blockOk && trimOk && allowOk && zeroOk, $"block={blockOk} trim={trimOk} allow={allowOk} zero={zeroOk}");
        }

        private static void Test_Pre_PassThrough()
        {
            APISharedGameActionAdapter.TryBind(typeof(FakePre), typeof(FakeAccepted), out var access, out _, out _);
            var other = new FakePre { Command = OtherCommand, StructureId = 7, ActionState = 3 };
            APISharedGameActionAdapter.Pre(access, other);
            var vetoed = new FakePre { Command = MakeTroop, StructureId = 5, ActionState = (int)eChimps.CHIMP_TYPE_KNIGHT, SkipOriginalFunction = true };
            APISharedGameActionAdapter.Pre(access, vetoed);
            bool ok = other.StructureId == 7 && !other.SkipOriginalFunction && other.State == null
                && vetoed.StructureId == 5 && vetoed.SkipOriginalFunction && vetoed.State == null;
            Check("Pre_PassThrough", ok, $"other={other.StructureId}/{other.SkipOriginalFunction} vetoed={vetoed.StructureId}/{vetoed.State}");
        }

        private static void Test_Accepted_ReserveAmount()
        {
            var approval = new APISharedGameActionAdapter.Approval { Decision = Decision(1000, 3), ForwardedStructureId = 3 };
            bool asLeft = APISharedGameActionAdapter.ReserveAmount(approval, 3) == 3;
            bool trimmedLater = APISharedGameActionAdapter.ReserveAmount(approval, 2) == 2;
            bool zeroIsOne = APISharedGameActionAdapter.ReserveAmount(approval, 0) == 1;
            var zeroForwarded = new APISharedGameActionAdapter.Approval { Decision = Decision(1, 1), ForwardedStructureId = 0 };
            bool zeroAsLeft = APISharedGameActionAdapter.ReserveAmount(zeroForwarded, 0) == 1;

            // No approval in State (another registration's call, or a vetoed one): nothing happens, no exception.
            APISharedGameActionAdapter.TryBind(typeof(FakePre), typeof(FakeAccepted), out _, out var acc, out _);
            bool noState = true;
            try { APISharedGameActionAdapter.Accepted(acc, new FakeAccepted(3, (int)eChimps.CHIMP_TYPE_KNIGHT, null)); }
            catch { noState = false; }
            Check("Accepted_ReserveAmount", asLeft && trimmedLater && zeroIsOne && zeroAsLeft && noState,
                $"asLeft={asLeft} trimmedLater={trimmedLater} zeroIsOne={zeroIsOne} zeroAsLeft={zeroAsLeft} noState={noState}");
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
