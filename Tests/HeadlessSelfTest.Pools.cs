// Tests/HeadlessSelfTest.Pools.cs
//
// PURPOSE: Self-test stages on the editor map for the building limit (GitLab #9, Config/Core/BuildingLimit.cs) and the
//          siege engine animations with very many units (GitLab #10: sprite pool, Config/Core/SpritePool.cs, and the
//          projectile pool). Runs after the limits editor stage, or alone with "-cdt-selftest-only pools"
//          (test_headless.ps1 -Only pools).
//
// WHAT IT DOES:
//   A. Building limit. Player 3 gets gold; a Well for player 3 is clicked through the game's own click path
//      (EngineInterface.PlaceMapperItem, a hover pass then a click, as EditorDirector does) onto a tile just outside the
//      map, with the tile validator overridden for the click (Script Extender TileManager placement override), so the only refusal left is
//      the building limit check, read from the blocked flag (TileManager.IsPlacementBlocked) right after the click. For a
//      click the test sets the two counters the check reads (player 3's building count, manager +4 + 4 * player, and the
//      free records, manager +0x54) under the engine lock; the game recounts both at the next tick (RVA 0xC60F0).
//      Cases: the editor's mode 0 (campaign rule: no per-player share; free 19 refused, 20 accepted) and the skirmish /
//      trail mode 0x63 set for the click (the game's share 4000 / players - 1, then BuildingLimit 32000, 1000 and -1, and
//      free 19 with 32000). Nothing may get built (the queued build checks the tile again, without the override).
//   B. Engines and animations. UnitLimit 10000 for the stage (put back after). The camera is centred on the map and a
//      probe grid finds the tiles the renderer draws (a unit is drawn when GameMap has a Chimp for its id). In each
//      phase a fresh Archer, Ballista and Mangonel (each with a pinned enemy) and a walking Spearman are watched for
//      WindowTicks game ticks: native animation fields (+0x00, +0x44), the frame the screen shows (GameMap Chimp file /
//      image / layers), shots (projectiles from the unit), and rendered frames. Phases:
//        E0 few units, old code (sprite growth off);
//        C2 SpritePool's growth on, with the sprite pool emptied by the test and NewUnits new units spawned;
//        then waves of units on the drawn tiles up to WaveTarget units (pool use per wave), and
//        E1 with WaveTarget units, old code;
//        a battle: BattleEnemies enemy Pikemen next to the army's shooters, projectile pool use (RVA 0x3668E38);
//        C1 last and only with -Only pools: old code with the pool emptied the same way (the game's "Pool Empty" path).
//           Measured 2026-10-10: 6 processTestMap exceptions, after which the game stops (each exception leaks one of
//           the 6 render buffers, MemoryBuffers: Director.Update never returns it), so nothing can run after it.
//      The camera is put back on the map centre every frame: the windowless game scrolls it by itself.
//      Units are removed with the Script Extender's DeleteUnitSafe (slots free at once); sprites the test took go back.
//
// IMPORTANT FOR AI AGENTS:
// - The RVAs checked here are the ones in Config/Core/BuildingLimit.cs (game 2.8.2); a mismatch turns the exact-boundary
//   cases into SKIP notes.
// - Durations are in game ticks (Script Extender CurrentGameTick): with thousands of units drawn the game runs far fewer
//   game ticks than Director.getSimTickCount counts.
// - A check that cannot be measured is a SKIP note, never a PASS.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CrusaderDETweaker.Config.Core;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        private const int PoolsStage = 100;
        internal const int BuildingLimitConfigured = 32000;   // ["Building Limit"] BuildingLimit, written by scripts/test_headless.ps1
        // The editor map is 160 x 160 tiles, game coordinates 320-479 (GameMap.mapGameTileToTilemapCoord: (800 - size) / 2).
        // The click tile is outside the 160-tile editor map (game coordinates 320-479): the build an accepted click queues fails
        // its own tile check (a Well placed by a click crashed the windowless game). No keep for player 3: with one, the range
        // stage's Archers with AttackRange 80 later fired at something 54-80 tiles away (runs 2026-10-10 21:07 and 21:25).
        private const int BuildPlayer = 3, ClickX = 300, ClickY = 440, BuildSettleTicks = 10;
        private const ulong MgrOwnOffset = 4, MgrFreeOffset = 0x54;
        // Code that proves the counters / slots read by the check (game 2.8.2): free records 0x91BCA "mov ecx, [rip]",
        // lobby slots 0x91AFB "cmp [rip], -1" and 0x91B07 "cmp [rip], esi".
        private const ulong FreeReadRva = 0x91BCA, SlotReadRva = 0x91AFB, SlotRead2Rva = 0x91B07, SlotRva = 0x8574BD0, Slot2Rva = 0x8574C48;
        // Diagnostics read at the click (game 2.8.2): blocked flag (0x91C1D), block reason, pending build mapper (0x91D6D).
        private const ulong BlockedRva = 0x60AD4AC, BlockReasonRva = 0x60AD4B4, PendingMapperRva = 0x86C1334;

        private const int MapCentre = 400, ProbeFrom = 326, ProbeTo = 446, ProbeStep = 6;
        private const int WaveTarget = 8500, Wave = 750, NewUnits = 40, WindowTicks = 300, EngineTargetTiles = 12;
        private const int BattleEnemies = 1000, BattleTicks = 150, BattleOffsetTiles = 14, ReservedProjectileSlots = 25;
        private const float PhaseWallSeconds = 420f;

        private sealed class BuildCase
        {
            internal string Name;
            internal int Mode;                 // 0 = the editor's own mode, 0x63 = skirmish / trail rule for the click
            internal long Limit = long.MinValue;   // BuildingLimit to apply first (MinValue = leave as is)
            internal int OwnFromShare = int.MinValue;   // own count = share(limit) + this (MinValue = real count)
            internal int OwnFixed = -1, Free = -1;
            internal bool Expect, Skipped, Accepted;
            internal int Constant;
            internal int Before = -1, After = -1, Own = -1, Tick;
            internal string Detail = "not run", Click = "";
        }

        private BuildCase[] _buildCases;
        private int _bCase = -1, _players, _pStep;
        private ulong _bMgr;
        private bool _bExact;

        private sealed class Watch
        {
            internal int Unit, Target;
            internal string LastNative, LastScreen;
            internal int NativeChanges, ScreenChanges, ScreenMissing, Samples, Shots;
            internal readonly HashSet<int> ShotIds = new HashSet<int>();
        }

        private sealed class EnginePhase
        {
            internal string Name;
            internal bool Fix, Drain;
            internal int GameTick0, Ticks, Frames, ExceptionsBefore, EmptyBefore, AddedBefore, Units, NativeSamples;
            internal volatile bool Ended;
            internal float Start, LastTickTime, Seconds;
            internal int LastTick = -1;
            internal readonly Dictionary<string, Watch> Watches = new Dictionary<string, Watch>();
            internal int Exceptions, Empty, Added, NewDrawn, NewSpawned, Taken, DrainAt = -1;
            internal readonly List<int> NewIds = new List<int>();
            internal string Detail = "not run";
        }

        private readonly EnginePhase _e0 = new EnginePhase { Name = "E0 few units, old code" };
        private readonly EnginePhase _c1 = new EnginePhase { Name = "C1 old code, sprite pool emptied", Drain = true };
        private readonly EnginePhase _c2 = new EnginePhase { Name = "C2 new code (pool grows), sprite pool emptied", Drain = true, Fix = true };
        private readonly EnginePhase _e1 = new EnginePhase { Name = "E1 " + WaveTarget + " units, old code" };
        private EnginePhase _ePhaseNow;

        private int _unitLimitBefore, _baselineUnits, _walkFlip, _waveFrame;
        private readonly List<int> _poolUnits = new List<int>(), _phaseUnits = new List<int>();
        private readonly List<(int X, int Y)> _drawnTiles = new List<(int, int)>();
        private readonly List<GameObject> _taken = new List<GameObject>();
        private int _poolEmptyLogs, _renderExceptions;
        private string _firstRenderException;
        private bool _logHooked;
        private int _peakUsed, _peakDrawn;
        private float _poolNoteTime, _stepStart;

        // ===================================================
        // Stage driver
        // ===================================================

        private void StartPools(float now)
        {
            _stage = PoolsStage;
            _pStep = 0;
            _deadline = now + 3000;
            _next = now;
            _lWatchTick = global::Director.instance.getSimTickCount(); _lWatchTime = now;
            Note("Pools: building limit (GitLab #9), siege engine animations with many units (GitLab #10)");
        }

        // Real game ticks: counted on the Script Extender's OnTick (raised once per native tick on the simulation thread).
        // Its CurrentGameTick is Director.getSimTickCount, which also counts the ticks the game skips while all 6 render
        // buffers are full (EngineInterface.run returns without DLL_RunTick), so it runs ahead under load.
        private int _nativeTicks;
        private bool _tickHooked, _battleActive;
        private int GameTick() => Volatile.Read(ref _nativeTicks);

        private void OnNativeTick(int simTick)
        {
            Interlocked.Increment(ref _nativeTicks);
            try
            {
                var p = _ePhaseNow;
                bool phase = p != null && p.Frames >= 0 && !p.Ended;
                if (!phase && !_battleActive) return;
                var projectiles = new List<int>();
                GameProjectileManagerAPI.Instance.GetAllProjectiles(projectiles, AliveState.IsAlive);
                if (_battleActive)
                {
                    _battleTickSamples++;
                    _battlePeak = Math.Max(_battlePeak, projectiles.Count);
                    if (_projectileCap > 0 && projectiles.Count >= _projectileCap - ReservedProjectileSlots) _battleFull++;
                }
                if (!phase) return;
                p.NativeSamples++;
                foreach (var w in p.Watches.Values)
                {
                    if (w.Unit <= 0) continue;
                    unsafe
                    {
                        if (GameUnitManagerAPI.Instance.TryGetUnitById(w.Unit, out GameUnit* u))
                        {
                            string native = $"{u->r_AnimationFrame}/{u->r_CurrentSpriteAnimationFrame}";
                            if (w.LastNative != null && native != w.LastNative) w.NativeChanges++;
                            w.LastNative = native;
                        }
                    }
                    foreach (int pid in projectiles)
                        if (GameProjectileManagerAPI.Instance.GetSourceUnit(pid) == w.Unit && w.ShotIds.Add(pid)) w.Shots++;
                }
            }
            catch (Exception ex)
            {
                if (_tickErrors++ == 0) Plugin.Logger.LogWarning("[SelfTest] tick sampler: " + ex.Message);
            }
        }

        private int _tickErrors, _battleTickSamples;

        private void HookTicks(bool on)
        {
            if (on == _tickHooked) return;
            if (on) GameTimeManagerAPI.Instance.OnTick += OnNativeTick;
            else GameTimeManagerAPI.Instance.OnTick -= OnNativeTick;
            _tickHooked = on;
        }

        /// <summary>One step of the pools stages (under the engine lock); true when they are done.</summary>
        private bool PoolsTick(float now)
        {
            if (LimitsStalled(now, "pools " + PoolsProgress())) return false;
            int tick = global::Director.instance.getSimTickCount();
            if (now - _poolNoteTime >= 60f)
            {
                _poolNoteTime = now;
                Note($"pools progress: {PoolsProgress()}, game tick {GameTick()}, sim tick {tick}");
            }
            switch (_pStep)
            {
                case 0:
                    BuildSetup();
                    _pStep = 1;
                    return false;
                case 1:
                    if (!BuildTick(GameTick())) return false;
                    PoolSetup();
                    _stepStart = now; _bTick = GameTick();
                    _pStep = 2;
                    return false;
                case 2:
                    CentreCamera(MapCentre, MapCentre);
                    if (GameTick() - _bTick < 10 && now - _stepStart < 60f) return false;
                    if (!FindDrawnTiles()) { DeleteTestUnits(final: true); _pStep = 9; return false; }
                    StartEnginePhase(_e0, now);
                    _pStep = 3;
                    return false;
                case 3:
                case 4:
                    _next = now;   // every frame
                    if (!EngineTick(now)) return false;
                    if (_pStep == 3) { StartEnginePhase(_c2, now); _pStep = 4; return false; }
                    SpritePool.Enabled = false;
                    _stepStart = now; _bTick = GameTick();
                    _pStep = 6;
                    return false;
                case 6:
                    CentreCamera(MapCentre, MapCentre);
                    if (GameTick() - _bTick < 10 && now - _stepStart < 60f) return false;
                    if (!NextWave()) { _bTick = GameTick(); _stepStart = now; return false; }
                    StartEnginePhase(_e1, now);
                    _pStep = 7;
                    return false;
                case 7:
                    _next = now;
                    if (!EngineTick(now)) return false;
                    StartBattle(now);
                    _pStep = 8;
                    return false;
                case 8:
                    _next = now;
                    if (!BattleTick(now)) return false;
                    DeleteTestUnits(final: true);
                    _stepStart = now;
                    _pStep = 9;
                    return false;
                case 9:
                    CentreCamera(MapCentre, MapCentre);
                    if ((UnitRecords() > _baselineUnits + 50 || MapCount("chimps") > 100) && now - _stepStart < 180f) return false;
                    Note($"Units removed: unit records {UnitRecords()} (before the stage {_baselineUnits}); {PoolSnapshot()}");
                    if (_only != "pools")
                    {
                        // C1 freezes the game (measured: 6 processTestMap exceptions leak the 6 render buffers); only a pools-only run ends with it.
                        Note("SKIP C1 (old code with the sprite pool emptied): it freezes the game, so it runs only at the end of 'test_headless.ps1 -Only pools'");
                        ReportEngines();
                        return FinishPools();
                    }
                    SpritePool.Enabled = false;
                    UnitLimit.TryWrite(UnitLimit.ExtremeTroopsValue);
                    StartEnginePhase(_c1, now);
                    _pStep = 10;
                    return false;
                case 10:
                    _next = now;
                    if (!EngineTick(now)) return false;
                    SpritePool.Enabled = true;
                    UnitLimit.TryWrite(_unitLimitBefore);
                    ReportEngines();
                    return FinishPools();
            }
            return true;
        }

        private int _bTick;

        private bool FinishPools()
        {
            HookTicks(false);
            if (_logHooked) { Application.logMessageReceivedThreaded -= OnUnityLog; _logHooked = false; }
            Note($"Pools done; {PoolSnapshot()}");
            return true;
        }

        private string PoolsProgress() => $"pools step {_pStep}, building case {_bCase}, phase {_ePhaseNow?.Name ?? "-"}, test units {_poolUnits.Count + _phaseUnits.Count}, Pool Empty {_poolEmptyLogs}, render exceptions {_renderExceptions}";

        // ===================================================
        // A. Building limit
        // ===================================================

        private unsafe void BuildSetup()
        {
            HookTicks(true);
            _module = (ulong)(long)GetModuleHandleW("CrusaderDE.dll");
            _bMgr = Plugin.GlobalsApi?.GameBuildingManagerVA ?? 0;
            bool found = BuildingLimit.EnsureFound();
            ResolveModeAddress();
            ulong free = RipTarget(_module, FreeReadRva, new byte[] { 0x8B, 0x0D });
            ulong slots = SlotTarget(), slots2 = RipTarget(_module, SlotRead2Rva, new byte[] { 0x39, 0x35 });
            _bExact = found && _bMgr != 0 && free == _bMgr + MgrFreeOffset && slots == _module + SlotRva && slots2 == _module + Slot2Rva && _modeVA != 0;
            _players = 0;
            if (slots != 0 && slots2 != 0)
            {
                int occupied = 0;
                for (int s = 0; s < 8; s++) if (*(int*)(slots + (ulong)(4 * s)) != -1 || *(int*)(slots2 + (ulong)(4 * s)) != 0) occupied++;
                _players = Math.Max(2, occupied);
            }
            Note($"Building limit: share code {(found ? $"at RVA 0x{BuildingLimit.MovRva:X}, constant {BuildingLimit.Current()}" : "NOT found")}; manager 0x{_bMgr:X} " +
                 $"(free-records read at RVA {FreeReadRva:X} -> 0x{free:X}), lobby slots 0x{slots:X} / 0x{slots2:X}, players counted by the game's rule {_players}; " +
                 $"game mode address {(_modeVA != 0 ? "verified" : "not verified")}; exact boundaries {(_bExact ? "checked" : "SKIPPED (an address did not verify)")}");
            if (!found || _bMgr == 0)
            {
                Note("SKIP building limit cases: the share code or the building manager was not found");
                _buildCases = new BuildCase[0];
                return;
            }
            if (_only == "pools")
            {
                Note("Applying the GameplaySettings file through the session-start path (ConfigLoader.ApplyAllGlobalConfigs; the editor map raises no OnStartMap)");
                Config.Toml.ConfigLoader.ApplyAllGlobalConfigs();
            }
            int fromFile = BuildingLimit.Current();
            BuildingLimit.RestoreGameValue("selftest: map unload");
            int afterUnload = BuildingLimit.Current();
            Check("BuildingLimit from the GameplaySettings file in effect, and map unload puts the game's code back",
                fromFile == BuildingLimitConfigured && afterUnload == BuildingLimit.GameValue,
                $"code constant {fromFile} after the session-start apply (file {BuildingLimitConfigured}), {afterUnload} after the unload restore (game {BuildingLimit.GameValue})");

            // 30 gold per Well: the click checks the cost (RVA 0xCC420) after the limit check.
            GamePlayerManagerAPI.Instance.SetPlayerGold(BuildPlayer, 100000);
            Note($"Player {BuildPlayer}: buildings {CountOwned(BuildPlayer)}, gold {GamePlayerManagerAPI.Instance.GetPlayerGold(BuildPlayer)}");

            int share = BuildingLimit.Share(BuildingLimit.GameValue, Math.Max(2, _players));
            _buildCases = new[]
            {
                new BuildCase { Name = "control: editor mode 0, real counters", Mode = 0, Expect = true },
                new BuildCase { Name = $"mode 0 (campaign rule): own buildings {share + 1} (over any skirmish share), free records 20", Mode = 0, OwnFixed = share + 1, Free = 20, Expect = true },
                new BuildCase { Name = "mode 0: free records 19 (map-wide limit)", Mode = 0, Free = 19, Expect = false },
                new BuildCase { Name = "mode 0x63 (skirmish / trails), game code: own buildings = share - 1", Mode = 0x63, Limit = -1, OwnFromShare = -1, Expect = true },
                new BuildCase { Name = "mode 0x63, game code: own buildings = share (the game's limit reached)", Mode = 0x63, OwnFromShare = 0, Expect = false },
                new BuildCase { Name = "mode 0x63, BuildingLimit 32000: own buildings = the game's share", Mode = 0x63, Limit = 32000, OwnFixed = share, Expect = true },
                new BuildCase { Name = "mode 0x63, BuildingLimit 1000: own buildings = share - 1", Mode = 0x63, Limit = 1000, OwnFromShare = -1, Expect = true },
                new BuildCase { Name = "mode 0x63, BuildingLimit 1000: own buildings = share (limit reached)", Mode = 0x63, OwnFromShare = 0, Expect = false },
                new BuildCase { Name = "mode 0x63, BuildingLimit 32000, free records 19 (the map-wide limit stays)", Mode = 0x63, Limit = 32000, Free = 19, Expect = false },
                new BuildCase { Name = "mode 0x63, BuildingLimit -1 (game code back): own buildings = the game's share", Mode = 0x63, Limit = -1, OwnFixed = share, Expect = false },
            };
        }

        private unsafe ulong SlotTarget()
        {
            // cmp dword [rip + disp32], -1: 83 3D disp32 FF (7 bytes; the immediate follows the displacement).
            byte* at = (byte*)(_module + SlotReadRva);
            if (_module == 0 || at[0] != 0x83 || at[1] != 0x3D || at[6] != 0xFF) return 0;
            return _module + SlotReadRva + 7 + (ulong)(long)*(int*)(at + 2);
        }

        private void ResolveModeAddress()
        {
            if (_modeVA != 0) return;
            ulong chore = Plugin.GlobalsApi?.ChoreManagerVA ?? 0;
            _modeVA = RipTarget(_module, ModeReadRva, new byte[] { 0x44, 0x8B, 0x35 });
            if (_modeVA != _module + ModeRva || _modeVA != chore + 0x870) _modeVA = 0;
        }

        private static int CountOwned(int player)
        {
            var all = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].r_PlayerIdOwner == player && all[i].r_AliveState != AliveState.None) n++;
            return n;
        }

        /// <summary>Runs the building cases one per BuildSettleTicks; true when all are done.</summary>
        private unsafe bool BuildTick(int tick)
        {
            if (_bCase >= 0 && _bCase < _buildCases.Length)
            {
                var c = _buildCases[_bCase];
                if (tick - c.Tick < BuildSettleTicks) return false;
                if (!c.Skipped)
                {
                    c.After = CountOwned(BuildPlayer);
                    c.Detail = $"own buildings for the click {c.Own}, free records {(c.Free >= 0 ? c.Free.ToString() : "real")}, code constant {c.Constant}: " +
                               $"click {(c.Accepted ? "accepted" : "refused")}; {c.Click}; player {BuildPlayer} buildings {c.Before} -> {c.After}";
                    bool exactCase = c.OwnFromShare != int.MinValue;
                    if (exactCase && !_bExact) Note($"SKIP Building limit '{c.Name}': {c.Detail} (exact boundary not verified)");
                    else Check($"Building limit: {c.Name} -> {(c.Expect ? "accepted" : "refused")}", c.Accepted == c.Expect && c.After == c.Before, c.Detail);
                }
            }
            if (++_bCase >= _buildCases.Length)
            {
                BuildingLimit.Apply(-1, "selftest: end of the building cases");
                var all = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                int removed = 0;
                for (int i = 0; i < all.Length; i++)
                    if (all[i].r_PlayerIdOwner == BuildPlayer && all[i].r_AliveState != AliveState.None) { GameBuildingManagerAPI.Instance.DeleteBuildingSafe(i + 1); removed++; }
                Note($"Building limit cases done; code constant back to {BuildingLimit.Current()} (game {BuildingLimit.GameValue}); player {BuildPlayer}'s {removed} test buildings marked for deletion");
                return true;
            }
            var nc = _buildCases[_bCase];
            if (nc.Limit != long.MinValue) BuildingLimit.Apply(nc.Limit, "selftest: " + nc.Name);
            int limit = BuildingLimit.Current();
            if (nc.Mode != 0 && _modeVA == 0)
            {
                Note($"SKIP Building limit '{nc.Name}': game mode address not verified");
                nc.Skipped = true;
                nc.Tick = tick - BuildSettleTicks;
                return false;
            }
            int realOwn = *(int*)(_bMgr + MgrOwnOffset + 4 * (ulong)BuildPlayer);
            nc.Own = nc.OwnFromShare != int.MinValue ? BuildingLimit.Share(limit, Math.Max(2, _players)) + nc.OwnFromShare
                   : nc.OwnFixed >= 0 ? nc.OwnFixed : realOwn;
            nc.Before = CountOwned(BuildPlayer);
            // During the click the Script Extender's placement override (what its CreatePrefab uses) makes its hook on the tile
            // validator (RVA 0x77E60) report a free tile, so the only code left that can block is the building limit check
            // (RVA 0x91AEA-0x91C1D, after the validator calls), which sets the blocked flag (Script Extender
            // TileManager.IsPlacementBlocked) to 1. The build command an accepted click queues runs at the next game tick
            // without the override and fails on the off-map tile, so nothing is built (a Well placed by a click crashed the
            // windowless game, run selftest-20261010-204223).
            var tiles = GameTileManagerAPI.Instance.TileManager;
            int modeBefore = _modeVA != 0 ? *(int*)_modeVA : 0;
            try
            {
                if (nc.Mode != 0) *(int*)_modeVA = nc.Mode;
                *(int*)(_bMgr + MgrOwnOffset + 4 * (ulong)BuildPlayer) = nc.Own;
                if (nc.Free >= 0) *(int*)(_bMgr + MgrFreeOffset) = nc.Free;
                tiles.PlacementBlockedOverrideValue = false;
                tiles.UsePlacementBlockedOverride = true;
                ClickWell(BuildPlayer);
                nc.Accepted = !tiles.IsPlacementBlocked;
                nc.Click = _lastClick;
                nc.Constant = BuildingLimit.Current();
            }
            finally
            {
                tiles.UsePlacementBlockedOverride = false;
                if (_modeVA != 0) *(int*)_modeVA = modeBefore;
            }
            nc.Tick = tick;
            return false;
        }

        private void ClickWell(int player)
        {
            int mapper = (int)eMappers.MAPPER_WELL;
            int sub = global::EngineInterface.StartMapperItem(mapper);
            int item = sub > 0 ? sub : mapper;
            bool inGame = !global::CrusaderDE.MainViewModel.Instance.IsMapEditorMode;
            int before = CountOwned(player);
            int x = ClickX, y = ClickY;
            global::EngineInterface.PlaceMapperItem(item, x, y, 0, player, inGame, constructingOnly: true, mouseState: 0);
            global::EngineInterface.PlaceMapperItem(item, x, y, 0, player, inGame, constructingOnly: false, mouseState: 1);
            _lastClick = $"tile {x},{y} (off the map); blocked flag {(GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked ? 1 : 0)}; " + ClickDiagnostics();
            if (_bCase == 0) Note($"First Well click: StartMapperItem({mapper}) = {sub}, PlaceMapperItem({item}, {x}, {y}, player {player}, inGame {inGame}); player {player} buildings {before} -> {CountOwned(player)} right after the click; {_lastClick}");
        }

        private string _lastClick = "";



        private unsafe string ClickDiagnostics()
        {
            if (_module == 0) return "";
            return $"click state: blocked {*(int*)(_module + BlockedRva)}, block reason {*(int*)(_module + BlockReasonRva)}, pending build mapper {*(int*)(_module + PendingMapperRva)}, player {BuildPlayer} gold {GamePlayerManagerAPI.Instance.GetPlayerGold(BuildPlayer)}";
        }

        // ===================================================
        // B. Units, sprites, engines
        // ===================================================

        private void OnUnityLog(string message, string stack, LogType type)
        {
            if (message == "Pool Empty") { Interlocked.Increment(ref _poolEmptyLogs); return; }
            if (type != LogType.Exception && type != LogType.Error) return;
            if ((stack == null || stack.IndexOf("processTestMap", StringComparison.Ordinal) < 0) && message.IndexOf("processTestMap", StringComparison.Ordinal) < 0) return;
            if (Interlocked.Increment(ref _renderExceptions) == 1) _firstRenderException = message + " | " + (stack ?? "").Replace('\n', ' ');
        }

        private static readonly FieldInfo ChimpsField = typeof(global::GameMap).GetField("chimps", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Dictionary<int, global::Chimp> Chimps() => (Dictionary<int, global::Chimp>)ChimpsField?.GetValue(global::GameMap.instance);

        private static int MapCount(string field) =>
            (typeof(global::GameMap).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(global::GameMap.instance) as System.Collections.ICollection)?.Count ?? -1;

        private static int ProjectilesAlive()
        {
            var projectiles = new List<int>();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(projectiles, AliveState.IsAlive);
            return projectiles.Count;
        }

        private string PoolSnapshot()
        {
            SpritePool.TryGetCounts(0, out int free, out int total);
            return $"sprites in use {total - free} of {total} (free {free}); drawn: units {MapCount("chimps")}, projectiles / effects {MapCount("flies")}, building animations {MapCount("buildingAnims")}, " +
                   $"wall fill-ins {MapCount("wallFillins")}, pixies {MapCount("pixies")}; native projectiles alive {ProjectilesAlive()}; \"Pool Empty\" logs {_poolEmptyLogs}, processTestMap exceptions {_renderExceptions}";
        }

        private static readonly eChimps[] WaveTypes =
        {
            // No Maceman (the runner caps it at 1) and no Crossbowman: once a Crossbowman has updated, the Script Extender's
            // GetEngageRange reports the observed (patched) native value, which the later Crossbowman guard check reads as an override.
            eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_SPEARMAN, eChimps.CHIMP_TYPE_PIKEMAN, eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_SLAVE, eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_ARAB_BOW,
        };

        private void PoolSetup()
        {
            if (!_logHooked) { Application.logMessageReceivedThreaded += OnUnityLog; _logHooked = true; }
            SpritePool.Enabled = false;   // the game's own fixed pool unless a phase says otherwise
            UnitLimit.TryRead(out _unitLimitBefore);
            UnitLimit.TryWrite(UnitLimit.ExtremeTroopsValue);
            _baselineUnits = UnitRecords();
            var map = global::GameMap.instance;
            string before = $"render bounds before: columns {map.cachedRenderBoundsLeft}-{map.cachedRenderBoundsRight}, rows {map.cachedRenderBoundsTop}-{map.cachedRenderBoundsBottom}";
            CentreCamera(MapCentre, MapCentre);
            float zoom = global::PerfectPixelWithZoom.instance != null ? global::PerfectPixelWithZoom.instance.getZoom() : -1f;
            int k = 0;
            for (int x = ProbeFrom; x <= ProbeTo; x += ProbeStep)
                for (int y = ProbeFrom; y <= ProbeTo; y += ProbeStep)
                {
                    int id = (int)GameUnitManagerAPI.Instance.CreateUnitLocal(1, 1, x, y, 8, WaveTypes[k++ % WaveTypes.Length]);
                    if (id > 0) _poolUnits.Add(id);
                }
            Note($"Units: SpritePool hook installed {SpritePool.Installed}; unit limit {_unitLimitBefore} -> {UnitLimit.ExtremeTroopsValue} for the stage; unit records in use {_baselineUnits}; " +
                 $"screen {Screen.width}x{Screen.height}, zoom {zoom}; {before}; camera centred on {MapCentre},{MapCentre}; probe grid {_poolUnits.Count} units every {ProbeStep} tiles over {ProbeFrom}-{ProbeTo}; {PoolSnapshot()}");
        }

        /// <summary>Puts the camera on a game tile the way GameData.SetCameraFromGameState does.</summary>
        private static void CentreCamera(int gameX, int gameY)
        {
            var map = global::GameMap.instance;
            map.mapGameTileToTilemapCoord(gameX, gameY, out int tx, out int ty);
            var tile = map.getMapTile(tx, ty);
            if (tile == null || global::CameraControls2D.instance == null) return;
            Vector3 pos = map.getSpritePosVector(tx, ty);
            pos.y += tile.height;
            global::CameraControls2D.instance.setCameraPos(pos.x + 0.5f, pos.y - 0.5f);
            map.PreCalcScreenCentre();
            map.ignoreNextCachedBounds = false;
        }

        private bool FindDrawnTiles()
        {
            var chimps = Chimps();
            var units = GameUnitManagerAPI.Instance;
            foreach (int id in _poolUnits)
                if (chimps != null && chimps.ContainsKey(id)) { var p = units.GetCurrentLocalTilePosition(id); _drawnTiles.Add((p.X, p.Y)); }
            var map = global::GameMap.instance;
            Note($"Probe: {_drawnTiles.Count} of {_poolUnits.Count} probe units drawn " +
                 $"(x {(_drawnTiles.Count > 0 ? _drawnTiles.Min(t => t.X) + "-" + _drawnTiles.Max(t => t.X) : "-")}, y {(_drawnTiles.Count > 0 ? _drawnTiles.Min(t => t.Y) + "-" + _drawnTiles.Max(t => t.Y) : "-")}); " +
                 $"render bounds columns {map.cachedRenderBoundsLeft}-{map.cachedRenderBoundsRight}, rows {map.cachedRenderBoundsTop}-{map.cachedRenderBoundsBottom}; {PoolSnapshot()}");
            if (_drawnTiles.Count > 0) return true;
            Note("SKIP unit / engine stages: the renderer drew none of the probe units in the windowless game (GameMap has no Chimp for them)");
            return false;
        }

        private static unsafe int UnitRecords()
        {
            int n = 0;
            for (int id = 1; id < GameUnitManager.NativeUnitSlotCount; id++)
                if (GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* u) && u->r_AliveState != AliveState.None) n++;
            return n;
        }

        /// <summary>Spawns the next wave on the drawn tiles; true when WaveTarget unit records exist.</summary>
        private bool NextWave()
        {
            var chimps = Chimps();
            SpritePool.TryGetCounts(0, out int free, out int total);
            int drawn = chimps != null ? _poolUnits.Count(id => chimps.ContainsKey(id)) : -1;
            _peakUsed = Math.Max(_peakUsed, total - free);
            _peakDrawn = Math.Max(_peakDrawn, drawn);
            int records = UnitRecords();
            Note($"Wave: {records} unit records ({_poolUnits.Count} wave units), {drawn} of them drawn; {PoolSnapshot()}");
            if (records >= WaveTarget || _poolEmptyLogs > 0) return true;
            var units = GameUnitManagerAPI.Instance;
            int made = 0;
            for (int i = 0; i < Wave && records + made < WaveTarget; i++)
            {
                var (x, y) = _drawnTiles[(_poolUnits.Count + i) % _drawnTiles.Count];
                int id = (int)units.CreateUnitLocal(1, 1, x, y, 8, WaveTypes[(_waveFrame + i) % WaveTypes.Length]);
                if (id > 0) { _poolUnits.Add(id); made++; }
            }
            _waveFrame++;
            return made == 0;
        }

        private void StartEnginePhase(EnginePhase p, float now)
        {
            _ePhaseNow = p;
            var units = GameUnitManagerAPI.Instance;
            SpritePool.Enabled = false;
            _walkFlip = 0;
            var (bx, by) = _drawnTiles[_drawnTiles.Count / 2];
            Watch Pair(eChimps type, int dy)
            {
                var w = new Watch { Unit = SpawnPhase(type, bx, by + dy, 1), Target = SpawnPhase(eChimps.CHIMP_TYPE_PIKEMAN, bx + EngineTargetTiles, by + dy, 2) };
                units.SetSpeed(w.Target, 30000);
                unsafe { if (units.TryGetUnitById(w.Target, out GameUnit* t)) t->r_CurrentSpeed2 = 30000; }
                return w;
            }
            // A Ballista or Mangonel unit spawned on the ground never operates (measured 2026-10-10: no shot and one animation
            // step in 300 game ticks; on a tower it needs the tower and its engineers), so a shooting Archer stands in for it.
            p.Watches["Archer"] = Pair(eChimps.CHIMP_TYPE_ARCHER, 0);
            p.Watches["walking Spearman"] = new Watch { Unit = SpawnPhase(eChimps.CHIMP_TYPE_SPEARMAN, bx - 4, by + 9, 1) };
            p.ExceptionsBefore = _renderExceptions; p.EmptyBefore = _poolEmptyLogs; p.AddedBefore = SpritePool.Added[0];
            p.GameTick0 = GameTick(); p.Start = now; p.Units = UnitRecords();
            Note($"Phase '{p.Name}' at {bx},{by}: " + string.Join(", ", p.Watches.Select(kv => $"{kv.Key} {kv.Value.Unit}" + (kv.Value.Target > 0 ? $" (pinned enemy {kv.Value.Target} {EngineTargetTiles} tiles east)" : ""))) +
                 $"; {p.Units} unit records; {PoolSnapshot()}");
        }

        private int SpawnPhase(eChimps type, int x, int y, int owner)
        {
            int id = (int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, 8, type);
            if (id > 0) _phaseUnits.Add(id);
            return id;
        }

        /// <summary>Samples the phase every frame; true when its window is over.</summary>
        private unsafe bool EngineTick(float now)
        {
            var p = _ePhaseNow;
            CentreCamera(MapCentre, MapCentre);   // the windowless game scrolls the camera by itself (mouse at a screen edge)
            int ticks = GameTick() - p.GameTick0;
            if (p.Drain && p.DrainAt < 0 && ticks >= 15) Drain(p);
            p.Frames++;
            var chimps = Chimps();
            foreach (var w in p.Watches.Values)
            {
                if (w.Unit <= 0) continue;
                w.Samples++;
                if (chimps != null && chimps.TryGetValue(w.Unit, out global::Chimp c) && c != null)
                {
                    string screen = $"{c.file1}/{c.image1}/{c.image2}/{c.image3}";
                    if (w.LastScreen != null && screen != w.LastScreen) w.ScreenChanges++;
                    w.LastScreen = screen;
                }
                else w.ScreenMissing++;
            }
            // Keep the walker walking: 8 tiles east, then back, every 40 game ticks.
            if (p.Watches.TryGetValue("walking Spearman", out Watch walker) && walker.Unit > 0 && ticks >= 40 * (_walkFlip + 1))
            {
                var at = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(walker.Unit);
                GameUnitManagerAPI.Instance.MoveToTile(walker.Unit, at.X + (_walkFlip % 2 == 0 ? 8 : -8), at.Y);
                _walkFlip++;
            }
            if (ticks != p.LastTick) { p.LastTick = ticks; p.LastTickTime = now; }
            bool stopped = p.DrainAt >= 0 && now - p.LastTickTime > 30f;   // the game froze (C1)
            if (ticks < WindowTicks && now - p.Start < PhaseWallSeconds && !stopped) return false;
            EndEnginePhase(p, ticks, now);
            return true;
        }

        /// <summary>Takes every free sprite, switches growth per the phase and spawns NewUnits units on the drawn tiles.</summary>
        private void Drain(EnginePhase p)
        {
            while (SpritePool.TryGetCounts(0, out int free, out _) && free > 0)
            {
                var obj = global::ObjectPool.instance.GetObjectForType(0, "simpleSprite");
                if (obj == null) break;
                _taken.Add(obj);
            }
            p.Taken = _taken.Count;
            SpritePool.Enabled = p.Fix;
            var units = GameUnitManagerAPI.Instance;
            for (int i = 0; i < NewUnits; i++)
            {
                var (x, y) = _drawnTiles[(i * 7919) % _drawnTiles.Count];
                int id = (int)units.CreateUnitLocal(1, 1, x, y, 8, WaveTypes[i % WaveTypes.Length]);
                if (id > 0) { _phaseUnits.Add(id); p.NewSpawned++; p.NewIds.Add(id); }
            }
            p.DrainAt = GameTick() - p.GameTick0;
            Note($"Phase '{p.Name}': took all {p.Taken} free sprites at game tick +{p.DrainAt}, growth {(p.Fix ? "ON" : "off")}, spawned {p.NewSpawned} new units on drawn tiles");
        }

        private void EndEnginePhase(EnginePhase p, int ticks, float now)
        {
            p.Ended = true;
            var chimps = Chimps();
            p.Ticks = ticks;
            p.Seconds = now - p.Start;
            p.Exceptions = _renderExceptions - p.ExceptionsBefore;
            p.Empty = _poolEmptyLogs - p.EmptyBefore;
            p.Added = SpritePool.Added[0] - p.AddedBefore;
            p.NewDrawn = chimps != null ? p.NewIds.Count(id => chimps.ContainsKey(id)) : -1;
            p.Detail = $"{p.Ticks} game ticks, {p.Frames} frames in {now - p.Start:0} s ({(p.Ticks > 0 ? (double)p.Frames / p.Ticks : 0):0.00} frames per game tick); " + string.Join("; ", p.Watches.Select(kv =>
                           $"{kv.Key}: shots {kv.Value.Shots}, native animation changes {kv.Value.NativeChanges}, screen frame changes {kv.Value.ScreenChanges}, not drawn in {kv.Value.ScreenMissing} of {kv.Value.Samples} samples")) +
                       (p.Drain ? $"; new units drawn {p.NewDrawn} of {p.NewSpawned}" : "") +
                       $"; \"Pool Empty\" {p.Empty}, processTestMap exceptions {p.Exceptions}, objects added by the growth {p.Added}; {PoolSnapshot()}";
            Note($"Phase '{p.Name}': {p.Detail}");
            foreach (var obj in _taken) global::ObjectPool.instance.PoolObject(0, obj);
            _taken.Clear();
            DeleteProjectilesOf(_phaseUnits);
            foreach (int id in _phaseUnits) GameUnitManagerAPI.Instance.DeleteUnitSafe(id);
            _phaseUnits.Clear();
            SpritePool.Enabled = false;
        }

        private static int Shown(EnginePhase p) => p.Watches.Values.Sum(w => w.ScreenChanges);
        private static int Animated(EnginePhase p) => p.Watches.Values.Sum(w => w.NativeChanges);

        private void ReportEngines()
        {
            Check("Few units (old code): the screen follows the units' animation", Animated(_e0) > 0 && Shown(_e0) > 0 && _e0.Exceptions == 0,
                $"native animation changes {Animated(_e0)}, screen frame changes {Shown(_e0)}, exceptions {_e0.Exceptions}");
            if (_c1.Frames > 0)
            {
                Check("Old code, sprite pool empty: new units fail in the draw loop (\"Pool Empty\" + processTestMap exception) and the game stops", _c1.Empty > 0 && _c1.Exceptions > 0 && _c1.Ticks < WindowTicks,
                    $"\"Pool Empty\" {_c1.Empty}, exceptions {_c1.Exceptions} (first: {_firstRenderException ?? "-"}), new units drawn {_c1.NewDrawn} of {_c1.NewSpawned}; " +
                    $"the game advanced {_c1.Ticks} game ticks in {_c1.Frames} frames (drained at +{_c1.DrainAt}); screen frame changes {Shown(_c1)}, native {Animated(_c1)}");
            }
            Check("New code, sprite pool empty: the pool grows, no exceptions, every new unit drawn", _c2.Added > 0 && _c2.Exceptions == 0 && _c2.Empty == 0 && _c2.NewDrawn == _c2.NewSpawned && _c2.NewSpawned > 0,
                $"added {_c2.Added}, exceptions {_c2.Exceptions}, \"Pool Empty\" {_c2.Empty}, new units drawn {_c2.NewDrawn} of {_c2.NewSpawned}; screen frame changes {Shown(_c2)}, native {Animated(_c2)}");
            Note($"{WaveTarget} units (old code): native animation changes {Animated(_e1)}, screen frame changes {Shown(_e1)} " +
                 $"({(Animated(_e1) > 0 ? 100.0 * Shown(_e1) / Animated(_e1) : 0):0}% shown; few units {(Animated(_e0) > 0 ? 100.0 * Shown(_e0) / Animated(_e0) : 0):0}%), " +
                 $"{_e1.Frames} frames for {_e1.Ticks} game ticks in {_e1.Seconds:0} s (few units: {_e0.Frames} frames for {_e0.Ticks} game ticks in {_e0.Seconds:0} s)");
            foreach (string name in new[] { "Archer", "walking Spearman" })
            {
                string Row(EnginePhase p) => p.Watches.TryGetValue(name, out Watch w) ? $"shots {w.Shots}, native {w.NativeChanges}, screen {w.ScreenChanges}" : "-";
                Note($"{name}: E0 {Row(_e0)} | C1 {Row(_c1)} | C2 {Row(_c2)} | E1 {Row(_e1)}");
            }
        }

        // ===================================================
        // C. Battle: does the projectile pool (RVA 0x3668E38) fill up?
        // ===================================================

        private int _battleTick0, _battlePeak, _battleFull, _battleSamples, _battleShooters, _projectileCap;
        private float _battleStart;

        private unsafe void StartBattle(float now)
        {
            var units = GameUnitManagerAPI.Instance;
            _projectileCap = _module != 0 ? *(int*)(_module + OtherPoolRva) : -1;
            _battleShooters = _poolUnits.Count(id =>
            {
                eChimps t = units.GetType(id);
                return t == eChimps.CHIMP_TYPE_ARCHER || t == eChimps.CHIMP_TYPE_ARAB_BOW;
            });
            int made = 0;
            for (int i = 0; i < BattleEnemies; i++)
            {
                var (x, y) = _drawnTiles[i % _drawnTiles.Count];
                int id = (int)units.CreateUnitLocal(2, 2, x + BattleOffsetTiles, y, 8, eChimps.CHIMP_TYPE_PIKEMAN);
                if (id > 0) { _poolUnits.Add(id); made++; }
            }
            _battleTick0 = GameTick(); _battleStart = now;
            _battleActive = true;
            Note($"Battle: {made} enemy Pikemen (player 2) {BattleOffsetTiles} tiles east of {_battleShooters} shooters (Archers, Arabian Bows) among {UnitRecords()} unit records; " +
                 $"projectile pool (RVA 3668E38) {_projectileCap} (Extreme troops: 6000), slots 1-24 kept for other kinds");
        }

        private bool BattleTick(float now)
        {
            CentreCamera(MapCentre, MapCentre);
            _battleSamples++;
            int ticks = GameTick() - _battleTick0;
            if (ticks < BattleTicks && now - _battleStart < PhaseWallSeconds) return false;
            _battleActive = false;
            Note($"Battle over {ticks} game ticks ({now - _battleStart:0} s, {_battleSamples} frames): projectiles alive (sampled every game tick) peak {_battlePeak} of the pool's {_projectileCap} " +
                 $"({_projectileCap - ReservedProjectileSlots} usable by shots), at the cap in {_battleFull} of {_battleTickSamples} ticks; {PoolSnapshot()}");
            return true;
        }

        /// <summary>Removes every unit the stage spawned; <paramref name="final"/> also puts the growth and the unit limit back.</summary>
        private void DeleteTestUnits(bool final)
        {
            foreach (var obj in _taken) global::ObjectPool.instance.PoolObject(0, obj);
            _taken.Clear();
            int n = _poolUnits.Count + _phaseUnits.Count;
            int shots = DeleteProjectilesOf(_poolUnits.Concat(_phaseUnits));
            foreach (int id in _poolUnits.Concat(_phaseUnits)) GameUnitManagerAPI.Instance.DeleteUnitSafe(id);
            _poolUnits.Clear();
            _phaseUnits.Clear();
            if (final)
            {
                SpritePool.Enabled = true;
                UnitLimit.TryWrite(_unitLimitBefore);
            }
            Note($"Pools: {n} test units and {shots} of their projectiles marked for deletion{(final ? $", sprite growth back on, unit limit back to {_unitLimitBefore}" : "")}");
        }

        /// <summary>
        /// Marks the projectiles fired by <paramref name="units"/> for deletion: a later stage reuses the unit ids, and the
        /// range stage counts "a live projectile from the shooter" as its first shot.
        /// </summary>
        private static int DeleteProjectilesOf(IEnumerable<int> units)
        {
            var set = new HashSet<int>(units);
            var projectiles = new List<int>();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(projectiles, AliveState.IsAlive);
            int n = 0;
            foreach (int pid in projectiles)
                if (set.Contains(GameProjectileManagerAPI.Instance.GetSourceUnit(pid)) && GameProjectileManagerAPI.Instance.DeleteProjectileSafe(pid)) n++;
            return n;
        }
    }
}
