// Tests/HeadlessSelfTest.Engines.cs
//
// PURPOSE: Self-test stage for GitHub #5: how far each siege engine fires with the game's range, with a raised
//          AttackRange and (Catapult) a lowered one, under the 2.10.0 code (Script Extender AttackRange only) and under
//          EngineRangePatch; and what a Crossbowman does when its EngageRange is below its AttackRange. Runs after the
//          range stage, or alone with "-cdt-selftest-only engines" (test_headless.ps1 -Only engines).
//
// WHAT IT DOES (editor map; player 1 owns the shooter, player 2 the target; in the full run it waits EngSettleTicks
// after the range stage first):
//   B. first, Crossbowman: a control with the game's ranges at 40 tiles, then AttackRange 62 / EngageRange 58 (the
//      GitHub #5 report) and the reverse 58 / 62: at each rung (66 ... 56 tiles) a fresh idle Crossbowman and a pinned
//      Pikeman, up to XbowRungTicks: shot (distance), walked (tiles) or ignored.
//   A. Engines. Each phase spawns one engine, pinned (speed 30000, so it fires from where it stands), and its crew of
//      Engineers next to it (two, three for the Trebuchet). A spawned engine waits in state 6 until enough Engineers of
//      its owner within 30 world units carry the "operate this engine" order (unit +0x9F4 = 0x10, +0x9F6 = engine id):
//      the engine's own scan every 16 ticks (Catapult RVA 0x152570-0x15289D) then takes them as crew and sets +0xA0C.
//      The test writes those two Engineer fields until the engine reports its crew. Then a target is placed due east at decreasing distances
//      (EngLadder, from 8 tiles beyond the larger of the game range and the setting): an enemy Apothecary for the
//      Catapult and Trebuchet, a pinned enemy Engineer for the Mangonel (a human-owned Mangonel picks only Engineers and
//      siege engines by itself; four more Engineers 1-2 tiles north and south catch its scattered stones) and a pinned
//      enemy Pikeman for the Ballistas. A rung lasts EngRungTicks, longer while the engine is winding up or releasing a
//      shot (state 2 / 4). The building is placed so that the game's own
//      distance (to the building centre minus half its size, RVA 0x152C27-0x152C77) equals the rung. A human-owned
//      Catapult / Trebuchet gets the attack order the game's order handler writes (RVA 0x11FD8A-0x11FE66 and
//      0x197950: target building, uid, aim point, +0xA1D = +0x9F4 = 9); the other engines pick targets themselves.
//      The first rung where a NEW projectile from the engine appears is its firing distance; then up to EngDamageTicks
//      for damage (StoneDamageTicks for the stone throwers, whose hits are notes: they scatter, and the Mangonel missed for
//      700 ticks even at its game range in runs selftest-20261011-200118 and -202749). Phases per engine: game range; the setting with the 2.10.0 code; the setting through the Units-file
//      path (UnitRanges.ApplyAttackRange, i.e. with EngineRangePatch).
//
// IMPORTANT FOR AI AGENTS:
// - Game record offsets (unit +0x..., building +0x...) are converted to the Script Extender's struct pointers (unit
//   record + 0x65C, building record + 0x5C); the stage checks that against the game's own code first.
// - Only the measured first-shot distance is judged (the patch cannot write it); 2.10.0-code phases are notes.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Toml.Units.Properties;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        private const int EnginesStage = 9;
        private const int UnitToSe = 0x65C, BuildingToSe = 0x5C, UnitRecord = 0x490, BuildingRecord = 0x32C;
        // Catapult update (game 2.8.2): "lea rbp, [unit manager]" at 0x152108, "cmp [rcx + rsi + building uid table]" at
        // 0x152C1E (building records + 0x134), "cmp [rsi + rax*4 + human slots], -1" at 0x1521FD (not -1 = human player).
        private const uint UnitManagerLeaRva = 0x152108, BuildingUidCmpRva = 0x152C1E, HumanSlotCmpRva = 0x1521FD;
        private const int EngX = 326, EngY = 400, EngRungTicks = 250, EngCrewTicks = 400, EngDamageTicks = 700, StoneDamageTicks = 1500, EngReorderTicks = 60, EngFiringTicks = 600;
        private static readonly int[] EngLadder = { 115, 108, 104, 100, 96, 93, 90, 88, 85, 82, 80, 77, 75, 72, 70, 66, 62, 58, 54, 50, 45, 40, 32, 24, 16 };
        internal const int CatapultLowered = 70;
        // Raised AttackRange per engine (the reporter's values where given: Trebuchet 100, Arabian Ballista 77).
        private static readonly (eChimps Unit, int Raised, bool Building)[] EngSettings =
        {
            (eChimps.CHIMP_TYPE_CATAPULT, 90, true), (eChimps.CHIMP_TYPE_TREBUCHET, 100, true), (eChimps.CHIMP_TYPE_MANGONEL, 85, false),
            (eChimps.CHIMP_TYPE_BALLISTA, 100, false), (eChimps.CHIMP_TYPE_ARAB_BALLISTA, 77, false)
        };

        private sealed class EngPhase
        {
            internal string Name;
            internal eChimps Unit;
            internal int Owner = 1, Attack, Game;   // Attack 0 = game ranges
            internal bool NewCode, Building, Crewed, Damaged;
            internal int HitAt = -1, ShotTick, CrewTicks = -1, Orders, Shots;
            internal double ShotDistance;
            internal string Detail = "not run";
            internal readonly SortedDictionary<string, int> Seen = new SortedDictionary<string, int>();
            internal ProjectileType Projectile;
            internal int AmmoStart = int.MinValue, LastTrace = int.MinValue;
            internal readonly List<string> Trace = new List<string>(), NewProjectiles = new List<string>();
        }

        private List<EngPhase> _eng;
        private readonly List<int> _eCrew = new List<int>();
        // Mangonel only: Engineers 1 and 2 tiles north and south of the target, so its scattered stones can hit one
        // (run selftest-20261011-202749: no hit on a single Engineer within 700 ticks). They are farther than the rung, so
        // only the target itself is in range when the first shot is judged.
        private readonly List<int> _eFlank = new List<int>();
        private readonly List<int> _eFlankHp = new List<int>();
        private int _eIdx = -1, _eStep, _eEngine, _eTargetUnit, _eTargetBuilding, _eRung, _eRungTick, _eStepTick, _eTargetHp, _eLastOrder;
        private int _eHalf, _eSize, _eScale;
        private ulong _eModule, _eUnits, _eBuildings, _eSlots;
        // Projectile slot -> its record id (+0x44), type and shooter when the rung started: GetAllProjectiles without a filter lists every slot.
        private readonly Dictionary<int, ulong> _eSeenSlots = new Dictionary<int, ulong>();
        private readonly List<int> _eProj = new List<int>();
        private bool _eXbow, _eDone, _eBegun;
        private int _eBeginTick;
        private const int EngSettleTicks = 600;

        /// <summary>Engineers an engine waits for (+0xA0C): 3 for the Trebuchet (RVA 0x1536E1), 2 for the others (e.g. 0x15219F).</summary>
        private static int CrewSize(eChimps t) => t == eChimps.CHIMP_TYPE_TREBUCHET ? 3 : 2;

        private static string EngName(eChimps t) => t.ToString().Replace("CHIMP_TYPE_", "");

        private unsafe bool IsHuman(int player) => *SlotPtr(player) != -1;
        private unsafe int* SlotPtr(int player) => (int*)(_eModule + _eSlots + (ulong)(4 * player));

        // ===================================================
        // Setup
        // ===================================================

        private void StartEngines(float now)
        {
            _stage = EnginesStage;
            _deadline = now + 600;
            _next = now;
            _watchTick = global::Director.instance.getSimTickCount(); _watchTime = now; _lastProgressNote = now;
            // After the range stage, let its killed units finish dying first: a killed unit's record is cleared some ticks
            // later, and a unit spawned into its id meanwhile is wiped with it (run selftest-20261011-184603: Engineers at 0,0).
            _eBeginTick = _watchTick + (_only == "engines" ? 0 : EngSettleTicks);
            Note("Engines stage (GitHub #5): Crossbowman EngageRange < AttackRange, then siege engine firing distance");
        }

        private unsafe void BeginEngines(float now)
        {
            _eBegun = true;
            _deadline = now + 600;
            // Addresses the game's own code uses, read from the DLL on disk.
            byte[] file = GameCode.ReadModuleFile();
            _eModule = (ulong)(long)GameCode.ModuleBase;
            _eUnits = (ulong)((long)UnitManagerLeaRva + 7 + GameCode.ReadFileInt32(file, UnitManagerLeaRva + 3));
            _eBuildings = (ulong)(GameCode.ReadFileInt32(file, BuildingUidCmpRva + 3) - 0x134);
            _eSlots = (ulong)GameCode.ReadFileInt32(file, HumanSlotCmpRva + 3);
            var units = GameUnitManagerAPI.Instance;
            int probe = Spawn(eChimps.CHIMP_TYPE_PIKEMAN, EngX, EngY - 20, owner: 2);
            units.TryGetUnitById(probe, out GameUnit* pu);
            bool unitsOk = (ulong)pu == _eModule + _eUnits + (ulong)(probe * UnitRecord) + UnitToSe && (int)((byte*)&pu->r_UnitChimp - (byte*)pu) == 0x6E6 - UnitToSe;
            units.DeleteUnitSafe(probe);

            _eScale = BuildingScales.GetScale(eMappers.MAPPER_HEALER);
            int bid = PlaceTarget(2, EngX + 60, EngY - 10);
            bool buildingsOk = false;
            if (bid > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(bid, out GameBuilding* pb))
            {
                buildingsOk = (ulong)pb == _eModule + _eBuildings + (ulong)(bid * BuildingRecord) + BuildingToSe && (int)((byte*)&pb->r_TilePositionXBegin - (byte*)pb) == 0x14A - BuildingToSe;
                _eSize = GB16(bid, 0x154);
                _eHalf = _eSize / 2;
                GameBuildingManagerAPI.Instance.DeleteBuildingSafe(bid);
            }
            string slots = string.Join(", ", Enumerable.Range(1, 8).Select(p => $"{p}:{*SlotPtr(p)}"));
            Note($"Game tables: units RVA 0x{_eUnits:X} (Script Extender pointers match: {unitsOk}), buildings RVA 0x{_eBuildings:X} (match: {buildingsOk}), " +
                 $"human-player slots RVA 0x{_eSlots:X} = [{slots}] (-1 = AI branch of the engine code); target Apothecary size {_eSize} (half {_eHalf}), scale {_eScale}");
            if (!unitsOk || !buildingsOk || _eSize <= 0) throw new InvalidOperationException("unit / building record layout not as expected; the engine stage cannot address the game's fields");
            Note("Engine range code (EngineRangePatch): " + string.Join("; ", EngineRangePatch.Engines.Select(u => $"{EngName(u)} {EngineRangePatch.Describe(u)}")));

            _eng = new List<EngPhase>();
            foreach (var s in EngSettings)
            {
                int game = UnitRanges.GameAttackTiles(s.Unit);
                _eng.Add(new EngPhase { Name = $"{EngName(s.Unit)}, game range {game}", Unit = s.Unit, Game = game, Building = s.Building });
                _eng.Add(new EngPhase { Name = $"{EngName(s.Unit)}, AttackRange {s.Raised}, 2.10.0 code", Unit = s.Unit, Game = game, Attack = s.Raised, Building = s.Building });
                _eng.Add(new EngPhase { Name = $"{EngName(s.Unit)}, AttackRange {s.Raised}", Unit = s.Unit, Game = game, Attack = s.Raised, NewCode = true, Building = s.Building });
                if (s.Unit == eChimps.CHIMP_TYPE_CATAPULT)
                {
                    _eng.Add(new EngPhase { Name = $"CATAPULT, AttackRange {CatapultLowered}, 2.10.0 code", Unit = s.Unit, Game = game, Attack = CatapultLowered, Building = true });
                    _eng.Add(new EngPhase { Name = $"CATAPULT, AttackRange {CatapultLowered}", Unit = s.Unit, Game = game, Attack = CatapultLowered, NewCode = true, Building = true });
                }
            }
            // No AI-owned phase: the AI branch picks its own targets (RVA 0x18FC00), and an AI-owned Catapult (player 2) never fired
            // in the editor map, at its game range or with AttackRange 90 (run selftest-20261011-193734, ammunition 40 -> 40).
            _eIdx = -1;
            StartXbowPhases(now);   // the Crossbowman part first: its control shot proves the map row engages at all
        }

        // ===================================================
        // Phases
        // ===================================================

        private void ResetEngineRanges()
        {
            foreach (eChimps t in EngineRangePatch.Engines) UnitRanges.ResetAttackRange(t);
        }

        private void CleanEnginePhase()
        {
            var units = GameUnitManagerAPI.Instance;
            DeleteProjectilesOf(new[] { _eEngine });
            foreach (int id in _eCrew.Concat(_eFlank).Concat(new[] { _eEngine, _eTargetUnit })) if (id > 0) units.DeleteUnitSafe(id);
            _eFlank.Clear(); _eFlankHp.Clear();
            _eCrew.Clear();
            if (_eTargetBuilding > 0) GameBuildingManagerAPI.Instance.DeleteBuildingSafe(_eTargetBuilding);
            _eEngine = _eTargetUnit = _eTargetBuilding = 0;
        }

        private unsafe void NextEnginePhase(float now)
        {
            CleanEnginePhase();
            if (_eIdx >= 0)
            {
                var done = _eng[_eIdx];
                Note($"Engine phase '{done.Name}': {done.Detail}; engine states seen (state/target kind x samples): {string.Join(", ", done.Seen.Select(kv => kv.Key + " x" + kv.Value))}; " +
                     $"trace (ticks into the rung: state, frame +0x908, ammunition +0x9BA, cow +0xA08, step +0x6A8): {string.Join(" ", done.Trace)}; new projectiles (id type source): {string.Join(", ", done.NewProjectiles)}");
            }
            ResetEngineRanges();
            if (++_eIdx >= _eng.Count)
            {
                ReportEngineRanges();
                _eDone = true;
                return;
            }
            var p = _eng[_eIdx];
            _deadline = now + 600;
            p.Projectile = GameUnitManagerAPI.GetDefaultAttackRangeProjectileType(p.Unit);
            if (p.Attack > 0)
            {
                if (p.NewCode) UnitRanges.ApplyAttackRange("AttackRange", p.Unit, p.Attack);
                else
                {
                    // 2.10.0: the Script Extender's AttackRange plus the engage distance, nothing else.
                    GameUnitManagerAPI.Instance.SetUnitAttackRange(p.Unit, p.Attack);
                    EngageDistancePatch.SetAttackTiles(p.Unit, p.Attack, p.Game);
                }
            }
            var units = GameUnitManagerAPI.Instance;
            _eEngine = Spawn(p.Unit, EngX, EngY, p.Owner);
            Pin(_eEngine);
            for (int i = 0; i < CrewSize(p.Unit); i++) _eCrew.Add(Spawn(eChimps.CHIMP_TYPE_ENGINEER, EngX - 1, EngY - 1 + i, p.Owner));
            _eStep = 0;
            _eStepTick = global::Director.instance.getSimTickCount();
            EngageDistancePatch.TryGetCurrentWorld(p.Unit, out int engage);
            Note($"Engine phase '{p.Name}': owner {p.Owner} ({(IsHuman(p.Owner) ? "human" : "AI")} branch); Script Extender AttackRange {units.GetUnitAttackRange(p.Unit)}; " +
                 $"engine code {EngineRangePatch.Describe(p.Unit)}; engage {(engage > 0 ? engage + " world units" : "none")}; engine {_eEngine} at {EngX},{EngY}, Engineers {string.Join(" / ", _eCrew)} next to it; ammunition (+0x9BA) {GU16(_eEngine, 0x9BA)}");
        }

        /// <summary>Samples one frame of the engine stage; true when the engine and Crossbowman parts are done.</summary>
        private bool EnginesTick(float now)
        {
            _next = now;   // per frame: crew orders and first shots are tick-exact
            if (_eDone) return true;
            if (!_eBegun)
            {
                if (global::Director.instance.getSimTickCount() >= _eBeginTick) BeginEngines(now);
                return false;
            }
            if (_eXbow)
            {
                if (!XbowTick(now)) return false;
                _eXbow = false;
                NextEnginePhase(now);
                return false;
            }
            var p = _eng[_eIdx];
            int tick = global::Director.instance.getSimTickCount();
            string seen = $"{GU16(_eEngine, 0x918)}/{GU16(_eEngine, 0x9F4)}";
            p.Seen.TryGetValue(seen, out int n);
            p.Seen[seen] = n + 1;
            int key = GU16(_eEngine, 0x918) * 1000 + GU16(_eEngine, 0x9BA);
            if (_eStep > 0 && key != p.LastTrace && p.Trace.Count < 60)
            {
                p.LastTrace = key;
                p.Trace.Add($"t{tick - _eRungTick}:s{GU16(_eEngine, 0x918)}f{GU32(_eEngine, 0x908)}a{GU16(_eEngine, 0x9BA)}c{GU16(_eEngine, 0xA08)}k{GU32(_eEngine, 0x6A8)}");
            }

            if (_eStep == 0)
            {
                foreach (int e in _eCrew) { SetGU16(e, 0x9F4, 0x10); SetGU16(e, 0x9F6, (short)_eEngine); }
                if (GU16(_eEngine, 0xA0C) == CrewSize(p.Unit))
                {
                    p.Crewed = true;
                    p.CrewTicks = tick - _eStepTick;
                    _eRung = Array.FindIndex(EngLadder, d => d <= Math.Max(p.Game, p.Attack) + 8);
                    _eStep = 1;
                    SpawnEngineTarget(p, tick);
                }
                else if (tick - _eStepTick > EngCrewTicks)
                {
                    p.Detail = $"SKIP not crewed within {EngCrewTicks} ticks (engine +0xA0C {GU16(_eEngine, 0xA0C)}, state {GU16(_eEngine, 0x918)}; " +
                               $"Engineers at {string.Join(" / ", _eCrew.Select(Pos))}, their +0x9F4 {string.Join(" / ", _eCrew.Select(e => GU16(e, 0x9F4)))}, state {string.Join(" / ", _eCrew.Select(e => GU16(e, 0x918)))})";
                    NextEnginePhase(now);
                }
                return false;
            }

            bool damaged = TargetDamaged();
            if (_eStep == 1)
            {
                if (p.Building && IsHuman(p.Owner) && (GU16(_eEngine, 0x9F4) != 9 || GU16(_eEngine, 0x98E) != _eTargetBuilding) && tick - _eLastOrder >= EngReorderTicks)
                    OrderAttack(p, tick);
                int shots = NewShots(_eEngine, p.Projectile, p.NewProjectiles);
                if (shots > 0 || damaged)
                {
                    p.HitAt = EngLadder[_eRung];
                    p.ShotTick = tick;
                    p.Shots = shots;
                    p.ShotDistance = TargetDistance(p);
                    p.Detail = $"crewed after {p.CrewTicks} ticks; first {(shots > 0 ? "shot" : "damage")} with the target {EngLadder[_eRung]} tiles away " +
                               $"(ammunition {p.AmmoStart} -> {GU16(_eEngine, 0x9BA)}) " +
                               $"(game distance now {p.ShotDistance:0.#}), {tick - _eRungTick} ticks after it was placed, engine at {Pos(_eEngine)}" +
                               (p.Building && IsHuman(p.Owner) ? $", attack orders given {p.Orders}" : "");
                    _eStep = 2;
                    if (!damaged) return false;
                }
                else if (tick - _eRungTick >= EngRungTicks && (!Firing(_eEngine) || tick - _eRungTick >= EngRungTicks + EngFiringTicks))
                {
                    RemoveEngineTarget();
                    if (++_eRung >= EngLadder.Length)
                    {
                        p.Detail = $"crewed after {p.CrewTicks} ticks; no shot at any distance down to {EngLadder[EngLadder.Length - 1]} tiles (ammunition {p.AmmoStart} -> {GU16(_eEngine, 0x9BA)})";
                        NextEnginePhase(now);
                    }
                    else SpawnEngineTarget(p, tick);
                    return false;
                }
                else return false;
            }
            // _eStep 2: wait for damage on the same rung.
            int wait = Bolts(p.Unit) ? EngDamageTicks : StoneDamageTicks;
            if (damaged || tick - p.ShotTick > wait)
            {
                p.Damaged = damaged;
                p.Detail += damaged ? $"; target damaged {tick - p.ShotTick} ticks after the first shot" : $"; no damage within {wait} ticks of the first shot";
                NextEnginePhase(now);
            }
            return false;
        }

        /// <summary>Bolt throwers aim straight; the Catapult, Trebuchet and Mangonel scatter stones (several runs: no hit within 700 ticks).</summary>
        private static bool Bolts(eChimps t) => t == eChimps.CHIMP_TYPE_BALLISTA || t == eChimps.CHIMP_TYPE_ARAB_BALLISTA;

        /// <summary>The engine is winding up (state 2) or releasing (state 4) a shot at the current target.</summary>
        private static bool Firing(int engine)
        {
            int state = GU16(engine, 0x918);
            return state == 2 || state == 4;
        }

        /// <summary>
        /// The unit target: a pinned enemy Pikeman, but an Engineer for the Mangonel: a human-owned Mangonel picks only
        /// Engineers and siege engines by itself (target scoring for projectile type 4, RVA 0x18F4A8-0x18F4E8).
        /// </summary>
        private static eChimps UnitTarget(eChimps engine) => engine == eChimps.CHIMP_TYPE_MANGONEL ? eChimps.CHIMP_TYPE_ENGINEER : eChimps.CHIMP_TYPE_PIKEMAN;

        private void SpawnEngineTarget(EngPhase p, int tick)
        {
            int d = EngLadder[_eRung];
            int enemy = p.Owner == 1 ? 2 : 1;
            if (p.Building)
            {
                _eTargetBuilding = PlaceTarget(enemy, EngX + d, EngY - _eHalf);
                _eTargetHp = _eTargetBuilding > 0 ? GameBuildingManagerAPI.Instance.GetCurrentHealth(_eTargetBuilding) : -1;
            }
            else
            {
                _eTargetUnit = Spawn(UnitTarget(p.Unit), EngX + d, EngY, enemy);
                Pin(_eTargetUnit);
                _eTargetHp = GameUnitManagerAPI.Instance.GetCurrentHealth(_eTargetUnit);
                if (p.Unit == eChimps.CHIMP_TYPE_MANGONEL)
                    foreach (int dy in new[] { -2, -1, 1, 2 })
                    {
                        int f = Spawn(UnitTarget(p.Unit), EngX + d, EngY + dy, enemy);
                        Pin(f);
                        _eFlank.Add(f);
                        _eFlankHp.Add(GameUnitManagerAPI.Instance.GetCurrentHealth(f));
                    }
            }
            SnapshotProjectiles();
            if (p.AmmoStart == int.MinValue) p.AmmoStart = GU16(_eEngine, 0x9BA);
            _eRungTick = tick;
            _eLastOrder = int.MinValue / 2;
            if (p.Building && IsHuman(p.Owner)) OrderAttack(p, tick);
        }

        private void RemoveEngineTarget()
        {
            if (_eTargetUnit > 0) GameUnitManagerAPI.Instance.DeleteUnitSafe(_eTargetUnit);
            foreach (int f in _eFlank) GameUnitManagerAPI.Instance.DeleteUnitSafe(f);
            _eFlank.Clear(); _eFlankHp.Clear();
            if (_eTargetBuilding > 0) GameBuildingManagerAPI.Instance.DeleteBuildingSafe(_eTargetBuilding);
            _eTargetUnit = _eTargetBuilding = 0;
        }

        /// <summary>An Apothecary for <paramref name="owner"/> with its first tile at x, y (0 = not placed).</summary>
        private unsafe int PlaceTarget(int owner, int x, int y)
        {
            var api = GameBuildingManagerAPI.Instance;
            long result = api.CreatePrefab(owner, x, y, eMappers.MAPPER_HEALER, _eScale, 0, true, true);
            var found = new List<int>();
            api.GetAllBuildings(found, null, eStructs.STRUCT_HEALER, PlayerRelationship.Any, 1);
            foreach (int id in found)
                if (api.TryGetBuildingById(id, out GameBuilding* b) && b->r_AliveState != AliveState.MarkedForDeletion && b->r_PlayerIdOwner == owner &&
                    b->r_TilePositionXBegin == x && b->r_TilePositionYBegin == y)
                    return id;
            Note($"Target Apothecary for player {owner} at {x},{y}: CreatePrefab returned {result}, no building found");
            return 0;
        }

        /// <summary>The attack order the game's order handler gives a unit for a building (RVA 0x11FD8A-0x11FE66).</summary>
        private void OrderAttack(EngPhase p, int tick)
        {
            int b = _eTargetBuilding, e = _eEngine;
            if (b <= 0) return;
            int size = GB16(b, 0x154);
            if (GU16(e, 0x918) == 0x69) SetGU16(e, 0x918, 0x65);
            SetGU16(e, 0x716, (short)(GB16(b, 0x14A) * 8 + size * 4));
            SetGU16(e, 0x718, (short)(GB16(b, 0x14C) * 8 + size * 4));
            SetGU16(e, 0x71A, (short)GB16(b, 0x148));
            SetGU16(e, 0x99C, -1);
            SetGU16(e, 0x98E, (short)b);
            SetGU32(e, 0x9F8, GB32(b, 0x134));
            SetGU8(e, 0xA1D, 9);
            SetGU16(e, 0x9F4, 9);
            SetGU8(e, 0xA50, 0);
            SetGU32(e, 0x752, 0); SetGU16(e, 0x92A, 0); SetGU16(e, 0x8EC, 0);   // RVA 0x197950
            SetGU16(e, 0xA30, 10);
            _eLastOrder = tick;
            p.Orders++;
        }

        private static unsafe ulong SlotKey(GameProjectile* g) => ((ulong)g->r_GlobalId << 32) | ((ulong)(ushort)g->r_ProjectileType << 16) | (ushort)g->r_SourceUnitId;

        private unsafe void SnapshotProjectiles()
        {
            _eSeenSlots.Clear();
            _eProj.Clear();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(_eProj);
            foreach (int id in _eProj)
                if (GameProjectileManagerAPI.Instance.TryGetProjectileById(id, out GameProjectile* g) && g->r_AliveState != AliveState.None) _eSeenSlots[id] = SlotKey(g);
        }

        /// <summary>
        /// Projectiles created since the snapshot (a used slot whose record id changed), in any state, fired by
        /// <paramref name="shooter"/> or of <paramref name="type"/> (no other shooter is on the map); the first few are noted.
        /// </summary>
        private unsafe int NewShots(int shooter, ProjectileType type, List<string> notes)
        {
            _eProj.Clear();
            GameProjectileManagerAPI.Instance.GetAllProjectiles(_eProj);
            int n = 0;
            foreach (int id in _eProj)
            {
                if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(id, out GameProjectile* g) || g->r_AliveState == AliveState.None) continue;
                if (_eSeenSlots.TryGetValue(id, out ulong record) && record == SlotKey(g)) continue;
                _eSeenSlots[id] = SlotKey(g);
                int source = g->r_SourceUnitId;
                if (notes != null && notes.Count < 6)
                    notes.Add($"{id} {g->r_ProjectileType} from unit {source} state {g->r_AliveState} target {g->r_TargetWorldTileX},{g->r_TargetWorldTileY}");
                if (source == shooter || g->r_ProjectileType == type) n++;
            }
            return n;
        }

        private bool TargetDamaged()
        {
            for (int i = 0; i < _eFlank.Count; i++)
                if (GameUnitManagerAPI.Instance.GetCurrentHealth(_eFlank[i]) < _eFlankHp[i]) return true;
            if (_eTargetUnit > 0) return GameUnitManagerAPI.Instance.GetCurrentHealth(_eTargetUnit) < _eTargetHp;
            if (_eTargetBuilding > 0) return GameBuildingManagerAPI.Instance.GetCurrentHealth(_eTargetBuilding) < _eTargetHp;
            return false;
        }

        /// <summary>The distance the game's range check uses: tiles to a unit, or to a building's centre minus half its size.</summary>
        private double TargetDistance(EngPhase p)
        {
            var e = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(_eEngine);
            double dx, dy, minus = 0;
            if (_eTargetBuilding > 0)
            {
                int half = GB16(_eTargetBuilding, 0x154) / 2;
                dx = GB16(_eTargetBuilding, 0x14A) + half - e.X; dy = GB16(_eTargetBuilding, 0x14C) + half - e.Y; minus = half;
            }
            else
            {
                var t = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(_eTargetUnit);
                dx = t.X - e.X; dy = t.Y - e.Y;
            }
            return Math.Sqrt(dx * dx + dy * dy) - minus;
        }

        private void ReportEngineRanges()
        {
            var summary = new StringBuilder("Engine first-shot distances (tiles):");
            foreach (var s in EngSettings)
            {
                var list = _eng.Where(p => p.Unit == s.Unit).ToList();
                EngPhase game = list[0];
                summary.Append($" {EngName(s.Unit)} game {Shot(game)}");
                foreach (var p in list.Skip(1)) summary.Append($", {p.Attack}{(p.NewCode ? "" : " (2.10.0)")} {Shot(p)}");
                summary.Append(';');
                if (!game.Crewed) { Note($"SKIP {EngName(s.Unit)}: the engine could not be crewed in the editor map ({game.Detail}), so its range cannot be measured"); continue; }
                Check($"{EngName(s.Unit)} fires at its game range {game.Game}", game.HitAt == game.Game, game.Detail);
                foreach (var p in list.Where(x => x.NewCode))
                {
                    if (!p.Crewed) { Note($"SKIP {p.Name}: not crewed ({p.Detail})"); continue; }
                    var old = list.FirstOrDefault(x => !x.NewCode && x.Attack == p.Attack);
                    Check($"{p.Name}: fires from {p.Attack} tiles", p.HitAt == p.Attack,
                          $"{p.Detail}; with the 2.10.0 code: {(old != null ? Shot(old) : "-")}");
                    if (Bolts(p.Unit) || p.Damaged)
                        Check($"{p.Name}: the shot from {p.HitAt} tiles damages the target", p.HitAt > 0 && p.Damaged, p.Detail);
                    else
                        Note($"SKIP {p.Name}: no hit within {StoneDamageTicks} ticks of the first shot from {p.HitAt} tiles. Stone throwers scatter, so a hit is " +
                             $"not a check (at its game range: {Shot(game)}); the first-shot distance above is the measured signal");
                }
            }
            Note(summary.ToString());
        }

        private static string Shot(EngPhase p) => !p.Crewed ? "not crewed" : p.HitAt > 0 ? p.HitAt + (p.Damaged ? "" : " (no damage)") : "no shot";

        private string EnginesProgress()
        {
            if (_eXbow) return $"Crossbowman phase {_xIdx}, rung {_xRung}, shooter {Pos(_xShooter)}, target {Pos(_xTarget)}";
            if (_eng == null || _eIdx < 0 || _eIdx >= _eng.Count) return "engines not started";
            return $"engine phase '{_eng[_eIdx].Name}', step {_eStep}, rung {(_eRung >= 0 && _eRung < EngLadder.Length ? EngLadder[_eRung] : -1)}, engine {Pos(_eEngine)} state {GU16(_eEngine, 0x918)} crew {GU16(_eEngine, 0xA0C)} target kind {GU16(_eEngine, 0x9F4)}";
        }

        // ===================================================
        // B. Crossbowman EngageRange < AttackRange
        // ===================================================

        private const int XbowRungTicks = 400, XbowY = 412, XbowX = 340;
        private static readonly int[] XbowLadder = { 66, 64, 62, 60, 58, 56 };
        private static readonly int[] XbowControlLadder = { 40 };

        private sealed class XbowPhase
        {
            internal int Attack, Engage;   // 0 = game ranges (control)
            internal int[] Ladder = XbowLadder;
            internal readonly List<string> Rungs = new List<string>();
        }

        private readonly XbowPhase[] _xbow =
        {
            new XbowPhase { Ladder = XbowControlLadder }, new XbowPhase { Attack = 62, Engage = 58 }, new XbowPhase { Attack = 58, Engage = 62 }
        };
        private int _xIdx, _xRung, _xShooter, _xTarget, _xTargetHp, _xTick, _xShotTick = -1, _xMoved, _xStartX, _xStartY;
        private double _xShotDistance;

        private void StartXbowPhases(float now)
        {
            _eXbow = true;
            _xIdx = 0; _xRung = -1;
            _deadline = now + 600;
            ApplyXbowPhase();
            NextXbowRung();
        }

        private void ApplyXbowPhase()
        {
            var x = _xbow[_xIdx];
            if (x.Attack > 0) UnitRanges.ApplyAttackRange("AttackRange", eChimps.CHIMP_TYPE_XBOWMAN, x.Attack); else UnitRanges.ResetAttackRange(eChimps.CHIMP_TYPE_XBOWMAN);
            if (x.Engage > 0) EngageDistancePatch.SetEngageTiles(eChimps.CHIMP_TYPE_XBOWMAN, x.Engage); else EngageDistancePatch.ClearEngage(eChimps.CHIMP_TYPE_XBOWMAN);
            EngageDistancePatch.TryGetCurrentWorld(eChimps.CHIMP_TYPE_XBOWMAN, out int engage);
            Note($"Crossbowman AttackRange {x.Attack} / EngageRange {x.Engage}: Script Extender AttackRange {GameUnitManagerAPI.Instance.GetUnitAttackRange(eChimps.CHIMP_TYPE_XBOWMAN)}, " +
                 $"engage {engage} world units; constants {EngageDistancePatch.Describe(eChimps.CHIMP_TYPE_XBOWMAN)}");
        }

        private void NextXbowRung()
        {
            var units = GameUnitManagerAPI.Instance;
            if (_xShooter > 0) { DeleteProjectilesOf(new[] { _xShooter }); units.DeleteUnitSafe(_xShooter); }
            if (_xTarget > 0) units.DeleteUnitSafe(_xTarget);
            _xShooter = _xTarget = 0;
            if (++_xRung >= _xbow[_xIdx].Ladder.Length)
            {
                _xRung = 0;
                if (++_xIdx >= _xbow.Length) return;
                ApplyXbowPhase();
            }
            int d = _xbow[_xIdx].Ladder[_xRung];
            _xShooter = Spawn(eChimps.CHIMP_TYPE_XBOWMAN, XbowX, XbowY, 1);
            _xTarget = Spawn(eChimps.CHIMP_TYPE_PIKEMAN, XbowX + d, XbowY, 2);
            Pin(_xTarget);
            _xTargetHp = units.GetCurrentHealth(_xTarget);
            var at = units.GetCurrentLocalTilePosition(_xShooter);
            _xStartX = at.X; _xStartY = at.Y;
            _xMoved = 0; _xShotTick = -1;
            _xTick = global::Director.instance.getSimTickCount();
            SnapshotProjectiles();
        }

        private bool XbowTick(float now)
        {
            if (_xIdx >= _xbow.Length) return FinishXbow();
            var units = GameUnitManagerAPI.Instance;
            int tick = global::Director.instance.getSimTickCount();
            var s = units.GetCurrentLocalTilePosition(_xShooter);
            _xMoved = Math.Max(_xMoved, Math.Max(Math.Abs(s.X - _xStartX), Math.Abs(s.Y - _xStartY)));
            if (_xShotTick < 0)
            {
                bool shot = NewShots(_xShooter, ProjectileType.CrossbowBolt, null) > 0;
                if (shot || units.GetCurrentHealth(_xTarget) < _xTargetHp)
                {
                    var t = units.GetCurrentLocalTilePosition(_xTarget);
                    _xShotTick = tick;
                    _xShotDistance = Math.Sqrt((double)(t.X - s.X) * (t.X - s.X) + (double)(t.Y - s.Y) * (t.Y - s.Y));
                }
            }
            bool over = tick - _xTick >= XbowRungTicks || (_xShotTick >= 0 && tick - _xShotTick >= 30);
            if (!over) return false;
            int d = _xbow[_xIdx].Ladder[_xRung];
            string outcome = _xShotTick >= 0
                ? (_xMoved > 0 ? $"walked {_xMoved} tiles, then shot from {_xShotDistance:0.#} tiles" : $"shot from where it stood after {_xShotTick - _xTick} ticks")
                : (_xMoved > 0 ? $"walked {_xMoved} tiles toward it, no shot in {XbowRungTicks} ticks" : $"ignored it (no shot, did not move) for {XbowRungTicks} ticks");
            _xbow[_xIdx].Rungs.Add($"{d}: {outcome}");
            NextXbowRung();
            return false;
        }

        private bool FinishXbow()
        {
            UnitRanges.ResetAttackRange(eChimps.CHIMP_TYPE_XBOWMAN);
            EngageDistancePatch.ClearEngage(eChimps.CHIMP_TYPE_XBOWMAN);
            foreach (var x in _xbow)
                Note($"Crossbowman {(x.Attack > 0 ? $"AttackRange {x.Attack} / EngageRange {x.Engage}" : "game ranges (control)")}, idle, enemy Pikeman at (tiles): " + string.Join("; ", x.Rungs));
            Check("Crossbowman with game ranges shoots an enemy 40 tiles away (control)", _xbow[0].Rungs.Count == 1 && _xbow[0].Rungs[0].Contains("shot"), string.Join("; ", _xbow[0].Rungs));
            var low = _xbow[1];
            Check("Crossbowman EngageRange 58 < AttackRange 62: measured at every rung", low.Rungs.Count == XbowLadder.Length, string.Join("; ", low.Rungs));
            return true;
        }

        // ===================================================
        // Game record fields (offsets as in CrusaderDE.dll)
        // ===================================================

        private static unsafe byte* UnitPtr(int id, int gameOffset)
        {
            if (id <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* u)) return null;
            return (byte*)u + (gameOffset - UnitToSe);
        }

        private static unsafe byte* BuildingPtr(int id, int gameOffset)
        {
            if (id <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* b)) return null;
            return (byte*)b + (gameOffset - BuildingToSe);
        }

        private static unsafe int GU16(int id, int off) { byte* p = UnitPtr(id, off); return p == null ? int.MinValue : *(short*)p; }
        private static unsafe void SetGU16(int id, int off, short v) { byte* p = UnitPtr(id, off); if (p != null) *(short*)p = v; }
        private static unsafe void SetGU32(int id, int off, int v) { byte* p = UnitPtr(id, off); if (p != null) *(int*)p = v; }
        private static unsafe void SetGU8(int id, int off, byte v) { byte* p = UnitPtr(id, off); if (p != null) *p = v; }
        private static unsafe int GU32(int id, int off) { byte* p = UnitPtr(id, off); return p == null ? int.MinValue : *(int*)p; }
        private static unsafe int GB16(int id, int off) { byte* p = BuildingPtr(id, off); return p == null ? int.MinValue : *(short*)p; }
        private static unsafe int GB32(int id, int off) { byte* p = BuildingPtr(id, off); return p == null ? int.MinValue : *(int*)p; }
    }
}
