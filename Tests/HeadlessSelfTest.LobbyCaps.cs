// Tests/HeadlessSelfTest.LobbyCaps.cs
//
// PURPOSE: Windowless self-test stage for GitLab #5 (part of HeadlessSelfTest, after the Bedouin heal stage):
//   lobby MaxCount values typed into the Crusader DE Tweaker lobby tab (Config/Sync/LobbyMaxCounts.cs).
//   - The tab's XAML loaded with both MaxCount lists bound to the ViewModel (item counts match).
//   - The boxes are driven the way the TextBox binding drives them (row.Text = "..."); the cap tables the
//     handlers read must follow, and the caps must act on real units / placements:
//     Knight "2": three Knights spawned one after the other for the local player (the spawn path,
//       UnitCapHandler): the third must be removed. A new unit is still NeedsInit during its own creation
//       event; before the fix the cap counted IsAlive units only, so the third Knight stayed (MaxCount + 1,
//       measured in selftest-20261008-105719). Each spawn logs the old rule's count next to the new one.
//       Then a recruit request with the cap reached must be refused by the recruit gate (MakeTroopRecruitHook)
//       without reaching the game;
//     Pikeman "1": three Pikemen spawned in the SAME tick: one must stay (before the fix none counted the
//       others, all initialising);
//     Maceman "-1" while the Units file says MaxCount = 1 (runner): two Macemen stay alive (lobby wins);
//     Maceman "" again: the file's 1 applies to the next one;
//     "abc" / "-5" are rejected and the box shows the value in effect;
//     Well "1": a second Well placed through the game's build routine is blocked if that routine raises
//       the placement validation (SKIP otherwise, with the reason).
//   - Everything is put back to the lobby values the player had (SE persists them in the plugin folder).
//   The UI itself cannot be seen in a windowless run, and host -> client delivery needs two machines.
//
// IMPORTANT FOR AI AGENTS: keep FileMacemanCap in sync with scripts/test_headless.ps1.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrusaderDETweaker.Config.Sync;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        // Written into the Units file by scripts/test_headless.ps1 ([CHIMP_TYPE_MACEMAN] MaxCount).
        internal const int FileMacemanCap = 1;
        private const int CapX = 430, CapY = 390, WellX = 440, WellY = 370, WellX2 = 446, CapSettleTicks = 5;

        private readonly List<int> _capUnits = new List<int>();
        private readonly List<string> _capSpawnLog = new List<string>();
        private string _capOwnValues;
        private int _capStep, _capStepTick, _capLocal, _capWellsBefore;

        private LobbyMaxCountRow Row(eChimps unit) => ConfigSyncManager.Lobby.UnitMaxCounts.First(r => r.Key == unit.ToString());

        private LobbyMaxCountRow Row(eStructs building) => ConfigSyncManager.Lobby.BuildingMaxCounts.First(r => r.Key == building.ToString());

        private int CountAlive(int owner, eChimps type)
        {
            var ids = new List<int>();
            GameUnitManagerAPI.Instance.GetAllUnits(ids, AliveState.IsAlive, type, PlayerRelationship.Self, owner);
            return ids.Count;
        }

        private static string Cap(int? cap) => cap.HasValue ? cap.Value.ToString() : "none (unlimited)";

        private static unsafe string State(int unitId) =>
            unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u) ? u->r_AliveState.ToString() : "?";

        /// <summary>
        /// Spawns one unit for the local player and notes its state right after creation and both counts: the old
        /// rule (IsAlive units only, what UnitCapHandler counted before the fix) and the fixed one (alive or still
        /// initialising), both read after the cap handler ran.
        /// </summary>
        private void CapSpawn(eChimps type, int x, int y)
        {
            int id = Spawn(type, x, y, owner: _capLocal);
            _capUnits.Add(id);
            _capSpawnLog.Add($"{type} {id}: {State(id)} after its creation event, old rule counts {CountAlive(_capLocal, type)}, " +
                             $"fixed rule {Config.BepInEx.Systems.Handlers.UnitCapHandler.CountPlayerUnitsOfType(_capLocal, type)}");
        }

        private string CapSpawned(eChimps type) =>
            string.Join(", ", _capUnits.Where(id => GameUnitManagerAPI.Instance.GetType(id) == type).Select(id => $"{id} {State(id)}"));

        private void NextCapStep()
        {
            _capStep++;
            _capStepTick = global::Director.instance.getSimTickCount();
        }

        /// <summary>Checks that need no game tick; returns false when the stage cannot run.</summary>
        private bool StartLobbyCapsTest()
        {
            var vm = ConfigSyncManager.Lobby;
            var entry = vm == null ? null : GameXAMLManagerAPI.Instance.RegisteredModSettings.FirstOrDefault(e => ReferenceEquals(e.ViewModel, vm));
            if (entry == null)
            {
                Check("Lobby tab registered", false, "the Crusader DE Tweaker tab is not registered with the Script Extender");
                return false;
            }
            var unitList = entry.View.FindName("CDT_UnitMaxCounts") as Noesis.ItemsControl;
            var buildingList = entry.View.FindName("CDT_BuildingMaxCounts") as Noesis.ItemsControl;
            Check("Lobby tab shows the MaxCount lists (XAML loaded, bound to the ViewModel)",
                unitList != null && buildingList != null && unitList.Items.Count == vm.UnitMaxCounts.Count && buildingList.Items.Count == vm.BuildingMaxCounts.Count
                && vm.UnitMaxCounts.Count == LobbyMaxCounts.UnitTypes.Length,
                $"unit list {(unitList == null ? "missing" : unitList.Items.Count + " items")} (ViewModel {vm.UnitMaxCounts.Count}), " +
                $"building list {(buildingList == null ? "missing" : buildingList.Items.Count + " items")} (ViewModel {vm.BuildingMaxCounts.Count})");

            _capOwnValues = vm.MaxCounts;
            _capLocal = Plugin.PlayerApi?.GetLocalPlayerId() ?? 1;
            Note($"Lobby MaxCount values before the test: '{_capOwnValues}'; local player {_capLocal} (AI: {Plugin.PlayerApi?.IsAIPlayer(_capLocal)}); " +
                 $"networked: {GameNetworkAPI.IsNetworkedEnvironment()}");
            vm.MaxCounts = string.Empty;
            Check($"Units file MaxCount = {FileMacemanCap} for the Maceman is in effect", LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_MACEMAN) == FileMacemanCap,
                $"Maceman cap {Cap(LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_MACEMAN))}");

            Row(eChimps.CHIMP_TYPE_KNIGHT).Text = "2";
            Check("Typing 2 in the Knight box sets the lobby value and the cap", vm.MaxCounts == "CHIMP_TYPE_KNIGHT=2" && LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT) == 2
                && Row(eChimps.CHIMP_TYPE_KNIGHT).Text == "2", $"MaxCounts '{vm.MaxCounts}', Knight cap {Cap(LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT))}");

            Row(eChimps.CHIMP_TYPE_KNIGHT).Text = "abc";
            Row(eChimps.CHIMP_TYPE_KNIGHT).Text = "-5";
            Check("Invalid box input (abc, -5) is rejected", vm.MaxCounts == "CHIMP_TYPE_KNIGHT=2" && Row(eChimps.CHIMP_TYPE_KNIGHT).Text == "2"
                && LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT) == 2, $"MaxCounts '{vm.MaxCounts}', Knight box '{Row(eChimps.CHIMP_TYPE_KNIGHT).Text}'");

            _capStep = -1;
            NextCapStep();
            CapSpawn(eChimps.CHIMP_TYPE_KNIGHT, CapX, CapY);
            return true;
        }

        /// <summary>One frame of the lobby MaxCount stage (steps CapSettleTicks apart); true when finished.</summary>
        private unsafe bool LobbyCapsTick()
        {
            if (global::Director.instance.getSimTickCount() - _capStepTick < CapSettleTicks) return false;
            var vm = ConfigSyncManager.Lobby;
            switch (_capStep)
            {
                case 0:
                case 1:
                    CapSpawn(eChimps.CHIMP_TYPE_KNIGHT, CapX + 3 * (_capStep + 1), CapY);
                    break;
                case 2:
                {
                    int knights = CountAlive(_capLocal, eChimps.CHIMP_TYPE_KNIGHT);
                    // Spawn path (UnitCapHandler, OnUnitCreate). Before the fix: 3 of 3 stayed (MaxCount + 1).
                    Check("Lobby Knight cap 2, spawn path: the third Knight spawned is removed (was MaxCount + 1)", knights == 2,
                        $"3 Knights spawned one after the other, {knights} alive ({CapSpawned(eChimps.CHIMP_TYPE_KNIGHT)}); {string.Join("; ", _capSpawnLog)}");
                    _capSpawnLog.Clear();
                    // Recruit path: with the cap reached the gate must refuse before the game sees the request.
                    Row(eChimps.CHIMP_TYPE_KNIGHT).Text = knights.ToString();
                    int pendingBefore = PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT);
                    if (knights <= 0)
                        Note("SKIP recruit gate with the lobby cap: no Knight alive to fill the cap");
                    else
                    {
                        int result = global::EngineInterface.GameAction(global::Enums.GameActionCommand.MakeTroop, 1, (int)eChimps.CHIMP_TYPE_KNIGHT, 0);
                        int pendingAfter = PendingRecruits(eChimps.CHIMP_TYPE_KNIGHT);
                        Check($"Recruiting a Knight is refused when the lobby cap ({knights}) is reached", result == 0 && pendingAfter == pendingBefore,
                            $"lobby Knight cap {Cap(LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT))}, {knights} alive; MakeTroop returned {result}, reserved recruits {pendingBefore} -> {pendingAfter}");
                    }
                    // Same tick: three Pikemen with a cap of 1 (no Pikeman of the local player is on the map).
                    Row(eChimps.CHIMP_TYPE_PIKEMAN).Text = "1";
                    for (int i = 0; i < 3; i++) CapSpawn(eChimps.CHIMP_TYPE_PIKEMAN, CapX + 3 * i, CapY + 8);
                    Row(eChimps.CHIMP_TYPE_MACEMAN).Text = "-1";
                    CapSpawn(eChimps.CHIMP_TYPE_MACEMAN, CapX, CapY + 4);
                    break;
                }
                case 3:
                {
                    int pikemen = CountAlive(_capLocal, eChimps.CHIMP_TYPE_PIKEMAN);
                    Check("Lobby Pikeman cap 1: of three Pikemen spawned in the same tick one stays", pikemen == 1,
                        $"{pikemen} alive ({CapSpawned(eChimps.CHIMP_TYPE_PIKEMAN)}); {string.Join("; ", _capSpawnLog.Where(l => l.StartsWith("CHIMP_TYPE_PIKEMAN")))}");
                    CapSpawn(eChimps.CHIMP_TYPE_MACEMAN, CapX + 3, CapY + 4);
                    break;
                }
                case 4:
                {
                    int macemen = CountAlive(_capLocal, eChimps.CHIMP_TYPE_MACEMAN);
                    Check($"Lobby Maceman -1 (unlimited) overrides the Units file's MaxCount {FileMacemanCap}", macemen == 2 && LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_MACEMAN) == null,
                        $"MaxCounts '{vm.MaxCounts}', 2 Macemen spawned, alive {macemen} ({CapSpawned(eChimps.CHIMP_TYPE_MACEMAN)})");
                    Row(eChimps.CHIMP_TYPE_MACEMAN).Text = "";
                    CapSpawn(eChimps.CHIMP_TYPE_MACEMAN, CapX + 6, CapY + 4);
                    break;
                }
                case 5:
                {
                    int macemen = CountAlive(_capLocal, eChimps.CHIMP_TYPE_MACEMAN);
                    Check($"Empty Maceman box: the Units file's MaxCount {FileMacemanCap} applies again (a third Maceman is removed)",
                        macemen == 2 && LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_MACEMAN) == FileMacemanCap,
                        $"MaxCounts '{vm.MaxCounts}', alive {macemen} ({CapSpawned(eChimps.CHIMP_TYPE_MACEMAN)})");
                    Row(eStructs.STRUCT_WELL).Text = "1";
                    Check("Typing 1 in the Well box sets the building cap", LobbyMaxCounts.EffectiveCap(eStructs.STRUCT_WELL) == 1,
                        $"MaxCounts '{vm.MaxCounts}', Well cap {Cap(LobbyMaxCounts.EffectiveCap(eStructs.STRUCT_WELL))}");
                    GameBuildingManagerAPI.Instance.CreatePrefab(_capLocal, WellX, WellY, eMappers.MAPPER_WELL, BuildingScales.GetScale(eMappers.MAPPER_WELL), 0, true, true);
                    break;
                }
                case 6:
                    _capWellsBefore = Wells().Count;
                    WellPlacementCheck();
                    break;
                case 7:
                {
                    var wells = Wells();
                    if (_capWellsBefore != 1 || _wellRaised == 0)
                        Note($"SKIP Well placement blocked by the lobby cap: {(_capWellsBefore != 1 ? $"the first Well was not placed ({_capWellsBefore} standing)" : "the game's build routine raised no placement validation for an API placement")}; " +
                             $"the cap table check above still applies ({wells.Count} Wells standing)");
                    else
                        Check("Lobby Well cap 1: a second Well placement is blocked", _wellBlocked && wells.Count == 1,
                            $"placement validation raised {_wellRaised} time(s), blocked by the cap: {_wellBlocked}; Wells standing {wells.Count}");
                    foreach (int id in wells) GameBuildingManagerAPI.Instance.DeleteBuildingSafe(id);

                    vm.MaxCounts = _capOwnValues;
                    Check("Lobby values restored", vm.MaxCounts == _capOwnValues && LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_MACEMAN) == FileMacemanCap,
                        $"MaxCounts '{vm.MaxCounts}', Knight cap {Cap(LobbyMaxCounts.EffectiveCap(eChimps.CHIMP_TYPE_KNIGHT))}");
                    Note("SKIP host -> client delivery of the lobby values: needs two machines in a lobby (the client side uses the same setter, called by the Script Extender's verified host write)");
                    foreach (int id in _capUnits) KillIfAlive(id);
                    return true;
                }
            }
            NextCapStep();
            return false;
        }

        private int _wellRaised;
        private bool _wellBlocked;

        private unsafe List<int> Wells()
        {
            var buildings = GameBuildingManagerAPI.Instance;
            var ids = new List<int>();
            buildings.GetAllBuildings(ids, null, eStructs.STRUCT_WELL, PlayerRelationship.Self, _capLocal);
            var standing = new List<int>();
            foreach (int id in ids)
                if (buildings.TryGetBuildingById(id, out GameBuilding* b) && b->r_AliveState != AliveState.MarkedForDeletion) standing.Add(id);
            return standing;
        }

        private void WellPlacementCheck()
        {
            using (BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(a => a.Phase == EventHookPhase.Pre && a.Mappers == eMappers.MAPPER_WELL)
                .Subscribe(a => { _wellRaised++; _wellBlocked |= a.CustomValidationRules && a.ForceBlockPlacementState; }))
            {
                GameBuildingManagerAPI.Instance.CreatePrefab(_capLocal, WellX2, WellY, eMappers.MAPPER_WELL, BuildingScales.GetScale(eMappers.MAPPER_WELL), 0, true, false);
            }
        }

        /// <summary>Recruits the recruit gate has reserved for a type (MakeTroopRecruitHook._pending, read by reflection).</summary>
        private static int PendingRecruits(eChimps type)
        {
            var field = typeof(Config.BepInEx.Systems.Handlers.MakeTroopRecruitHook).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null) is Dictionary<eChimps, List<DateTime>> pending && pending.TryGetValue(type, out var list) ? list.Count : 0;
        }

        private static unsafe void KillIfAlive(int unitId)
        {
            if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u) && u->r_AliveState == AliveState.IsAlive)
                GameUnitManagerAPI.Instance.KillUnit(unitId);
        }
    }
}
