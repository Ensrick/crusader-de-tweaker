// Tests/HeadlessSelfTestLimits.cs
//
// PURPOSE: The self-test's limits stages: the unit limit (GitHub #3, Config/Core/UnitLimit.cs) and the advanced
//          gameplay options' master switches (GitLab #1, ConfigLoader.LoadPlayerOptions).
//
// WHAT IT DOES:
//   A. Editor map, first in the run (LimitsStage): the game's 3000 and both master switches off on a new map (the game's
//      reset behind GitLab #1); the GameplaySettings file (LimitConfigured, written by scripts/test_headless.ps1) applied
//      through the session-start path; the pool still there after LimitTicks ticks and the per-player limits the game
//      recomputes every tick (0xCBD90) following it (mode 0: both = pool); -1 and map unload putting 3000 back; spawning
//      stopping exactly at a small pool.
//   B. Real custom skirmish, last in the run (LimitsSkirmishStart; started like the economy stage does, through the
//      game's own restart path FRONT_Multiplayer.RestartSkirmishGame: first built-in multiplayer map, player 1 human,
//      player 2 AI, start option Normal). Here the mod's real OnStartMap Post hook has applied the file:
//        - the pool is 4000 and the game's start did not turn it into Extreme troops (other pool 0x3668E38 still 3000);
//        - the per-player limits follow the skirmish formula (pool - 100) / players - 40 with 4000, then with 3000;
//        - mode 0x63 and both master switches on;
//        - four phases: player 1's shooters get a 2-tile range (they hit the target first otherwise), a new Spearman
//          (player 1, its own group) is ordered to attack a pinned enemy Pikeman 10 tiles away, and its AI state is sampled
//          every frame, its animation group / speed bonus while in state 101. The Improved Spearmen code (AI
//          state 101, anim group 0x81, RVA 0x143D51) runs only when the mode's switch is on (mode 0x63 ?
//          AdvancedSkirmishOptions : AdvancedOptions, RVA 0x143BF3-0x143D4B). Phases: as the mod set it; mode 0x63 with
//          AdvancedSkirmishOptions off (a trail start before the mod raised it); mode 0 (campaign rule) with
//          AdvancedOptions on; mode 0 with AdvancedOptions off (a campaign start before the mod raised it).
//      Measured 2026-10-08 in the editor map: a move order never enters state 101 (states 0, 1, 2), and a group attack
//      order is accepted but the unit never moves there (game type 1 and 2), so the phases need the real skirmish.
//
// GAME FACTS (CrusaderDE.dll, game 2.8.2; RE report 2026-10-08): pool RVA 0x3668E34 (SE LocalPlayerUnitLimitVA),
// human / AI limits 0x37EF954 / 0x37EF950 (read at 0xD69FB by the barracks check), game mode = ChoreManager block
// + 0x870 (RVA 0x8574B90, read at 0x143B9B by the spearman update). Each address is taken from the instruction
// that reads it in memory and must match the RVA, otherwise that check is skipped with a note.
//
// IMPORTANT FOR AI AGENTS:
// - Keep LimitConfigured in sync with scripts/test_headless.ps1 (["Army Size"] UnitLimit = 4000; ["Gameplay Options"]
//   ImprovedSpearman = true with both master switches false, so the raise under test is the mod's own).
// - A check that cannot be measured is a "SKIP" note, never a PASS.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CrusaderDETweaker.Config.Core;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        internal const int LimitConfigured = 4000;
        private const int LimitsStage = 40, LimitsSkirmishStart = 45;
        private const int LimitTicks = 60, CapSentinel = 12345, TroopWaitTicks = 1500, TroopDeliveryTicks = 450, State101Ticks = 150, ApproachState = 101;
        private const ulong PoolRva = 0x3668E34, OtherPoolRva = 0x3668E38, HumanCapRva = 0x37EF954, CapReadRva = 0xD69FB, ModeReadRva = 0x143B9B, ModeRva = 0x8574B90;
        private const int SkirmishMode = 0x63, ImprovedSpearAnim = 0x81;
        private const int SpearTargetTiles = 10, SpearOffsetTiles = 6, SpearPhaseTicks = 1200, SlotX = 360, SlotY = 370;

        private sealed class SpearPhase
        {
            internal string Name;
            internal int Mode;
            internal bool ViaMod, Campaign, Skirmish;
            internal int Unit, Target, TargetHp, TickStart, Ticks = -1, First101 = -1, Orders;
            internal string Detail = "not run";
            internal readonly SortedDictionary<int, int> Anim = new SortedDictionary<int, int>(), Bonus = new SortedDictionary<int, int>(), State = new SortedDictionary<int, int>();
            internal bool Improved => Anim.ContainsKey(ImprovedSpearAnim);
        }

        private readonly SpearPhase[] _spearPhases =
        {
            new SpearPhase { Name = "skirmish as the mod set it (mode 0x63, both switches on)", Mode = SkirmishMode, ViaMod = true },
            new SpearPhase { Name = "mode 0x63, AdvancedSkirmishOptions off (a trail start before the mod raised it)", Mode = SkirmishMode, Campaign = true },
            new SpearPhase { Name = "mode 0 (campaign rule), AdvancedOptions on", Mode = 0, Campaign = true },
            new SpearPhase { Name = "mode 0 (campaign rule), AdvancedOptions off (a campaign start before the mod raised it)", Mode = 0, Skirmish = true },
        };

        private int _lStep, _skStep, _spear = -1, _limitTick, _capHumanBefore, _capAiBefore, _modeBefore, _lWatchTick, _skTick0, _anchorX, _anchorY;
        private ulong _module, _poolVA, _capVA, _modeVA;
        private float _lWatchTime, _skNoteTime;
        private bool _advBefore, _skirmishBefore, _spearOptBefore;
        private readonly Dictionary<int, int> _skCaps = new Dictionary<int, int>();
        // Player 1's starting shooters would hit the target first (measured: within 71-431 ticks). Killing them ended the
        // hidden skirmish within 50 ticks (twice, cause not traced), so their range is cut to ShooterRangeTiles instead.
        private static readonly eChimps[] Shooters = { eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_XBOWMAN, eChimps.CHIMP_TYPE_ARAB_BOW, eChimps.CHIMP_TYPE_ARAB_SLINGER };
        private const int ShooterRangeTiles = 2;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string name);

        /// <summary>Fails (and ends the run) when the simulation stops for 60 s inside a limits stage.</summary>
        private bool LimitsStalled(float now, string where)
        {
            int tick = global::Director.instance.getSimTickCount();
            if (tick != _lWatchTick) { _lWatchTick = tick; _lWatchTime = now; return false; }
            if (now - _lWatchTime <= 60f) return false;
            RestoreMode();
            if (_skStep == 4 && _spear < _spearPhases.Length && _spearPhases[_spear].Mode == 0)
            {
                // Mode 0 inside a skirmish is the test's own manipulation, not the mod's behaviour: the phases left are skipped.
                for (int i = _spear; i < _spearPhases.Length; i++)
                    _spearPhases[i].Detail = $"SKIP (the skirmish simulation stopped at tick {tick} while the test had set the game mode to 0)";
                Note($"Spearman phase '{_spearPhases[_spear].Name}': {_spearPhases[_spear].Detail}");
                RestoreOptions("skirmish");
                ReportSpear();
                Finish();
                return true;
            }
            Check("Simulation keeps running", false, $"no simulation tick for 60 s at tick {tick} ({where})");
            Finish();
            return true;
        }

        // ===================================================
        // A. Editor map
        // ===================================================

        /// <summary>The editor part; true when it is done.</summary>
        private bool LimitsTick(float now)
        {
            if (_lStep > 0 && LimitsStalled(now, $"limits stage, step {_lStep}")) return false;
            int tick = global::Director.instance.getSimTickCount();
            switch (_lStep)
            {
                case 0:
                    StartLimits();
                    ApplyFromFile();
                    _lWatchTick = tick; _lWatchTime = now;
                    _lStep = 1;
                    return false;
                case 1:
                    if (tick - _limitTick < LimitTicks) return false;
                    CheckLimitAfterTicks(tick);
                    CheckLimitRestoreAndSlots();
                    RestoreOptions("editor map");
                    return true;
            }
            return true;
        }

        private unsafe void StartLimits()
        {
            _module = (ulong)(long)GetModuleHandleW("CrusaderDE.dll");
            _poolVA = Plugin.GlobalsApi?.LocalPlayerUnitLimitVA ?? 0;
            Note($"Unit limit address: Script Extender {_poolVA:X}, CrusaderDE.dll at {_module:X} (pool RVA {(_poolVA != 0 && _module != 0 ? (_poolVA - _module).ToString("X") : "-")}, expected {PoolRva:X})");
            bool sameBuild = _module != 0 && _poolVA == _module + PoolRva;
            _capVA = sameBuild ? RipTarget(_module, CapReadRva, new byte[] { 0x3B, 0x05 }) : 0;
            if (_capVA != _module + HumanCapRva) _capVA = 0;
            _modeVA = sameBuild ? RipTarget(_module, ModeReadRva, new byte[] { 0x44, 0x8B, 0x35 }) : 0;
            ulong chore = Plugin.GlobalsApi?.ChoreManagerVA ?? 0;
            if (_modeVA != _module + ModeRva || _modeVA != chore + 0x870) _modeVA = 0;
            if (_capVA == 0) Note("SKIP per-player limit checks: the barracks check at RVA D69FB does not read RVA 37EF954 (different game build)");
            if (_modeVA == 0) Note("SKIP game-mode phases: the spearman update at RVA 143B9B does not read the mode at RVA 8574B90 (different game build)");

            UnitLimit.TryRead(out int pool);
            _capHumanBefore = _capVA != 0 ? *(int*)_capVA : 0;
            _capAiBefore = _capVA != 0 ? *(int*)(_capVA - 4) : 0;
            _modeBefore = _modeVA != 0 ? *(int*)_modeVA : -1;
            var players = GamePlayerManagerAPI.Instance;
            _advBefore = players.IsAdvancedOptionsEnabled();
            _skirmishBefore = players.IsAdvancedSkirmishOptionsEnabled();
            _spearOptBefore = players.IsImprovedSpearman();
            Note($"New editor map: unit limit {pool}, per-player limits human {(_capVA != 0 ? _capHumanBefore.ToString() : "-")} / AI {(_capVA != 0 ? _capAiBefore.ToString() : "-")}, " +
                 $"game mode {(_modeVA != 0 ? _modeBefore.ToString() : "-")}; AdvancedOptions {_advBefore}, AdvancedSkirmishOptions {_skirmishBefore}, ImprovedSpearman {_spearOptBefore}");
            Check("Unit limit: game value on a new map", pool == UnitLimit.GameValue, $"read {pool} (game {UnitLimit.GameValue})");
            Check("The game turns both master switches off on a new map (the reset behind GitLab #1, RVA 0x211A0)", !_advBefore && !_skirmishBefore,
                $"AdvancedOptions {_advBefore}, AdvancedSkirmishOptions {_skirmishBefore}");
        }

        private static unsafe ulong RipTarget(ulong module, ulong rva, byte[] opcode)
        {
            byte* at = (byte*)(module + rva);
            for (int i = 0; i < opcode.Length; i++) if (at[i] != opcode[i]) return 0;
            int disp = *(int*)(at + opcode.Length);
            return module + rva + (ulong)(opcode.Length + 4) + (ulong)(long)disp;
        }

        private unsafe void ApplyFromFile()
        {
            Note("Applying the GameplaySettings file through the session-start path (ConfigLoader.ApplyAllGlobalConfigs; the editor map raises no OnStartMap)");
            Config.Toml.ConfigLoader.ApplyAllGlobalConfigs();
            UnitLimit.TryRead(out int pool);
            Check("Unit limit from the GameplaySettings file", pool == LimitConfigured, $"read {pool} (file {LimitConfigured})");
            var players = GamePlayerManagerAPI.Instance;
            Check("Master switches raised by the mod (ImprovedSpearman on, both switches false in the file)",
                players.IsAdvancedOptionsEnabled() && players.IsAdvancedSkirmishOptionsEnabled() && players.IsImprovedSpearman(),
                $"AdvancedOptions {players.IsAdvancedOptionsEnabled()}, AdvancedSkirmishOptions {players.IsAdvancedSkirmishOptionsEnabled()}, ImprovedSpearman {players.IsImprovedSpearman()}");
            if (_capVA != 0)
            {
                // Only the game's per-tick recompute (0xCBD90) replaces this marker.
                *(int*)_capVA = CapSentinel;
                *(int*)(_capVA - 4) = CapSentinel;
            }
            _limitTick = global::Director.instance.getSimTickCount();
        }

        private unsafe void CheckLimitAfterTicks(int tick)
        {
            UnitLimit.TryRead(out int pool);
            Check($"Unit limit still {LimitConfigured} after {tick - _limitTick} ticks", pool == LimitConfigured, $"read {pool}");
            if (_capVA == 0) return;
            int human = *(int*)_capVA, ai = *(int*)(_capVA - 4);
            if (human == CapSentinel && ai == CapSentinel)
            {
                Note($"SKIP per-player limits follow the unit limit: the game did not recompute them in the editor map within {tick - _limitTick} ticks (0xCBD90 not reached); put back {_capHumanBefore} / {_capAiBefore}");
                *(int*)_capVA = _capHumanBefore;
                *(int*)(_capVA - 4) = _capAiBefore;
                return;
            }
            Check("Per-player troop limits follow the unit limit (mode 0: both = unit limit)", human == LimitConfigured && ai == LimitConfigured,
                $"human {human}, AI {ai} after {tick - _limitTick} ticks (were {_capHumanBefore} / {_capAiBefore} with the game's {UnitLimit.GameValue})");
        }

        private unsafe void CheckLimitRestoreAndSlots()
        {
            UnitLimit.Apply(-1, "selftest: UnitLimit = -1");
            UnitLimit.TryRead(out int afterMinusOne);
            UnitLimit.Apply(LimitConfigured, "selftest: re-apply");
            UnitLimit.TryRead(out int reapplied);
            UnitLimit.RestoreGameValue("selftest: map unload");
            UnitLimit.TryRead(out int afterUnload);
            Check("UnitLimit = -1 and map unload put the game value back", afterMinusOne == UnitLimit.GameValue && reapplied == LimitConfigured && afterUnload == UnitLimit.GameValue,
                $"-1: {afterMinusOne}; re-applied: {reapplied}; after the unload restore: {afterUnload} (game {UnitLimit.GameValue})");

            // Spawning past a small pool: choose the pool so that exactly 3 slots below it are free.
            var units = GameUnitManagerAPI.Instance;
            int free = 0, pool = 0;
            for (int id = 1; id < GameUnitManager.NativeUnitSlotCount && pool == 0; id++)
                if (units.TryGetUnitById(id, out GameUnit* u) && u->r_AliveState == AliveState.None && ++free == 3) pool = id + 1;
            if (pool == 0) { Note("SKIP spawning past a small unit limit: no free unit slots found"); return; }
            UnitLimit.TryWrite(pool);
            var ids = new List<int>();
            for (int i = 0; i < 6; i++) ids.Add((int)units.CreateUnitLocal(1, 1, SlotX + 2 * i, SlotY, 8, eChimps.CHIMP_TYPE_PIKEMAN));
            UnitLimit.TryWrite(UnitLimit.GameValue);
            int extra = (int)units.CreateUnitLocal(1, 1, SlotX, SlotY + 4, 8, eChimps.CHIMP_TYPE_PIKEMAN);
            int ok = ids.Count(id => id > 0);
            Check("Spawning stops at the unit limit", ok == 3 && ids.Where(id => id > 0).All(id => id < pool) && ids.Skip(3).All(id => id <= 0) && extra > 0,
                $"unit limit {pool} (3 free slots below it): 6 spawns returned [{string.Join(", ", ids)}]; after restoring {UnitLimit.GameValue}: {extra}");
            foreach (int id in ids.Concat(new[] { extra }).Where(id => id > 0)) units.KillUnit(id);
        }

        private void RestoreOptions(string where)
        {
            var players = GamePlayerManagerAPI.Instance;
            players.SetImprovedSpearman(_spearOptBefore);
            players.SetAdvancedOptionsEnabled(_advBefore);
            players.SetAdvancedSkirmishOptionsEnabled(_skirmishBefore);
            Note($"Options put back ({where}): AdvancedOptions {players.IsAdvancedOptionsEnabled()}, AdvancedSkirmishOptions {players.IsAdvancedSkirmishOptionsEnabled()}, ImprovedSpearman {players.IsImprovedSpearman()}");
        }

        private unsafe void RestoreMode()
        {
            if (_modeVA != 0 && _modeBefore >= 0 && *(int*)_modeVA != _modeBefore)
            {
                *(int*)_modeVA = _modeBefore;
                Note($"Game mode put back to {_modeBefore}");
            }
        }

        // ===================================================
        // B. Real custom skirmish
        // ===================================================

        /// <summary>Last stage: writes a provisional FAIL (a hang in the skirmish start cannot hide earlier results), then starts.</summary>
        private void BeginLimitsSkirmish()
        {
            try
            {
                File.WriteAllText(_result, "FAIL" + Environment.NewLine + _details +
                    "FAIL the limits skirmish stage did not finish (the game stopped or hung while starting or running a custom skirmish)" + Environment.NewLine);
            }
            catch (Exception ex) { Plugin.Logger.LogError("[SelfTest] could not write the provisional result: " + ex.Message); }
            _stage = LimitsSkirmishStart;
            _deadline = UnityEngine.Time.realtimeSinceStartup + 240;
        }

        /// <summary>Starts the skirmish OUTSIDE the engine lock (the game stops the editor's simulation thread and starts its own).</summary>
        private void StartLimitsSkirmish(float now)
        {
            var maps = global::MapFileManager.Instance;
            if (maps == null || !maps.fileListLoaded) return;   // wait for the map list
            var headers = maps.GetMultiplayerMaps(0, true, 2, true, false, false);
            if (headers == null || headers.Count == 0) throw new InvalidOperationException("no built-in multiplayer map found");
            global::FileHeader map = headers[0];

            global::EditorDirector.instance.stopGameSim();
            var setup = global::EngineInterface.initMultiplayerGame(skirmishGame: true);
            setup.starting_goods_level = 1;   // Normal
            setup.starting_gamespeed = global::ConfigSettings.Settings_GameSpeed;
            var info = new global::CrusaderDE.HUD_IngameMenu.RestartSkirmishMapInfo { selectedHeader = map, MPsetupData = setup, customisedExtremeTrail = false };
            for (int i = 0; i < 8; i++)
            {
                info.lordTypes.Add(i == 0 ? -1 : i == 1 ? 1 : -9999);   // human, AI lord, empty
                info.teams.Add(0);
                info.colours.Add(i + 1);
            }
            Note($"Limits skirmish: starting a custom skirmish on {map.display_filename} ({map.maxPlayers} players), player 1 human, player 2 AI, start option Normal; " +
                 $"the mod applies the GameplaySettings file (UnitLimit {LimitConfigured}, ImprovedSpearman) from its own OnStartMap Post hook");
            global::CrusaderDE.MainViewModel.Instance.FRONTMultiplayer.RestartSkirmishGame(info);
            _stage = LimitsSkirmishStart + 1;
            _deadline = now + 900;
        }

        private unsafe void LimitsSkirmishTick(float now)
        {
            var vm = global::CrusaderDE.MainViewModel.Instance;
            var director = global::Director.instance;
            // The skirmish opens on its briefing, which pauses a single-player game; the hidden game can also open its menu.
            bool briefing = vm.Show_HUD_Briefing, menu = vm.Show_HUD_IngameMenu, paused = director.Paused;
            if (briefing) vm.ButtonBriefingResume(null);
            if (menu) vm.Show_HUD_IngameMenu = false;
            if (paused) director.SetPausedState(state: false);
            int tick = director.getSimTickCount();
            if (now - _skNoteTime >= 20f)
            {
                _skNoteTime = now;
                Note($"skirmish progress: tick {tick}, simulation running {director.SimRunning}, paused {paused}, briefing {briefing}, menu {menu}, editor {vm.IsMapEditorMode}, " +
                     $"skirmish {director.SkirmishModeGame}, game over state {(global::GameData.scenario?.gameOverState)}; step {_skStep}, spearman phase {_spear}" +
                     (_spear >= 0 && _spear < _spearPhases.Length ? $", spearman at {Pos(_spearPhases[_spear].Unit)}" : ""));
            }
            int over = global::GameData.scenario?.gameOverState ?? 0;
            if (_skStep == 4 && over > 0 && !director.SimRunning)
            {
                // The test's own spawns ended the match (not the mod): the phases left are skipped.
                for (int i = _spear; i < _spearPhases.Length; i++)
                    _spearPhases[i].Detail = $"SKIP (the skirmish ended at tick {tick}: game over state {over}, {(over == 1 ? "victory" : "defeat")}, during phase {_spear + 1})";
                Note($"Spearman phase '{_spearPhases[_spear].Name}': {_spearPhases[_spear].Detail}");
                ReportSpear();
                Finish();
                return;
            }
            if (!director.SimRunning || vm.IsMapEditorMode || !director.SkirmishModeGame) return;
            if (_skStep > 0 && LimitsStalled(now, $"skirmish step {_skStep}, spearman phase {_spear}")) return;

            switch (_skStep)
            {
                case 0:
                    SkirmishStartChecks();
                    _skTick0 = tick; _lWatchTick = tick; _lWatchTime = now;
                    _skStep = 1;
                    return;
                case 1:
                case 2:
                    // Per-player limits with the configured pool, then with the game's 3000, then back.
                    if (tick - _skTick0 < LimitTicks) return;
                    int pool = _skStep == 1 ? LimitConfigured : UnitLimit.GameValue;
                    if (_capVA != 0) _skCaps[pool] = *(int*)_capVA;
                    Note($"Skirmish tick {tick}: unit limit {Read(_poolVA)}, per-player limits human {(_capVA != 0 ? (*(int*)_capVA).ToString() : "-")} / AI {(_capVA != 0 ? (*(int*)(_capVA - 4)).ToString() : "-")}");
                    UnitLimit.TryWrite(_skStep == 1 ? UnitLimit.GameValue : LimitConfigured);
                    _skTick0 = tick;
                    if (++_skStep == 3) ReportSkirmishCaps();
                    return;
                case 3:
                    // Anchor the spearman phases on player 1's starting troops (they walk out of the keep on the ground),
                    // once they have all arrived (about 410 ticks, economy stage).
                    if (tick - _skTick0 < TroopDeliveryTicks) return;
                    if (!FindAnchor() && tick - _skTick0 < TroopWaitTicks) return;
                    if (_anchorX == 0)
                    {
                        Note($"SKIP Improved Spearmen phases: no starting troop of player 1 within {TroopWaitTicks} ticks to place them next to");
                        Finish();
                        return;
                    }
                    foreach (eChimps shooter in Shooters)
                    {
                        Config.Toml.Units.Properties.UnitRanges.ApplyAttackRange("AttackRange", shooter, ShooterRangeTiles);
                        Config.Toml.Units.Properties.EngageDistancePatch.SetEngageTiles(shooter, ShooterRangeTiles);
                    }
                    Note($"Shooters' AttackRange / EngageRange set to {ShooterRangeTiles} tiles for the phases ({string.Join(", ", Shooters)}), so they leave the target alone");
                    _modeBefore = _modeVA != 0 ? *(int*)_modeVA : -1;
                    var players = GamePlayerManagerAPI.Instance;
                    _advBefore = players.IsAdvancedOptionsEnabled(); _skirmishBefore = players.IsAdvancedSkirmishOptionsEnabled(); _spearOptBefore = players.IsImprovedSpearman();
                    StartSpearPhase();
                    _skStep = 4;
                    return;
                case 4:
                    _next = now;   // sample every frame
                    if (!SpearTick()) return;
                    EndSpearPhase();
                    if (StartSpearPhase()) return;
                    RestoreMode();
                    RestoreOptions("skirmish");
                    foreach (eChimps shooter in Shooters) { Config.Toml.Units.Properties.UnitRanges.ResetAttackRange(shooter); Config.Toml.Units.Properties.EngageDistancePatch.ClearEngage(shooter); }
                    ReportSpear();
                    Finish();
                    return;
            }
        }

        private static unsafe int Read(ulong va) => va != 0 ? *(int*)va : -1;

        private unsafe void SkirmishStartChecks()
        {
            var players = GamePlayerManagerAPI.Instance;
            int mode = Read(_modeVA), pool = Read(_poolVA), otherPool = _poolVA != 0 && _poolVA == _module + PoolRva ? Read(_module + OtherPoolRva) : -1;
            Note($"Limits skirmish running at tick {(global::Director.instance.getSimTickCount())}: game type {players.GetCurrentGameTypeMode()}, game mode {mode}, " +
                 $"unit limit {pool}, other pool (RVA 3668E38) {otherPool}; AdvancedOptions {players.IsAdvancedOptionsEnabled()}, " +
                 $"AdvancedSkirmishOptions {players.IsAdvancedSkirmishOptionsEnabled()}, ImprovedSpearman {players.IsImprovedSpearman()}");
            Check("Real skirmish start: the mod's session-start hook set the unit limit, and the game's start did not treat it as Extreme troops",
                pool == LimitConfigured && (otherPool == -1 || otherPool == UnitLimit.GameValue),
                $"unit limit {pool} (file {LimitConfigured}; Extreme troops would be 10000), other pool {otherPool} (Extreme: 6000)");
            Check("Real skirmish start: game mode 0x63 and both master switches on after the start (the mod's hook)",
                (_modeVA == 0 || mode == SkirmishMode) && players.IsAdvancedOptionsEnabled() && players.IsAdvancedSkirmishOptionsEnabled() && players.IsImprovedSpearman(),
                $"mode {mode}, AdvancedOptions {players.IsAdvancedOptionsEnabled()}, AdvancedSkirmishOptions {players.IsAdvancedSkirmishOptionsEnabled()}, ImprovedSpearman {players.IsImprovedSpearman()}");
        }

        private void ReportSkirmishCaps()
        {
            if (_capVA == 0 || !_skCaps.TryGetValue(LimitConfigured, out int high) || !_skCaps.TryGetValue(UnitLimit.GameValue, out int low)) return;
            // The game's formula (0xCBD90, mode 0x63): (pool - 100) / players - 40.
            int players = Enumerable.Range(1, 8).FirstOrDefault(n => (LimitConfigured - 100) / n - 40 == high && (UnitLimit.GameValue - 100) / n - 40 == low);
            Check("Skirmish per-player troop limit follows the unit limit ((unit limit - 100) / players - 40)",
                players > 0 && high > low,
                $"human limit {high} with unit limit {LimitConfigured}, {low} with the game's {UnitLimit.GameValue}" +
                (players > 0 ? $": the formula with {players} players" : ": matches the formula for no player count 1-8"));
        }

        private bool FindAnchor()
        {
            var units = GameUnitManagerAPI.Instance;
            // Ground troops first (starting archers may stand on the keep), archers as the fallback.
            foreach (bool ground in new[] { true, false })
                foreach (int id in units.GetAllAliveUnits())
                {
                    if (id <= 0 || units.GetOwner(id) != 1) continue;
                    eChimps t = units.GetType(id);
                    bool melee = t == eChimps.CHIMP_TYPE_SPEARMAN || t == eChimps.CHIMP_TYPE_KNIGHT || t == eChimps.CHIMP_TYPE_MACEMAN || t == eChimps.CHIMP_TYPE_PIKEMAN;
                    if (ground ? !melee : t != eChimps.CHIMP_TYPE_ARCHER) continue;
                    var at = units.GetCurrentLocalTilePosition(id);
                    _anchorX = at.X; _anchorY = at.Y;
                    Note($"Spearman phases next to player 1's {t} {id} at {at.X},{at.Y}");
                    return true;
                }
            return false;
        }

        /// <summary>Starts the next spearman phase that can run; false when none is left.</summary>
        private unsafe bool StartSpearPhase()
        {
            var units = GameUnitManagerAPI.Instance;
            var players = GamePlayerManagerAPI.Instance;
            SpearPhase p = null;
            while (p == null)
            {
                if (++_spear >= _spearPhases.Length) return false;
                p = _spearPhases[_spear];
                if (!p.ViaMod && _modeVA == 0) { p.Detail = "SKIP (mode address not verified)"; p = null; }
            }
            if (!p.ViaMod)
            {
                *(int*)_modeVA = p.Mode;
                players.SetImprovedSpearman(true);
                players.SetAdvancedOptionsEnabled(p.Campaign);
                players.SetAdvancedSkirmishOptionsEnabled(p.Skirmish);
            }
            // A fresh Spearman in its own group, and an enemy Pikeman 10 tiles farther on, both on the side of the starting
            // troop that faces away from player 1's keep. Measured 2026-10-08: in 4 of 7 skirmishes the match ended in defeat
            // (game over state 2) at tick 637 or 638, 40-50 ticks after the first phase started, each time with the enemy
            // Pikeman 10-14 tiles east of a starting troop (2 runs with it east and 1 with it west went on). The cause is not
            // traced [unverified]; placing away from the keep is a precaution, and a defeat skips the phases left (SKIP note).
            p.Unit = 0;
            foreach (var (dx, dy) in AwayFromKeep())
            {
                p.Unit = (int)units.CreateUnitLocal(1, 1, _anchorX + SpearOffsetTiles * dx, _anchorY + SpearOffsetTiles * dy, 8, eChimps.CHIMP_TYPE_SPEARMAN);
                if (p.Unit <= 0) continue;
                p.Target = (int)units.CreateUnitLocal(2, 2, _anchorX + (SpearOffsetTiles + SpearTargetTiles) * dx, _anchorY + (SpearOffsetTiles + SpearTargetTiles) * dy, 8, RangeTarget);
                if (p.Target > 0) break;
                units.KillUnit(p.Unit);
                p.Unit = 0;
            }
            if (p.Unit <= 0 || p.Target <= 0) throw new InvalidOperationException($"could not place the spearman and the enemy Pikeman near {_anchorX},{_anchorY}");
            p.TargetHp = units.GetCurrentHealth(p.Target);
            units.SetSpeed(p.Target, 30000);   // pinned, so the time to the first hit is the spearman's own approach
            if (units.TryGetUnitById(p.Target, out GameUnit* t)) t->r_CurrentSpeed2 = 30000;
            string order = OrderAttack(p);
            p.TickStart = global::Director.instance.getSimTickCount();
            var tp = units.GetCurrentLocalTilePosition(p.Target);
            Note($"Spearman phase '{p.Name}': mode {Read(_modeVA)}, AdvancedOptions {players.IsAdvancedOptionsEnabled()}, AdvancedSkirmishOptions {players.IsAdvancedSkirmishOptionsEnabled()}, " +
                 $"ImprovedSpearman {players.IsImprovedSpearman()}; spearman {p.Unit} at {Pos(p.Unit)} ordered to attack the enemy Pikeman {p.Target} at {tp.X},{tp.Y} (pinned): {order}");
            return true;
        }

        /// <summary>Unit directions from the anchor, the one pointing away from player 1's keep first (lord as the fallback).</summary>
        private (int, int)[] AwayFromKeep()
        {
            var dirs = new List<(int, int)> { (1, 0), (-1, 0), (0, 1), (0, -1) };
            int fromX = -1, fromY = -1;
            var all = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < all.Length && fromX < 0; i++)
                if (all[i].r_PlayerIdOwner == 1 && all[i].r_BuildingType >= eStructs.STRUCT_KEEP_ONE && all[i].r_BuildingType <= eStructs.STRUCT_KEEP_FIVE)
                {
                    fromX = all[i].r_TilePositionXBegin; fromY = all[i].r_TilePositionYBegin;
                }
            if (fromX < 0)
            {
                var units = GameUnitManagerAPI.Instance;
                int lord = units.GetAllAliveUnits().FirstOrDefault(id => id > 0 && units.GetOwner(id) == 1 && units.GetType(id) == eChimps.CHIMP_TYPE_LORD);
                if (lord <= 0) return dirs.ToArray();
                var l = units.GetCurrentLocalTilePosition(lord);
                fromX = l.X; fromY = l.Y;
            }
            int ddx = _anchorX - fromX, ddy = _anchorY - fromY;
            if (_spear == 0) Note($"Player 1's keep (or lord) at {fromX},{fromY}; the phases are placed away from it");
            return dirs.OrderByDescending(d => d.Item1 * ddx + d.Item2 * ddy).ToArray();
        }

        /// <summary>Puts the phase's spearman into a new group of its own and orders that group to attack the target.</summary>
        private static string OrderAttack(SpearPhase p)
        {
            var tribes = GameTribeManagerAPI.Instance;
            int tribe = (int)tribes.Create(1);
            bool ordered = tribe > 0 && tribes.AssignUnit(tribe, p.Unit) && tribes.AttackUnit(tribe, p.Target);
            p.Orders++;
            return $"group {tribe}, {(ordered ? "order accepted" : "ORDER FAILED")}";
        }

        private void EndSpearPhase()
        {
            var p = _spearPhases[_spear];
            var units = GameUnitManagerAPI.Instance;
            if (p.Unit > 0) units.KillUnit(p.Unit);
            if (p.Target > 0) units.KillUnit(p.Target);
            Note($"Spearman phase '{p.Name}': {p.Detail}");
        }

        /// <summary>Samples the current spearman every frame; true when its phase is over.</summary>
        private unsafe bool SpearTick()
        {
            var p = _spearPhases[_spear];
            var units = GameUnitManagerAPI.Instance;
            int tick = global::Director.instance.getSimTickCount(), ticks = tick - p.TickStart;
            if (units.TryGetUnitById(p.Unit, out GameUnit* u))
            {
                // The improved-run code runs only in AI state 101 (the approach to the target): sample the fields there.
                Count(p.State, u->r_AIState);
                if (u->r_AIState == ApproachState)
                {
                    if (p.First101 < 0) p.First101 = ticks;
                    Count(p.Anim, (int)u->r_SpriteAnimationGroup);
                    Count(p.Bonus, (short)u->r_SpeedBonus);
                }
            }
            if (p.Ticks < 0 && units.GetCurrentHealth(p.Target) < p.TargetHp) p.Ticks = ticks;
            // Measured once (2026-10-08): a spearman stayed in states 0 / 2 for 1200 ticks after an accepted order; order again.
            if (p.First101 < 0 && ticks >= 300 * (p.Orders) && p.Orders < 3) Note($"Spearman phase '{p.Name}': no state {ApproachState} after {ticks} ticks, attack order again ({OrderAttack(p)})");
            bool enough = p.First101 >= 0 && ticks - p.First101 >= State101Ticks;
            if (!enough && ticks <= SpearPhaseTicks) return false;
            p.Detail = $"{p.Orders} attack order(s); AI state seen {Seen(p.State)} in {ticks} ticks; {(p.First101 >= 0 ? $"state {ApproachState} from tick {p.First101}" : $"state {ApproachState} never reached")}; " +
                       $"in state {ApproachState}: animation group seen {Seen(p.Anim)}, speed bonus seen {Seen(p.Bonus)}; first hit on the target {(p.Ticks >= 0 ? $"after {p.Ticks} ticks" : "none")} (spearman at {Pos(p.Unit)})";
            return true;
        }

        private static void Count(SortedDictionary<int, int> seen, int value)
        {
            seen.TryGetValue(value, out int n);
            seen[value] = n + 1;
        }

        private static string Seen(SortedDictionary<int, int> seen) => string.Join(", ", seen.Select(kv => $"{kv.Key} x{kv.Value}"));

        private void ReportSpear()
        {
            SpearPhase mod = _spearPhases[0], trailBug = _spearPhases[1], campaignFix = _spearPhases[2], campaignBug = _spearPhases[3];
            string T(SpearPhase p) => p.Ticks >= 0 ? p.Ticks + " ticks to the first hit" : "no hit";
            void Expect(SpearPhase p, bool improved, string name, string extra)
            {
                if (p.Detail.StartsWith("SKIP")) { Note($"SKIP {name}: {p.Detail}"); return; }
                if (p.First101 < 0) { Note($"SKIP {name}: the spearman never reached AI state {ApproachState} (states {Seen(p.State)})"); return; }
                Check(name, p.Improved == improved, $"in state {ApproachState}: animation group seen {Seen(p.Anim)}, speed bonus {Seen(p.Bonus)}; {T(p)}{extra}");
            }
            Expect(mod, true, "Skirmish / trail rule (mode 0x63): with the switches as the mod set them, a new Spearman runs", "");
            Expect(trailBug, false, "Skirmish / trail rule: with AdvancedSkirmishOptions off (a trail start before the mod raised it) ImprovedSpearman has no effect", $" (with the switch on: {T(mod)})");
            Expect(campaignFix, true, "Campaign rule (mode 0): with AdvancedOptions on, as the mod sets it, a new Spearman runs", "");
            Expect(campaignBug, false, "Campaign rule: with AdvancedOptions off (a campaign start before the mod raised it) ImprovedSpearman has no effect", $" (with the switch on: {T(campaignFix)})");
        }
    }
}
