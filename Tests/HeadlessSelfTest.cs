// Tests/HeadlessSelfTest.cs
//
// PURPOSE: Opt-in in-game integration test for features the on-load suites cannot reach (they use mocks):
//          team colours on the real game palettes / UI tables, and Speed values above the Script
//          Extender's 6 on real units. Started only by scripts/test_headless.ps1:
//            Stronghold Crusader Definitive Edition.exe -batchmode ... -cdt-selftest "<result.txt>"
//          Without that flag (every normal launch) Create() returns at once and nothing runs.
//
// WHAT IT DOES (windowless; the runner sets the config values below and restores the files after):
//   1. Waits for the main menu, notes whether the menu-time team-colour apply already ran.
//   2. Creates a disposable editor map (nothing is saved), checks the speed table and the colours:
//      sprite palettes (default / lord / jester recoloured, knight-horse untouched), the game's own
//      lookup spriteLoader.GetGMMaterial, both UI tables, then revert (-1) and re-apply.
//   3. Spawns a Catapult and a Siege Tower (speed fields must equal the configured 8 / 1), then two
//      Archers 10 tiles from a target: one created while the table says 8, one after setting the table
//      to 1. Both walk the same 10 tiles; the Speed 8 archer must take clearly longer (ticks counted).
//   4. Crash guard (2.9.1): the Crossbowman EngageRange the runner writes must be refused, and a Crossbowman
//      must run 300 ticks next to an enemy (without the guard SE <= 2.13.1 crashes on its first update).
//   5. Ranges: checks the Archer AttackRange / EngageRange from the Units file reached the Script
//      Extender, then measures behaviour in four phases (game ranges, AttackRange only, EngageRange only,
//      both): an idle Archer (player 1) and an enemy Pikeman (player 2) are placed 96, 84, ... 16 tiles
//      apart, closer each 400 ticks, until the Archer fires (a live projectile from it) or the Pikeman loses health. The first
//      damaging distance is the effective engage distance. Idle soldiers wake within 400 world units
//      whatever the overrides (SE #195), so that result is a KNOWN LIMITATION note, and a NOTICE if it changes.
//      A watchdog logs the tick every 60 s and fails the run if the simulation stops for 60 s.
//   6. Writes PASS / FAIL + details to the result file and quits.
//
// IMPORTANT FOR AI AGENTS:
// - Keep the constants in sync with scripts/test_headless.ps1, which writes them into the configs.
// - Native calls are made under EngineInterface.threadLock (the game's own simulation lock), the
//   pattern of shcde-naval-mod's HeadlessMovementTest.
//
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CrusaderDETweaker.Config.Core;
using SHCDESE.API;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Tests
{
    internal sealed class HeadlessSelfTest
    {
        internal const string Flag = "-cdt-selftest";

        // Written into the configs by scripts/test_headless.ps1.
        internal const int CatapultSpeed = 8, SiegeTowerSpeed = 1, ArcherSpeed = 8, FastArcherSpeed = 1;
        private static readonly Color RedOverride = new Color(0f, 1f, 0f);    // Red = [0, 255, 0]
        private static readonly Color BlueOverride = new Color(1f, 0f, 1f);   // Blue = "#FF00FF"
        private const int WalkTiles = 10;

        // Archer ranges written by scripts/test_headless.ps1 (tiles; the game's AttackRange is 54).
        // Not the Crossbowman: SE <= 2.13.1 crashes the game with a Crossbowman EngageRange (its engage hook
        // clobbers r13, which the Crossbowman's native compare uses; see UnitRanges.EngageRangeCrashUnits).
        internal const int XbowAttackRange = 80, XbowEngageRange = 80;
        private static readonly int[] RangeLadder = { 96, 84, 72, 64, 56, 48, 42, 36, 32, 28, 24, 20, 16 };
        private const int TicksPerRung = 400, RangeShooterX = 340, RangeY = 400;
        private readonly System.Collections.Generic.List<int> _projectiles = new System.Collections.Generic.List<int>();
        private const eChimps RangeShooter = eChimps.CHIMP_TYPE_ARCHER, RangeTarget = eChimps.CHIMP_TYPE_PIKEMAN;

        private sealed class RangePhase
        {
            internal string Name;
            internal bool Attack, Engage;
            internal int HitAt = -1;
            internal string Detail = "not run";
        }

        private readonly RangePhase[] _rangePhases =
        {
            new RangePhase { Name = "game ranges (control)" },
            new RangePhase { Name = $"AttackRange {XbowAttackRange} only", Attack = true },
            new RangePhase { Name = $"EngageRange {XbowEngageRange} only", Engage = true },
            new RangePhase { Name = $"AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange}", Attack = true, Engage = true },
        };
        private int _phase = -1, _rung, _shooter, _target, _targetHp, _rungTick;
        private int _catapult, _tower;
        // Crash guard (2.9.1): the runner writes this EngageRange for the Crossbowman; CDT must refuse it.
        internal const int GuardEngageRange = 80;
        private const int GuardTicks = 300;
        private int _guardUnit, _guardEnemy, _guardTick;

        private readonly string _result;
        private readonly StringBuilder _details = new StringBuilder();
        private readonly object _engineLock;
        private int _stage, _failures;
        private float _next, _deadline;
        private bool _done;
        private bool _menuApplied;
        private int _slowArcher, _fastArcher, _startTick, _slowTicks = -1, _fastTicks = -1;
        private int _slowTargetX, _slowTargetY, _fastTargetX, _fastTargetY;

        private HeadlessSelfTest(string result)
        {
            _result = Path.GetFullPath(result);
            _engineLock = typeof(global::EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                          ?? new object();
            _deadline = Time.realtimeSinceStartup + 240;
            Note("START Crusader DE Tweaker " + PluginInfo.PLUGIN_VERSION + " batch-mode self-test");
        }

        /// <summary>Starts the self-test when the game was launched with the flag in batch mode; otherwise does nothing.</summary>
        internal static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0) return;
            if (!Application.isBatchMode || index + 1 >= args.Length)
            {
                Plugin.Logger.LogError($"[SelfTest] {Flag} needs -batchmode and a result path; not started.");
                return;
            }
            var test = new HeadlessSelfTest(args[index + 1]);
            SelfTestDriver.Run(test.Tick);
        }

        private void Note(string text)
        {
            Plugin.Logger.LogInfo("[SelfTest] " + text);
            _details.AppendLine(text);
        }

        private void Check(string name, bool ok, string detail)
        {
            if (!ok) _failures++;
            Note($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
        }

        private void Tick()
        {
            if (_done) return;
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now < _next) return;
                _next = now + 0.25f;
                if (now > _deadline) throw new TimeoutException("timed out at stage " + _stage);

                var vm = global::CrusaderDE.MainViewModel.Instance;
                if (_stage == 0)
                {
                    if (vm == null || !global::CrusaderDE.MainViewModel.viewModelLoaded || vm.HUDmain == null || vm.IngameUI == null ||
                        global::Director.instance == null || global::EditorDirector.instance == null || global::GameMap.instance == null ||
                        global::spriteLoader.instance == null || !global::spriteLoader.instance.spritesLoaded) return;
                    if (global::Director.instance.SimRunning) throw new InvalidOperationException("a map is already running");

                    _menuApplied = Near(Palette("defaultColours")[1], RedOverride);
                    Note("Main menu reached; team colours applied by the menu path before any map: " + _menuApplied);

                    global::Director.instance.SetWaitCursor();
                    vm.InitNewScene(global::Enums.SceneIDS.MapEditor);
                    global::EditorDirector.instance.createNewMap(160, global::Enums.GameModes.BUILD, false, false);
                    Note("Created a disposable editor map (not saved)");
                    _stage = 1; _next = now + 2;
                    return;
                }

                lock (_engineLock)
                {
                    if (_stage == 1)
                    {
                        if (!vm.IsMapEditorMode || !global::Director.instance.SimRunning) return;
                        CheckTeamColors();
                        CheckSpeedTableAndSpawn();
                        _stage = 2; _deadline = now + 180;
                        return;
                    }
                    if (_stage == 2)
                    {
                        int tick = global::Director.instance.getSimTickCount();
                        if (_slowTicks < 0 && At(_slowArcher, _slowTargetX, _slowTargetY)) _slowTicks = tick - _startTick;
                        if (_fastTicks < 0 && At(_fastArcher, _fastTargetX, _fastTargetY)) _fastTicks = tick - _startTick;
                        if (_slowTicks < 0 || _fastTicks < 0) return;
                        Check("Archer walk time",
                            _fastTicks > 0 && _slowTicks > _fastTicks * 2.5,
                            $"{WalkTiles} tiles: Speed {FastArcherSpeed} took {_fastTicks} ticks, Speed {ArcherSpeed} took {_slowTicks} ticks (ratio {(_fastTicks > 0 ? (double)_slowTicks / _fastTicks : 0):0.00}; the game's step formula predicts about 4.5 with no speed bonus)");
                        StartCrossbowGuard();
                        _deadline = now + 120;
                        return;
                    }
                    if (_stage == 3) { if (SimStalled(now)) return; RangeTick(); }
                    else if (_stage == 4) GuardTick(now);
                }
            }
            catch (Exception ex)
            {
                _failures++;
                Note("FAIL exception: " + ex);
                if (_stage == 2) Note($"progress: slow archer {Pos(_slowArcher)} ticks={_slowTicks}, fast archer {Pos(_fastArcher)} ticks={_fastTicks}");
                if (_stage == 3) Note($"progress: range phase {_phase}, rung {_rung}, shooter {Pos(_shooter)}, target {Pos(_target)}; " +
                                      string.Join("; ", _rangePhases.Select(p => p.Name + ": " + p.Detail)));
                Finish();
            }
        }

        // ===================================================
        // Team colours
        // ===================================================

        private void CheckTeamColors()
        {
            if (!Near(Palette("defaultColours")[1], RedOverride))
            {
                Note("Team colours not applied yet (no menu map unload in batch mode); applying through the menu path now");
                Config.Toml.ConfigLoader.ApplyTeamColorsFromFile("selftest");
            }

            var def = Palette("defaultColours");
            var lord = Palette("lordLadyColours");
            var jester = Palette("jesterColours");
            var knight = Palette("knightColours");
            Check("Sprite palette Red", Near(def[1], RedOverride), $"defaultColours[1]={def[1]}");
            Check("Sprite palette Blue (hex)", Near(def[2], BlueOverride), $"defaultColours[2]={def[2]}");
            Check("Sprite palette Orange untouched (-1)", Near(def[3], new Color(0.945f, 0.478f, 0.121f)), $"defaultColours[3]={def[3]}");
            Check("Lord palette Red", Near(lord[1], RedOverride), $"lordLadyColours[1]={lord[1]}");
            Check("Jester palette Red", Near(jester[1], RedOverride), $"jesterColours[1]={jester[1]}");
            Check("Knight-horse palette untouched", Near(knight[1], new Color(0.6698f, 0.6698f, 0.6698f)), $"knightColours[1]={knight[1]}");

            global::spriteLoader.instance.GetGMMaterial(global::Enums.GM.GM_BODY_ARCHER, 1, 0, out Color archerRed, 0);
            Check("Game sprite lookup (archer, colour slot 1)", Near(archerRed, RedOverride), $"GetGMMaterial colour={archerRed}");

            Color ui = global::OnScreenText.Instance.MPTeamColours[1];
            Check("Interface colour Red (names, panels)", Near(ui, RedOverride), $"OnScreenText.MPTeamColours[1]={ui}");
            var chat = global::CrusaderDE.HUD_MPChatMessages.MPTeamColours[2];
            Check("Chat colour Blue", chat.R == 255 && chat.G == 0 && chat.B == 255, $"HUD_MPChatMessages.MPTeamColours[2]={chat.R},{chat.G},{chat.B}");

            TeamColors.ApplyToGame(null, "selftest revert", quiet: false);
            Color revertedSprite = Palette("defaultColours")[1];
            Color revertedUi = global::OnScreenText.Instance.MPTeamColours[1];
            Check("Revert to game colours", Near(revertedSprite, new Color(0.8f, 0.109f, 0.219f))
                && Near(revertedUi, new Color(0.76862746f, 0.007843138f, 0.007843138f)),
                $"defaultColours[1]={revertedSprite} ui[1]={revertedUi}");

            Config.Toml.ConfigLoader.ApplyTeamColorsFromFile("selftest re-apply");
            Check("Re-apply from file", Near(Palette("defaultColours")[1], RedOverride), $"defaultColours[1]={Palette("defaultColours")[1]}");
        }

        private static Color[] Palette(string field) =>
            (Color[])typeof(global::spriteLoader).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(global::spriteLoader.instance);

        private static bool Near(Color a, Color b) =>
            Math.Abs(a.r - b.r) < 2e-3f && Math.Abs(a.g - b.g) < 2e-3f && Math.Abs(a.b - b.b) < 2e-3f;

        // ===================================================
        // Speed
        // ===================================================

        private void CheckSpeedTableAndSpawn()
        {
            var units = GameUnitManagerAPI.Instance;
            Check("Speed table Catapult", units.GetDefaultSpeed(eChimps.CHIMP_TYPE_CATAPULT) == CatapultSpeed, $"GetDefaultSpeed={units.GetDefaultSpeed(eChimps.CHIMP_TYPE_CATAPULT)} (config {CatapultSpeed}, above the Script Extender's 6)");
            Check("Speed table Siege Tower", units.GetDefaultSpeed(eChimps.CHIMP_TYPE_SIEGE_TOWER) == SiegeTowerSpeed, $"GetDefaultSpeed={units.GetDefaultSpeed(eChimps.CHIMP_TYPE_SIEGE_TOWER)} (config {SiegeTowerSpeed}, game 3)");
            Check("Speed table Archer", units.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARCHER) == ArcherSpeed, $"GetDefaultSpeed={units.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARCHER)} (config {ArcherSpeed})");

            _catapult = Spawn(eChimps.CHIMP_TYPE_CATAPULT, 395, 400);
            _tower = Spawn(eChimps.CHIMP_TYPE_SIEGE_TOWER, 395, 406);
            Check("New Catapult speed", units.GetSpeed(_catapult) == CatapultSpeed, $"unit {_catapult} speed={units.GetSpeed(_catapult)}");
            Check("New Siege Tower speed", units.GetSpeed(_tower) == SiegeTowerSpeed, $"unit {_tower} speed={units.GetSpeed(_tower)}");

            _slowArcher = Spawn(eChimps.CHIMP_TYPE_ARCHER, 400, 412);
            units.SetDefaultSpeed(eChimps.CHIMP_TYPE_ARCHER, FastArcherSpeed);
            _fastArcher = Spawn(eChimps.CHIMP_TYPE_ARCHER, 400, 420);
            Check("Archers keep their creation speed", units.GetSpeed(_slowArcher) == ArcherSpeed && units.GetSpeed(_fastArcher) == FastArcherSpeed,
                $"slow {_slowArcher} speed={units.GetSpeed(_slowArcher)}, fast {_fastArcher} speed={units.GetSpeed(_fastArcher)}");

            var slowAt = units.GetCurrentLocalTilePosition(_slowArcher);
            var fastAt = units.GetCurrentLocalTilePosition(_fastArcher);
            _slowTargetX = slowAt.X + WalkTiles; _slowTargetY = slowAt.Y;
            _fastTargetX = fastAt.X + WalkTiles; _fastTargetY = fastAt.Y;
            units.MoveToTile(_slowArcher, _slowTargetX, _slowTargetY);
            units.MoveToTile(_fastArcher, _fastTargetX, _fastTargetY);
            _startTick = global::Director.instance.getSimTickCount();
            Note($"Archers ordered {WalkTiles} tiles: slow {slowAt.X},{slowAt.Y} -> {_slowTargetX},{_slowTargetY}; fast {fastAt.X},{fastAt.Y} -> {_fastTargetX},{_fastTargetY}; tick {_startTick}");
        }

        private int Spawn(eChimps type, int x, int y, int owner = 1)
        {
            int id = (int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, 8, type);
            if (id <= 0) throw new InvalidOperationException($"could not spawn {type} for player {owner} at {x},{y}");
            return id;
        }

        // ===================================================
        // Ranges
        // ===================================================

        private void StartRangeTest()
        {
            var units = GameUnitManagerAPI.Instance;
            foreach (int id in new[] { _catapult, _tower, _slowArcher, _fastArcher })
                if (id > 0) units.KillUnit(id);

            int attack = units.GetUnitAttackRange(RangeShooter);
            int engage = units.GetEngageRange(RangeShooter);
            int native = GameProjectileManagerAPI.Instance.GetAttackRangeTiles(GameUnitManagerAPI.GetDefaultAttackRangeProjectileType(RangeShooter));
            Check("Archer ranges reached the Script Extender",
                attack == XbowAttackRange && engage == XbowEngageRange * UnitRangesWorld,
                $"AttackRange {attack} tiles (file {XbowAttackRange}, game {native}); EngageRange {engage} world units (file {XbowEngageRange} tiles = {XbowEngageRange * UnitRangesWorld})");
            Note($"Players 1 and 2 allied: {GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(1, 2)}; teams {GamePlayerManagerAPI.Instance.GetPlayerTeam(1)} / {GamePlayerManagerAPI.Instance.GetPlayerTeam(2)}");
            NextRangePhase();
        }

        private const int UnitRangesWorld = 8;   // world units per tile (Config.Toml.Units.Properties.UnitRanges.WorldUnitsPerTile)

        private void NextRangePhase()
        {
            var units = GameUnitManagerAPI.Instance;
            if (_target > 0) units.KillUnit(_target);
            if (_shooter > 0) units.KillUnit(_shooter);
            _target = _shooter = 0;
            if (_phase >= 0) Note($"Range phase '{_rangePhases[_phase].Name}': {_rangePhases[_phase].Detail}");

            _phase++;
            if (_phase >= _rangePhases.Length)
            {
                ReportRanges();
                Finish();
                return;
            }
            var p = _rangePhases[_phase];
            if (p.Attack) units.SetUnitAttackRange(RangeShooter, XbowAttackRange); else units.ResetUnitAttackRange(RangeShooter);
            if (p.Engage) units.SetEngageRange(RangeShooter, XbowEngageRange * UnitRangesWorld); else units.ResetUnitEngageRange(RangeShooter);
            Note($"Range phase '{p.Name}': AttackRange now {units.GetUnitAttackRange(RangeShooter)} tiles, EngageRange {units.GetEngageRange(RangeShooter)} world units (observed game default {units.GetDefaultEngageRange(RangeShooter)})");
            _rung = 0;
            _shooter = Spawn(RangeShooter, RangeShooterX, RangeY);
            SpawnRungTarget();
        }

        private void SpawnRungTarget()
        {
            _target = Spawn(RangeTarget, RangeShooterX + RangeLadder[_rung], RangeY, owner: 2);
            _targetHp = GameUnitManagerAPI.Instance.GetCurrentHealth(_target);
            _rungTick = global::Director.instance.getSimTickCount();
        }

        private int _watchTick;
        private float _watchTime, _lastProgressNote;

        /// <summary>Fails fast when the simulation stops advancing (seen once on 2026-10-08: no tick for 15 minutes).</summary>
        private bool SimStalled(float now)
        {
            int tick = global::Director.instance.getSimTickCount();
            bool running = global::Director.instance.SimRunning;
            if (tick != _watchTick) { _watchTick = tick; _watchTime = now; }
            if (now - _lastProgressNote >= 60f)
            {
                _lastProgressNote = now;
                Note($"range progress: tick {tick}, phase {_phase}, rung {_rung}, shooter {Pos(_shooter)}, target {Pos(_target)}, sim running {running}");
            }
            if (now - _watchTime < 60f) return false;
            Check("Simulation keeps running", false, $"no simulation tick for 60 s at tick {tick} (phase {_phase}, rung {_rung}, sim running {running})");
            Finish();
            return true;
        }

        private void RangeTick()
        {
            var units = GameUnitManagerAPI.Instance;
            var p = _rangePhases[_phase];
            int tick = global::Director.instance.getSimTickCount();
            // Engagement signal: a live projectile fired by the shooter (direct), or the target losing health.
            _projectiles.Clear();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(_projectiles, SHCDESE.Interop.Enums.AliveState.IsAlive);
            bool fired = _projectiles.Any(id => GameProjectileManagerAPI.Instance.GetSourceUnit(id) == _shooter);
            bool damaged = units.GetCurrentHealth(_target) < _targetHp;
            if (fired || damaged)
            {
                var s = units.GetCurrentLocalTilePosition(_shooter);
                var t = units.GetCurrentLocalTilePosition(_target);
                double now = Math.Sqrt((double)(t.X - s.X) * (t.X - s.X) + (double)(t.Y - s.Y) * (t.Y - s.Y));
                p.HitAt = RangeLadder[_rung];
                p.Detail = $"first {(fired ? "shot" : "damage")} with the target placed {RangeLadder[_rung]} tiles away, after {tick - _rungTick} ticks (shooter at {s.X},{s.Y}, target at {t.X},{t.Y}, {now:0.#} tiles apart at that moment)";
                NextRangePhase();
                return;
            }
            if (tick - _rungTick < TicksPerRung) return;

            units.KillUnit(_target);
            _target = 0;
            if (++_rung >= RangeLadder.Length)
            {
                p.Detail = $"no damage at any distance down to {RangeLadder[RangeLadder.Length - 1]} tiles (shooter at {Pos(_shooter)})";
                NextRangePhase();
                return;
            }
            SpawnRungTarget();
        }

        private void ReportRanges()
        {
            RangePhase game = _rangePhases[0], attackOnly = _rangePhases[1], engageOnly = _rangePhases[2], both = _rangePhases[3];
            Check("Range baseline (game ranges)", game.HitAt > 0,
                game.HitAt > 0 ? $"engaged at {game.HitAt} tiles" : "no engagement at any distance, so the range phases cannot be judged (players not hostile, or the target is unreachable)");
            Note($"Archer, AttackRange {XbowAttackRange} only: {Rung(attackOnly)} (game: {Rung(game)})");
            Note($"Archer, EngageRange {XbowEngageRange} only: {Rung(engageOnly)} (game: {Rung(game)})");
            // Known limitation (SE #195): idle soldiers wake only within 400 world units (50 tiles), a check SE's
            // engage hook does not reach, so the larger ranges cannot make the idle Archer start sooner.
            if (both.HitAt > game.HitAt)
                Note($"NOTICE: AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange} now engage farther ({Rung(both)} vs game {Rung(game)}): " +
                     "the Script Extender's idle-wake limitation looks fixed; update the guide's range notes and the CHANGELOG.");
            else
                Note($"KNOWN LIMITATION (Script Extender #195): AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange}: {Rung(both)}, game {Rung(game)}; " +
                     "idle soldiers wake within 50 tiles regardless.");
            Check("Range phases measured", game.HitAt > 0 && attackOnly.HitAt > 0 && engageOnly.HitAt > 0 && both.HitAt > 0,
                $"every phase engaged at some distance (game {game.HitAt}, attack {attackOnly.HitAt}, engage {engageOnly.HitAt}, both {both.HitAt})");
        }

        private void StartCrossbowGuard()
        {
            var units = GameUnitManagerAPI.Instance;
            int engage = units.GetEngageRange(eChimps.CHIMP_TYPE_XBOWMAN);
            Check("Crossbowman EngageRange refused", engage != GuardEngageRange * UnitRangesWorld,
                $"Script Extender engage range {engage} world units (file {GuardEngageRange} tiles = {GuardEngageRange * UnitRangesWorld} must not reach it)");
            _guardUnit = Spawn(eChimps.CHIMP_TYPE_XBOWMAN, RangeShooterX, RangeY + 20);
            _guardEnemy = Spawn(RangeTarget, RangeShooterX + 16, RangeY + 20, owner: 2);
            _guardTick = global::Director.instance.getSimTickCount();
            Note($"Crossbowman {_guardUnit} spawned with an enemy Pikeman {_guardEnemy} 16 tiles away; running {GuardTicks} ticks (before the guard this crashed on the first update)");
            _stage = 4;
        }

        private void GuardTick(float now)
        {
            var units = GameUnitManagerAPI.Instance;
            int ticks = global::Director.instance.getSimTickCount() - _guardTick;
            if (ticks < GuardTicks) return;
            Check("Crossbowman runs without crashing", true,
                $"{ticks} ticks; crossbowman at {Pos(_guardUnit)}, enemy health {units.GetCurrentHealth(_guardEnemy)}");
            units.KillUnit(_guardUnit);
            units.KillUnit(_guardEnemy);
            StartRangeTest();
            _stage = 3; _deadline = now + 900;
            _watchTick = global::Director.instance.getSimTickCount(); _watchTime = now; _lastProgressNote = now;
        }

        private static string Rung(RangePhase p) => p.HitAt > 0 ? $"engaged at {p.HitAt} tiles" : "no engagement";

        private static bool At(int unitId, int x, int y)
        {
            var p = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(unitId);
            return p.X == x && p.Y == y;
        }

        private static string Pos(int unitId)
        {
            if (unitId <= 0) return "-";
            var p = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(unitId);
            return $"{p.X},{p.Y}";
        }

        // ===================================================
        // Finish
        // ===================================================

        private void Finish()
        {
            if (_done) return;
            _done = true;
            string verdict = _failures == 0 ? "PASS" : "FAIL";
            Note($"END {verdict} ({_failures} failure(s))");
            try { File.WriteAllText(_result, verdict + Environment.NewLine + _details); }
            catch (Exception ex) { Plugin.Logger.LogError("[SelfTest] could not write result: " + ex.Message); }
            Application.Quit();
        }
    }

    /// <summary>Per-frame driver on a hidden object that survives scene loads (the game replaces scenes at startup).</summary>
    internal sealed class SelfTestDriver : MonoBehaviour
    {
        private static Action _tick;

        internal static void Run(Action tick)
        {
            _tick = tick;
            var host = new GameObject("CrusaderDETweaker.SelfTest") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SelfTestDriver>();
        }

        private void Update() => _tick?.Invoke();
    }
}
