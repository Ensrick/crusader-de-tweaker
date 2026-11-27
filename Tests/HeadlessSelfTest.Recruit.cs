// Tests/HeadlessSelfTest.Recruit.cs
//
// PURPOSE: Self-test stage for 2.11.12 (GitLab #17, plan FWP-U-20 / P6-01): the recruit gate (MaxCount at recruit time)
//          on its two interception paths. Runs alone with "-cdt-selftest-only recruit" (test_headless.ps1 -Only recruit),
//          which launches the game twice: CFG [Compatibility] GameActionHook = Auto, then = Own. The runner writes
//          [CHIMP_TYPE_KNIGHT] MaxCount = RecruitKnightCap into the Units file and compares the OUTCOME lines of both runs.
//
// WHAT IT DOES (editor map, local player):
//   1. Path: Auto must have registered the gate with APIShared's GameActionEvents (MakeTroopRecruitHook.ActivePath, and a
//      second registration with the same owner / ID refused as a duplicate) with the mod's own MonoMod hook NOT installed;
//      Own must have the own hook installed and no APIShared registration under that owner / ID (the probe is accepted).
//      In Auto a test observer registers on GameActionEvents at order 2000 (after the gate's 1000) and records what it
//      sees: the veto, and the final amount of an accepted request.
//   2. RecruitSpawned Knights spawned (all stay under the cap), then EngineInterface.GameAction(MakeTroop, 1, Knight):
//      refused (returns 0, nothing reserved; Auto: the observer's Post reports the veto).
//   3. RecruitSpawned - RecruitKept Knights removed (DeleteUnitSafe), then a Ctrl request (1000): trimmed to the room
//      left, RecruitKnightCap - RecruitKept, which must be exactly what the gate reserves (Auto: also the observer's final
//      amount). Then a request of 1: refused, because the reserved recruits count.
//   4. Cleanup (Knights removed, lobby values back), the OUTCOME line, the registration probe.
//   The editor map has no Barracks, so a request the gate lets through reaches the game and recruits nothing; only the
//   gate's decision is measured.
//
// IMPORTANT FOR AI AGENTS: keep RecruitKnightCap in sync with scripts/test_headless.ps1. The observer and the probe stay
//   registered for the rest of the (test) process: APIShared registrations have process lifetime.
//
using System;
using System.Collections.Generic;
using System.Reflection;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.BepInEx.Systems.Handlers;
using CrusaderDETweaker.Config.Sync;
using SHCDESE.API;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        // Written into the Units file by scripts/test_headless.ps1 (-Only recruit).
        internal const int RecruitKnightCap = 5;
        private const int RecruitStage = 300, RecruitSpawned = 5, RecruitKept = 2, RecruitX = 420, RecruitY = 420;
        private const string ObserverOwner = "CrusaderDETweaker.SelfTest", ObserverId = "RecruitObserver";

        private readonly List<int> _rKnights = new List<int>();
        private readonly List<string> _rOutcome = new List<string>();
        private int _rStep, _rStepTick, _rLocal;
        private string _rLobbyBefore, _rMode;
        private bool _rAuto;
        private Assembly _rApi;
        private bool _rObserverOn, _rObservedSkipped;
        private int _rObservedAccepted = -1, _rObservedPosts;

        private struct RecruitResult
        {
            internal int Result, PendingDelta, ObservedAccepted, ObservedPosts;
            internal bool ObservedSkipped;
            public override string ToString() => $"returned {Result}, reserved +{PendingDelta}";
        }

        private void StartRecruit(float now)
        {
            _rMode = BepInExConfigManager.GameActionHook?.Value ?? "?";
            _rAuto = !string.Equals(_rMode, "Own", StringComparison.OrdinalIgnoreCase);
            string path = MakeTroopRecruitHook.ActivePath;
            bool ownHook = OwnHookInstalled();
            _rApi = APISharedAssembly(out string apiVersion);
            Note($"Recruit gate: CFG [Compatibility] GameActionHook = {_rMode}; path in use: {path}; own MonoMod hook on EngineInterface.GameAction installed: {ownHook}; " +
                 $"APIShared: {(_rApi == null ? "not loaded" : apiVersion)}");

            if (_rAuto)
            {
                Check("Auto: APIShared is loaded", _rApi != null, _rApi == null ? "APIShared_Serp is not among the loaded plugins" : $"APIShared {apiVersion}");
                Check("Auto: the recruit gate is registered through APIShared's GameActionEvents", path == "APIShared", $"path in use: {path}");
                Check("Auto: the mod's own MonoMod hook is NOT installed", !ownHook, $"own hook installed: {ownHook}");
                if (_rApi != null)
                {
                    _rObserverOn = TryRegisterOnGameActionEvents(_rApi, ObserverOwner, ObserverId, 2000, null, ObserverPost, ObserverAccepted, out string reason);
                    Check("Auto: test observer registered on GameActionEvents after the gate (order 2000)", _rObserverOn, _rObserverOn ? "registered" : reason);
                }
            }
            else
            {
                Check("Own: the mod's own MonoMod hook is installed and carries the gate", ownHook && path == "own hook", $"path in use: {path}, own hook installed: {ownHook}");
            }

            var vm = ConfigSyncManager.Lobby;
            _rLobbyBefore = vm?.MaxCounts;
            if (vm != null) vm.MaxCounts = string.Empty;   // the Units file's Knight MaxCount, no lobby value
            Check($"Units file MaxCount {RecruitKnightCap} for the Knight is in effect", LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT) == RecruitKnightCap,
                $"Knight cap {Cap(LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT))}; lobby values before the test '{_rLobbyBefore}'");

            _rLocal = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
            Check("The local player id is valid (the gate refuses nothing without it)", _rLocal > 0, $"local player {_rLocal}");
            int owner = _rLocal > 0 ? _rLocal : 1;
            for (int i = 0; i < RecruitSpawned; i++) _rKnights.Add(Spawn(eChimps.CHIMP_TYPE_KNIGHT, RecruitX + 3 * i, RecruitY, owner));

            _rStep = 0;
            _rStepTick = global::Director.instance.getSimTickCount();
            _stage = RecruitStage; _deadline = now + 300;
            _watchTick = _rStepTick; _watchTime = now; _lastProgressNote = now;
        }

        /// <summary>One frame of the recruit stage (steps CapSettleTicks apart); true when finished.</summary>
        private bool RecruitTick()
        {
            if (global::Director.instance.getSimTickCount() - _rStepTick < CapSettleTicks) return false;
            var units = GameUnitManagerAPI.Instance;
            int owner = _rLocal > 0 ? _rLocal : 1;
            switch (_rStep)
            {
                case 0:
                {
                    int live = UnitCapHandler.CountPlayerUnitsOfType(owner, eChimps.CHIMP_TYPE_KNIGHT);
                    Check($"{RecruitSpawned} Knights spawned under MaxCount {RecruitKnightCap} all stay", live == RecruitSpawned, $"{live} counted by the gate");
                    var r = Request(1);
                    Check($"Recruiting a Knight with MaxCount {RecruitKnightCap} reached is refused", r.Result == 0 && r.PendingDelta == 0
                        && (!_rObserverOn || (r.ObservedSkipped && r.ObservedPosts == 1 && r.ObservedAccepted < 0)),
                        $"{live} alive, request 1: {r}" + (_rObserverOn ? $"; observer: Post {r.ObservedPosts}, vetoed {r.ObservedSkipped}, accepted amount {r.ObservedAccepted}" : ""));
                    _rOutcome.Add($"cap reached, request 1: {r}");
                    for (int i = 0; i < RecruitSpawned - RecruitKept; i++) units.DeleteUnitSafe(_rKnights[i]);
                    break;
                }
                case 1:
                {
                    int live = UnitCapHandler.CountPlayerUnitsOfType(owner, eChimps.CHIMP_TYPE_KNIGHT);
                    int pending = PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT);
                    int room = RecruitKnightCap - live - pending;
                    Check($"{RecruitSpawned - RecruitKept} Knights removed, {RecruitKept} left", live == RecruitKept, $"{live} counted by the gate, {pending} reserved");
                    var r = Request(1000);
                    Check($"A Ctrl recruit (1000) is trimmed to the room left ({room}), and exactly that is reserved", room > 0 && r.PendingDelta == room
                        && (!_rObserverOn || (!r.ObservedSkipped && r.ObservedAccepted == room)),
                        $"{live} alive, cap {RecruitKnightCap}, request 1000: {r}" + (_rObserverOn ? $"; observer: final amount {r.ObservedAccepted}, vetoed {r.ObservedSkipped}" : ""));
                    _rOutcome.Add($"room {room}, request 1000: reserved +{r.PendingDelta}");
                    var again = Request(1);
                    Check("With the room reserved, the next Knight recruit is refused", again.Result == 0 && again.PendingDelta == 0
                        && (!_rObserverOn || again.ObservedSkipped),
                        $"request 1: {again}, reserved now {PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT)}");
                    _rOutcome.Add($"room reserved, request 1: {again}");
                    break;
                }
                case 2:
                {
                    foreach (int id in _rKnights) KillIfAlive(id);
                    var vm = ConfigSyncManager.Lobby;
                    if (vm != null && _rLobbyBefore != null) vm.MaxCounts = _rLobbyBefore;
                    Note("OUTCOME " + string.Join("; ", _rOutcome));

                    // Last, so the probe cannot change the hook chain the requests above went through.
                    if (_rApi == null)
                        Note("SKIP APIShared registration probe: APIShared is not loaded");
                    else
                    {
                        bool accepted = TryRegisterOnGameActionEvents(_rApi, PluginInfo.PLUGIN_GUID, APISharedGameActionAdapter.RegistrationId, 0,
                            _ => { }, null, null, out string reason);
                        if (_rAuto)
                            Check($"Auto: APIShared holds the registration {PluginInfo.PLUGIN_GUID} / {APISharedGameActionAdapter.RegistrationId} (a second one is refused as a duplicate)",
                                !accepted && reason != null && reason.IndexOf("already registered", StringComparison.OrdinalIgnoreCase) >= 0, accepted ? "the probe was accepted" : reason);
                        else
                            Check($"Own: APIShared holds no registration {PluginInfo.PLUGIN_GUID} / {APISharedGameActionAdapter.RegistrationId} (a probe registration is accepted)",
                                accepted, accepted ? "accepted (a no-op, process lifetime)" : reason);
                    }
                    return true;
                }
            }
            _rStep++;
            _rStepTick = global::Director.instance.getSimTickCount();
            return false;
        }

        private RecruitResult Request(int amount)
        {
            int before = PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT);
            _rObservedAccepted = -1; _rObservedSkipped = false;
            int postsBefore = _rObservedPosts;
            int result = global::EngineInterface.GameAction(global::Enums.GameActionCommand.MakeTroop, amount, (int)eChimps.CHIMP_TYPE_KNIGHT, 0);
            return new RecruitResult
            {
                Result = result,
                PendingDelta = PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT) - before,
                ObservedAccepted = _rObservedAccepted,
                ObservedSkipped = _rObservedSkipped,
                ObservedPosts = _rObservedPosts - postsBefore,
            };
        }

        // Observer callbacks (APIShared GameActionPostEventArgs / GameActionAcceptedEventArgs, read by reflection).
        private void ObserverPost(object args)
        {
            if (!IsKnightRecruit(args)) return;
            _rObservedPosts++;
            _rObservedSkipped = (bool)Prop(args, "WasSkipped");
        }

        private void ObserverAccepted(object args)
        {
            if (!IsKnightRecruit(args)) return;
            _rObservedAccepted = (int)Prop(args, "StructureId");
        }

        private static bool IsKnightRecruit(object args) =>
            (global::Enums.GameActionCommand)Prop(args, "Command") == global::Enums.GameActionCommand.MakeTroop
            && (int)Prop(args, "ActionState") == (int)eChimps.CHIMP_TYPE_KNIGHT;

        private static object Prop(object o, string name) => o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(o, null);

        private static bool OwnHookInstalled() =>
            typeof(MakeTroopRecruitHook).GetField("_hook", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) != null;

        private static Assembly APISharedAssembly(out string version)
        {
            version = "?";
            if (global::BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(APISharedGameActionAdapter.APISharedGuid, out var info) && info?.Instance != null)
            {
                version = info.Metadata?.Version?.ToString() ?? "?";
                return info.Instance.GetType().Assembly;
            }
            return null;
        }

        /// <summary>GameActionEvents.TryRegister through reflection (the test, like the plugin, has no build reference to APIShared).</summary>
        private static bool TryRegisterOnGameActionEvents(Assembly api, string owner, string id, int order,
            Action<object> pre, Action<object> post, Action<object> accepted, out string reason)
        {
            try
            {
                Type tEvents = api.GetType("APIShared.Commands.GameActionEvents", true);
                Type tPre = api.GetType("APIShared.Commands.GameActionPreEventArgs", true);
                Type tPost = api.GetType("APIShared.Commands.GameActionPostEventArgs", true);
                Type tAccepted = api.GetType("APIShared.Commands.GameActionAcceptedEventArgs", true);
                MethodInfo m = tEvents.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static, null, new[]
                {
                    typeof(string), typeof(string), typeof(Action<>).MakeGenericType(tPre), typeof(Action<>).MakeGenericType(tPost),
                    typeof(string).MakeByRefType(), typeof(int), typeof(Action<>).MakeGenericType(tAccepted)
                }, null);
                if (m == null) { reason = "GameActionEvents.TryRegister (7 parameters) not found"; return false; }
                var args = new object[] { owner, id, ObjectAction(tPre, pre), ObjectAction(tPost, post), null, order, ObjectAction(tAccepted, accepted) };
                bool ok = (bool)m.Invoke(null, args);
                reason = args[4] as string ?? string.Empty;
                return ok;
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                reason = $"{inner.GetType().Name}: {inner.Message}";
                return false;
            }
        }

        private static object ObjectAction(Type argType, Action<object> target) =>
            target == null ? null : typeof(HeadlessSelfTest).GetMethod(nameof(TypedAction), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(argType).Invoke(null, new object[] { target });

        private static Action<T> TypedAction<T>(Action<object> target) => x => target(x);
    }
}
