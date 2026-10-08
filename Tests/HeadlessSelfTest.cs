// Tests/HeadlessSelfTest.cs
//
// PURPOSE: Opt-in in-game integration test for features the on-load suites cannot reach (they use mocks):
//          team colours on the real game palettes / UI tables, and Speed values above the Script
//          Extender's 6 on real units. Started only by scripts/test_headless.ps1:
//            Stronghold Crusader Definitive Edition.exe -batchmode ... -cdt-selftest "<result.txt>"
//          Without that flag (every normal launch) Create() returns at once and nothing runs.
//
// WHAT IT DOES (windowless; the runner sets the config values below and restores the files after):
//   0. Limits stage, first on the editor map (Tests/HeadlessSelfTestLimits.cs): the unit limit (GitHub #3) and the
//      advanced gameplay options' master switches (GitLab #1); its real-skirmish part runs last (after step 5).
//      With -cdt-selftest-quick only those two run.
//   1. Waits for the main menu, notes whether the menu-time team-colour apply already ran.
//   2. Creates a disposable editor map (nothing is saved), checks the speed table and the colours:
//      sprite palettes (default / lord / jester recoloured, knight-horse untouched), the game's own
//      lookup spriteLoader.GetGMMaterial, both UI tables, then revert (-1) and re-apply.
//   3. Spawns a Catapult and a Siege Tower (speed fields must equal the configured 8 / 1), then two
//      Archers 10 tiles from a target: one created while the table says 8, one after setting the table
//      to 1. Both walk the same 10 tiles; the Speed 8 archer must take clearly longer (ticks counted).
//   4. Knight run speed: with KnightRunSpeedBonus 2 (game), 0 and 8, a Knight walks 20 tiles on a move order;
//      the times must match (the game writes the bonus only in the knight's AI state 101, not on a move order)
//      and the knight's speed-bonus field is sampled every frame. Measured 2026-10-08: 311 / 315 / 312 ticks,
//      field 0 throughout. (Higher = faster while the bonus applies: the step function moves bonus + 1
//      sub-steps per step, RVA 0x1857B3.)
//   4b. Former crash units (2.9.1): the Crossbowman EngageRange the runner writes must be in effect through
//      EngageDistancePatch (not SE's engage hook, which crashed it), and a Crossbowman, a Bedouin Heavy Camel and
//      an Arabian Ballista, each with EngageRange 80, must run 300 ticks next to an enemy.
//   5. Ranges: checks the Archer AttackRange / EngageRange from the Units file are in effect, then measures six
//      phases (Archer: game ranges, AttackRange 80 only, EngageRange 80 only, both; Crossbowman: game, both): an
//      idle shooter (player 1) and an enemy Pikeman (player 2) are placed 96, 84, ... 16 tiles apart, closer
//      each 400 ticks, until the shooter fires (a live projectile from it) or the Pikeman loses health; then up
//      to 300 more ticks for damage. The first shot distance is the effective engage distance. Before 2.9.1 it
//      was 48 tiles in every Archer phase (idle units woke at 400 world units, SE #195).
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
using CrusaderDETweaker.Config.Toml.Units.Properties;
using SHCDESE.API;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        internal const string Flag = "-cdt-selftest";
        // Development only (test_headless.ps1 -Quick): run only the limits stages, editor map and skirmish (HeadlessSelfTestLimits.cs).
        internal const string QuickFlag = "-cdt-selftest-quick";
        private readonly bool _quick = Array.IndexOf(Environment.GetCommandLineArgs(), QuickFlag) >= 0;

        // Written into the configs by scripts/test_headless.ps1.
        internal const int CatapultSpeed = 8, SiegeTowerSpeed = 1, ArcherSpeed = 8, FastArcherSpeed = 1;
        private static readonly Color RedOverride = new Color(0f, 1f, 0f);    // Red = [0, 255, 0]
        private static readonly Color BlueOverride = new Color(1f, 0f, 1f);   // Blue = "#FF00FF"
        private const int WalkTiles = 10;

        // Archer ranges written by scripts/test_headless.ps1 (tiles; the game's AttackRange is 54, engage 50).
        internal const int XbowAttackRange = 80, XbowEngageRange = 80;
        private static readonly int[] RangeLadder = { 96, 84, 72, 64, 56, 48, 42, 36, 32, 28, 24, 20, 16 };
        private const int TicksPerRung = 400, DamageWaitTicks = 300, RangeShooterX = 340, RangeY = 400;
        private readonly System.Collections.Generic.List<int> _projectiles = new System.Collections.Generic.List<int>();
        private const eChimps RangeTarget = eChimps.CHIMP_TYPE_PIKEMAN;

        private sealed class RangePhase
        {
            internal string Name;
            internal eChimps Shooter = eChimps.CHIMP_TYPE_ARCHER;
            internal bool Attack, Engage;
            internal int HitAt = -1, ShotTick;
            internal bool Damaged;
            internal string Detail = "not run";
        }

        private readonly RangePhase[] _rangePhases =
        {
            new RangePhase { Name = "Archer, game ranges (control)" },
            new RangePhase { Name = $"Archer, AttackRange {XbowAttackRange} only", Attack = true },
            new RangePhase { Name = $"Archer, EngageRange {XbowEngageRange} only", Engage = true },
            new RangePhase { Name = $"Archer, AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange}", Attack = true, Engage = true },
            new RangePhase { Name = "Crossbowman, game ranges (control)", Shooter = eChimps.CHIMP_TYPE_XBOWMAN },
            new RangePhase { Name = $"Crossbowman, AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange}", Shooter = eChimps.CHIMP_TYPE_XBOWMAN, Attack = true, Engage = true },
        };
        private int _phase = -1, _rung, _shooter, _target, _targetHp, _rungTick;
        private int _catapult, _tower;
        // Former crash units (2.9.1): the runner writes this Crossbowman EngageRange; the other two are set here.
        internal const int GuardEngageRange = 80;
        private const int GuardTicks = 300;
        private static readonly eChimps[] GuardUnits = { eChimps.CHIMP_TYPE_XBOWMAN, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, eChimps.CHIMP_TYPE_ARAB_BALLISTA };
        private readonly System.Collections.Generic.List<int> _guardSpawned = new System.Collections.Generic.List<int>();
        private int _guardTick;

        // KnightRunSpeedBonus is an immediate in the knight's update (CrusaderDE.dll RVA 0x1496B5, game 2.8.2) that
        // the update writes into the unit's speed-bonus field (+0x916). The movement step (RVA 0x184203) waits
        // until more than Speed (+0x9A2) + that field + one more field (+0x74C) ticks have passed since the last step.
        // The knight update (jump table at RVA 0x14A330) writes the bonus only in AI state 101 (unit +0x918).
        private const int KnightWalkTiles = 20, KnightChargeTiles = 10, KnightPhaseTicks = 1500;
        private const int SpeedToBonus = 0x9A2 - 0x916, SpeedToTickCounter = 0x9AC - 0x9A2;

        private sealed class KnightPhase
        {
            internal ushort Bonus;
            internal bool Charge;
            internal int Ticks = -1, CounterStart, TickStart;
            internal string Detail = "not run";
            internal readonly System.Collections.Generic.SortedDictionary<int, int> Seen = new System.Collections.Generic.SortedDictionary<int, int>();
        }

        private readonly KnightPhase[] _knightPhases =
        {
            new KnightPhase { Bonus = 2 }, new KnightPhase { Bonus = 0 }, new KnightPhase { Bonus = 8 },
            // Charge phases (Charge = true) need a knight that engages: on 2026-10-08 an idle knight did not attack a
            // pinned enemy Pikeman 10 tiles away in the editor map within 1500 ticks, so they are not run.
        };
        private int _kPhase = -1, _knight, _kTarget, _kTargetHp, _kTargetX, _kTargetY, _speedOffset;
        private ushort _knightBonusGame;


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

                if (_stage == LimitsSkirmishStart) { StartLimitsSkirmish(now); return; }   // outside the lock: the game swaps simulation threads

                lock (_engineLock)
                {
                    if (_stage > LimitsSkirmishStart) { LimitsSkirmishTick(now); return; }
                    if (_stage == 1)
                    {
                        if (!vm.IsMapEditorMode || !global::Director.instance.SimRunning) return;
                        _stage = LimitsStage; _deadline = now + 900;   // unit limit + gameplay options first (HeadlessSelfTestLimits.cs)
                        return;
                    }
                    if (_stage == LimitsStage)
                    {
                        if (!LimitsTick(now) || _done) return;
                        if (_quick) { Note("Quick run (-cdt-selftest-quick): only the limits stages run"); BeginLimitsSkirmish(); return; }
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
                        StartKnightTest();
                        _stage = 5; _deadline = now + 600;
                        return;
                    }
                    if (_stage == 5)
                    {
                        _next = now;   // sample the knight's speed-bonus field every frame
                        if (KnightTick())
                        {
                            StartCrossbowGuard();
                            _deadline = now + 180;
                        }
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
                if (_stage == LimitsStage || _stage > LimitsSkirmishStart) Note($"progress: limits step {_lStep}, skirmish step {_skStep}, spearman phase {_spear}");
                if (_stage == 2) Note($"progress: slow archer {Pos(_slowArcher)} ticks={_slowTicks}, fast archer {Pos(_fastArcher)} ticks={_fastTicks}");
                if (_stage == 5) Note($"progress: knight phase {_kPhase}, knight {Pos(_knight)}; " + string.Join("; ", _knightPhases.Select(KnightName).Zip(_knightPhases, (n, p) => n + ": " + p.Detail)));
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
        // Knight run speed
        // ===================================================

        private static string KnightName(KnightPhase p) => $"{(p.Charge ? "charge" : "move")}, KnightRunSpeedBonus {p.Bonus}";

        private void StartKnightTest()
        {
            var units = GameUnitManagerAPI.Instance;
            // The speed-stage archers would shoot the charge target.
            foreach (int id in new[] { _catapult, _tower, _slowArcher, _fastArcher })
                if (id > 0) units.KillUnit(id);
            _catapult = _tower = _slowArcher = _fastArcher = 0;

            var prop = Plugin.GlobalsApi?.KnightRunSpeedBonus ?? throw new InvalidOperationException("the Script Extender found no KnightRunSpeedBonus");
            _knightBonusGame = prop.GetValue();
            Note($"Knight: KnightRunSpeedBonus in the game {_knightBonusGame}, Speed {units.GetDefaultSpeed(eChimps.CHIMP_TYPE_KNIGHT)}");
            NextKnightPhase();
        }

        /// <summary>Ends the current knight phase and starts the next; true when all phases are done.</summary>
        private unsafe bool NextKnightPhase()
        {
            var units = GameUnitManagerAPI.Instance;
            var prop = Plugin.GlobalsApi.KnightRunSpeedBonus;
            if (_kTarget > 0) units.KillUnit(_kTarget);
            if (_knight > 0) units.KillUnit(_knight);
            _kTarget = _knight = 0;
            if (_kPhase >= 0) Note($"Knight phase '{KnightName(_knightPhases[_kPhase])}': {_knightPhases[_kPhase].Detail}");

            if (++_kPhase >= _knightPhases.Length)
            {
                prop.SetValue(_knightBonusGame);
                ReportKnight();
                return true;
            }
            var p = _knightPhases[_kPhase];
            prop.SetValue(p.Bonus);
            _knight = Spawn(eChimps.CHIMP_TYPE_KNIGHT, 350, 400 + 4 * _kPhase);   // fresh ground each phase
            if (_speedOffset == 0)
            {
                if (!units.TryGetUnitById(_knight, out GameUnit* u)) throw new InvalidOperationException("knight not found");
                _speedOffset = (int)((byte*)&u->r_CurrentSpeed2 - (byte*)u);
                Note($"Unit layout: speed (game +0x9A2) = SE r_CurrentSpeed2 at +0x{_speedOffset:X}; speed-bonus field at +0x{_speedOffset - SpeedToBonus:X} " +
                     $"(SE r_SpeedBonus at +0x{(int)((byte*)&u->r_SpeedBonus - (byte*)u):X})");
            }
            var at = units.GetCurrentLocalTilePosition(_knight);
            if (p.Charge)
            {
                _kTarget = Spawn(RangeTarget, at.X + KnightChargeTiles, at.Y, owner: 2);
                _kTargetHp = units.GetCurrentHealth(_kTarget);
                // Pin the target so the time to contact is the knight's own movement.
                units.SetSpeed(_kTarget, 30000);
                WriteUnitField16(_kTarget, _speedOffset, 30000);
            }
            else
            {
                _kTargetX = at.X + KnightWalkTiles; _kTargetY = at.Y;
                units.MoveToTile(_knight, _kTargetX, _kTargetY);
            }
            p.TickStart = global::Director.instance.getSimTickCount();
            p.CounterStart = ReadUnitField32(_knight, _speedOffset + SpeedToTickCounter);
            Note($"Knight phase '{KnightName(p)}': KnightRunSpeedBonus now {prop.GetValue()}; knight {_knight} at {at.X},{at.Y}" +
                 (p.Charge ? $", enemy Pikeman {_kTarget} {KnightChargeTiles} tiles east (pinned)" : $", ordered {KnightWalkTiles} tiles east"));
            return false;
        }

        /// <summary>Samples the knight's speed-bonus field; true when all knight phases are done.</summary>
        private bool KnightTick()
        {
            var units = GameUnitManagerAPI.Instance;
            var p = _knightPhases[_kPhase];
            int tick = global::Director.instance.getSimTickCount();
            int bonus = ReadUnitField16(_knight, _speedOffset - SpeedToBonus);
            p.Seen.TryGetValue(bonus, out int n);
            p.Seen[bonus] = n + 1;

            bool done = p.Charge ? units.GetCurrentHealth(_kTarget) < _kTargetHp : At(_knight, _kTargetX, _kTargetY);
            if (done)
            {
                p.Ticks = tick - p.TickStart;
                int counter = ReadUnitField32(_knight, _speedOffset + SpeedToTickCounter) - p.CounterStart;
                p.Detail = $"{(p.Charge ? "first hit on the enemy" : "arrived")} after {p.Ticks} ticks; speed-bonus field values seen (samples): {Seen(p)}; " +
                           $"unit step counter (+0x9AC) advanced {counter}";
                return NextKnightPhase();
            }
            if (tick - p.TickStart > KnightPhaseTicks)
            {
                p.Detail = $"no {(p.Charge ? "hit" : "arrival")} within {KnightPhaseTicks} ticks (knight at {Pos(_knight)}); speed-bonus field values seen (samples): {Seen(p)}";
                return NextKnightPhase();
            }
            return false;
        }

        private void ReportKnight()
        {
            KnightPhase game = _knightPhases[0], zero = _knightPhases[1], eight = _knightPhases[2];
            bool same = zero.Ticks > 0 && game.Ticks > 0 && eight.Ticks > 0 && Math.Max(zero.Ticks, Math.Max(game.Ticks, eight.Ticks)) <= Math.Min(zero.Ticks, Math.Min(game.Ticks, eight.Ticks)) * 1.05;
            Check("KnightRunSpeedBonus leaves move orders unchanged", same && zero.Seen.Keys.All(v => v == 0) && eight.Seen.Keys.All(v => v == 0),
                $"{KnightWalkTiles} tiles: bonus 0 {T(zero)}, game {_knightBonusGame} {T(game)}, bonus 8 {T(eight)}; speed-bonus field seen: {Seen(eight)}");
            if (_knightPhases.Length < 6) return;
            KnightPhase cGame = _knightPhases[3], cZero = _knightPhases[4], cEight = _knightPhases[5];
            Note($"KnightRunSpeedBonus on a charge ({KnightChargeTiles} tiles to a pinned enemy, until the first hit): bonus 0 {T(cZero)}, game {_knightBonusGame} {T(cGame)}, bonus 8 {T(cEight)}");
        }

        private static string T(KnightPhase p) => p.Ticks > 0 ? p.Ticks + " ticks" : "no result";

        private static string Seen(KnightPhase p) => string.Join(", ", p.Seen.Select(kv => $"{kv.Key} x{kv.Value}"));

        private static unsafe int ReadUnitField16(int unitId, int offset)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u)) return int.MinValue;
            return *(short*)((byte*)u + offset);
        }

        private static unsafe int ReadUnitField32(int unitId, int offset)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u)) return int.MinValue;
            return *(int*)((byte*)u + offset);
        }

        private static unsafe void WriteUnitField16(int unitId, int offset, short value)
        {
            if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u)) *(short*)((byte*)u + offset) = value;
        }

        // ===================================================
        // Ranges
        // ===================================================

        private void StartRangeTest()
        {
            var units = GameUnitManagerAPI.Instance;
            foreach (int id in new[] { _catapult, _tower, _slowArcher, _fastArcher })
                if (id > 0) units.KillUnit(id);

            const eChimps archer = eChimps.CHIMP_TYPE_ARCHER;
            int attack = units.GetUnitAttackRange(archer);
            EngageDistancePatch.TryGetCurrentWorld(archer, out int engage);
            EngageDistancePatch.TryGetGameTiles(archer, out int gameEngage);
            Check("Archer ranges from the Units file in effect",
                attack == XbowAttackRange && engage == XbowEngageRange * UnitRangesWorld,
                $"AttackRange {attack} tiles (file {XbowAttackRange}, game {UnitRanges.GameAttackTiles(archer)}); engage distance {engage} world units " +
                $"(file {XbowEngageRange} tiles = {XbowEngageRange * UnitRangesWorld}, game {gameEngage} tiles); constants {EngageDistancePatch.Describe(archer)}");
            Note($"Players 1 and 2 allied: {GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(1, 2)}; teams {GamePlayerManagerAPI.Instance.GetPlayerTeam(1)} / {GamePlayerManagerAPI.Instance.GetPlayerTeam(2)}");
            NextRangePhase();
        }

        private const int UnitRangesWorld = UnitRanges.WorldUnitsPerTile;

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
                foreach (eChimps t in new[] { eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_XBOWMAN }) { UnitRanges.ResetAttackRange(t); EngageDistancePatch.ClearEngage(t); }
                ReportRanges();
                BeginLimitsSkirmish();   // last: a real custom skirmish (HeadlessSelfTestLimits.cs)
                return;
            }
            var p = _rangePhases[_phase];
            // The same paths the Units file uses (AttackRange through SE, engage distance through EngageDistancePatch).
            foreach (eChimps t in new[] { eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_XBOWMAN })
            {
                bool on = t == p.Shooter;
                if (on && p.Attack) UnitRanges.ApplyAttackRange("AttackRange", t, XbowAttackRange); else UnitRanges.ResetAttackRange(t);
                if (on && p.Engage) EngageDistancePatch.SetEngageTiles(t, XbowEngageRange); else EngageDistancePatch.ClearEngage(t);
            }
            EngageDistancePatch.TryGetCurrentWorld(p.Shooter, out int engage);
            Note($"Range phase '{p.Name}': AttackRange now {units.GetUnitAttackRange(p.Shooter)} tiles, engage distance {engage} world units; constants {EngageDistancePatch.Describe(p.Shooter)}");
            _rung = 0;
            _shooter = Spawn(p.Shooter, RangeShooterX, RangeY);
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
            bool damaged = units.GetCurrentHealth(_target) < _targetHp;
            if (p.HitAt > 0)
            {
                // After the first shot: does it reach? Wait for damage on the same rung.
                if (damaged || tick - p.ShotTick > DamageWaitTicks)
                {
                    p.Damaged = damaged;
                    p.Detail += damaged ? $"; target damaged {tick - p.ShotTick} ticks after the first shot (target at {Pos(_target)})"
                                        : $"; no damage within {DamageWaitTicks} ticks of the first shot (target at {Pos(_target)})";
                    NextRangePhase();
                }
                return;
            }
            // Engagement signal: a live projectile fired by the shooter (direct), or the target losing health.
            _projectiles.Clear();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(_projectiles, SHCDESE.Interop.Enums.AliveState.IsAlive);
            bool fired = _projectiles.Any(id => GameProjectileManagerAPI.Instance.GetSourceUnit(id) == _shooter);
            if (fired || damaged)
            {
                var s = units.GetCurrentLocalTilePosition(_shooter);
                var t = units.GetCurrentLocalTilePosition(_target);
                double now = Math.Sqrt((double)(t.X - s.X) * (t.X - s.X) + (double)(t.Y - s.Y) * (t.Y - s.Y));
                p.HitAt = RangeLadder[_rung];
                p.ShotTick = tick;
                p.Detail = $"first {(fired ? "shot" : "damage")} with the target placed {RangeLadder[_rung]} tiles away, after {tick - _rungTick} ticks (shooter at {s.X},{s.Y}, target at {t.X},{t.Y}, {now:0.#} tiles apart at that moment)";
                if (damaged) { p.Damaged = true; NextRangePhase(); }
                return;
            }
            if (tick - _rungTick < TicksPerRung) return;

            units.KillUnit(_target);
            _target = 0;
            if (++_rung >= RangeLadder.Length)
            {
                p.Detail = $"no shot at any distance down to {RangeLadder[RangeLadder.Length - 1]} tiles (shooter at {Pos(_shooter)})";
                NextRangePhase();
                return;
            }
            SpawnRungTarget();
        }

        private void ReportRanges()
        {
            RangePhase game = _rangePhases[0], attackOnly = _rangePhases[1], engageOnly = _rangePhases[2], both = _rangePhases[3];
            RangePhase xGame = _rangePhases[4], xBoth = _rangePhases[5];
            Check("Range baseline (game ranges)", game.HitAt > 0,
                game.HitAt > 0 ? $"engaged at {game.HitAt} tiles" : "no engagement at any distance, so the range phases cannot be judged (players not hostile, or the target is unreachable)");
            Note($"Archer, EngageRange {XbowEngageRange} only: {Rung(engageOnly)} (game: {Rung(game)})");
            // Before 2.9.1 every Archer phase engaged at 48 tiles (idle units woke at 400 world units, SE #195).
            Check($"Archer AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange} engages farther than the game",
                game.HitAt > 0 && both.HitAt > game.HitAt && both.Damaged, $"{Rung(both)} vs game {Rung(game)}");
            Check($"Archer AttackRange {XbowAttackRange} alone engages farther than the game (engage distance follows it)",
                game.HitAt > 0 && attackOnly.HitAt > game.HitAt && attackOnly.Damaged, $"{Rung(attackOnly)} vs game {Rung(game)}");
            Check($"Crossbowman AttackRange {XbowAttackRange} + EngageRange {XbowEngageRange} engages farther than the game",
                xGame.HitAt > 0 && xBoth.HitAt > xGame.HitAt && xBoth.Damaged, $"{Rung(xBoth)} vs game {Rung(xGame)}");
        }

        private void StartCrossbowGuard()
        {
            var units = GameUnitManagerAPI.Instance;
            EngageDistancePatch.TryGetCurrentWorld(eChimps.CHIMP_TYPE_XBOWMAN, out int engage);
            Check("Crossbowman EngageRange from the Units file in effect (game constants, not the Script Extender's hook)",
                engage == GuardEngageRange * UnitRangesWorld && units.GetEngageRange(eChimps.CHIMP_TYPE_XBOWMAN) != GuardEngageRange * UnitRangesWorld,
                $"engage distance {engage} world units (file {GuardEngageRange} tiles = {GuardEngageRange * UnitRangesWorld}); Script Extender override {units.GetEngageRange(eChimps.CHIMP_TYPE_XBOWMAN)}; constants {EngageDistancePatch.Describe(eChimps.CHIMP_TYPE_XBOWMAN)}");
            Note("EngageRange units (game constants found): " + string.Join(", ", EngageDistancePatch.SupportedUnits.Select(u =>
                EngageDistancePatch.TryGetGameTiles(u, out int tiles) ? $"{u} {tiles} tiles" : u.ToString())));
            int y = RangeY + 20;
            foreach (eChimps t in GuardUnits)
            {
                if (t != eChimps.CHIMP_TYPE_XBOWMAN) EngageDistancePatch.SetEngageTiles(t, GuardEngageRange);
                EngageDistancePatch.TryGetCurrentWorld(t, out int now);
                Check($"{t} EngageRange {GuardEngageRange} in effect", now == GuardEngageRange * UnitRangesWorld,
                    $"engage distance {now} world units; constants {EngageDistancePatch.Describe(t)}");
                _guardSpawned.Add(Spawn(t, RangeShooterX, y));
                _guardSpawned.Add(Spawn(RangeTarget, RangeShooterX + 16, y, owner: 2));
                y += 12;
            }
            _guardTick = global::Director.instance.getSimTickCount();
            Note($"Crossbowman, Bedouin Heavy Camel and Arabian Ballista spawned with EngageRange {GuardEngageRange}, each 16 tiles from an enemy Pikeman; running {GuardTicks} ticks " +
                 "(with the Script Extender's engage hook the first two crashed on their first update)");
            _stage = 4;
        }

        private void GuardTick(float now)
        {
            var units = GameUnitManagerAPI.Instance;
            int ticks = global::Director.instance.getSimTickCount() - _guardTick;
            if (ticks < GuardTicks) return;
            var detail = new StringBuilder($"{ticks} ticks");
            for (int i = 0; i + 1 < _guardSpawned.Count; i += 2)
                detail.Append($"; {GuardUnits[i / 2]} at {Pos(_guardSpawned[i])}, its enemy's health {units.GetCurrentHealth(_guardSpawned[i + 1])}");
            Check("Former crash units run with an EngageRange", true, detail.ToString());
            foreach (int id in _guardSpawned) units.KillUnit(id);
            foreach (eChimps t in GuardUnits) EngageDistancePatch.ClearEngage(t);
            StartRangeTest();
            _stage = 3; _deadline = now + 1500;
            _watchTick = global::Director.instance.getSimTickCount(); _watchTime = now; _lastProgressNote = now;
        }

        private static string Rung(RangePhase p) => p.HitAt > 0 ? $"engaged at {p.HitAt} tiles{(p.Damaged ? " (target damaged)" : " (no damage)")}" : "no engagement";

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
