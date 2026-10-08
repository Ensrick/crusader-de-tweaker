// Tests/HeadlessSelfTest.Healing.cs
//
// PURPOSE: Windowless self-test stages for GitLab #4 (part of HeadlessSelfTest, started after the knight stage):
//   A. ["Apothecary Healing"] (Config/Core/ApothecaryHealing.cs) on a real apothecary and real units:
//      - the settings the runner wrote into the GameplaySettings file are read back exactly;
//      - an apothecary for player 1 (placed through the game's own build routine, CreatePrefab), and pinned
//        Spearmen: A (player 1, 4 tiles from the walls, 47% health), B (player 1, 14 tiles away, 50%),
//        C (player 2, 4 tiles away, 50%), D (player 1, 4 tiles away, full health, then hit in melee by C
//        through the game's melee damage function when healing starts);
//      - gates: an apothecary without its worker (NeedsWorker = true) and a switched-off apothecary heal nobody;
//      - then only A and D heal: A by exactly HealPercent of its max health per pass, every IntervalTicks,
//        capped at max (47 -> 57 -> ... -> 97 -> 100); D only once OutOfCombatTicks have passed since the hit;
//        B (out of range) and C (other player) never; health percentage and health-bar fields match the game's
//        own heal formula (RVA 0x17491A);
//      - HealPercent / HealHitPoints back to -1 = no healing at all.
//   B. BedouinHealMultiplier = 0 (runner writes it into the multipliers .cfg): the Bedouin heal table is 0 for
//      every unit, and a Bedouin healer next to a wounded Spearman heals 0 per heal; with the table set back
//      to 10 the same healer heals 10 per heal.
//   The range stage later also checks that real projectile hits stamp combat (shooter and target).
//
// IMPORTANT FOR AI AGENTS: keep the constants in sync with scripts/test_headless.ps1.
//
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Tomlyn.Model;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        // Written into the GameplaySettings file by scripts/test_headless.ps1 (["Apothecary Healing"]).
        internal const int HealTestPercent = 10, HealTestRadius = 8, HealTestInterval = 50, HealTestOutOfCombat = 200;
        // Written into CrusaderDETweaker_GlobalMultipliers.cfg by the runner.
        internal const float BedouinHealTestMultiplier = 0f;

        private const int ApoX = 390, ApoY = 370, HealStepTicks = 3000;
        private const eChimps HealSubject = eChimps.CHIMP_TYPE_SPEARMAN;

        private int _apoStep, _apoId, _apoStepTick, _apoPassMark, _apoLogMark, _apoHitTick = -1;
        private int _hA, _hB, _hC, _hD, _hStartA, _hStartB, _hStartC, _hMax;
        private int _fMinX, _fMinY, _fMaxX, _fMaxY;
        private ApothecaryHealing.Settings _fileHeal;

        private int _bedStep, _bedHealer, _bedTarget, _bedStepTick, _bedEvents, _bedHpStart;
        private IDisposable _bedSub;

        // ===================================================
        // A. Apothecary healing
        // ===================================================

        private unsafe void StartApothecaryTest()
        {
            var units = GameUnitManagerAPI.Instance;
            var buildings = GameBuildingManagerAPI.Instance;

            Config.Toml.ConfigLoader.ApplyApothecaryHealingFromFile("selftest");
            _fileHeal = ApothecaryHealing.Current;
            Check("Apothecary Healing settings from the GameplaySettings file",
                _fileHeal.HealBasisPoints == HealTestPercent * 100 && _fileHeal.HealHitPoints == -1 && _fileHeal.RadiusTiles == HealTestRadius
                && _fileHeal.IntervalTicks == HealTestInterval && _fileHeal.OutOfCombatTicks == HealTestOutOfCombat && !_fileHeal.NeedsWorker,
                $"read: {_fileHeal}");
            ApothecaryHealing.Configure(null, "selftest setup");
            ApothecaryHealing.RecordHeals = true;   // stays on: the range stage checks projectile combat stamps
            lock (ApothecaryHealing.HealLog) ApothecaryHealing.HealLog.Clear();

            Note($"Map size {GameTileManagerAPI.Instance.GetCurrentMapSize()}; apothecary at {ApoX},{ApoY} inside the map: {GameTileManagerAPI.Instance.IsTileInsideMapBounds(ApoX, ApoY)}");
            int scale = BuildingScales.GetScale(eMappers.MAPPER_HEALER);
            long placed = buildings.CreatePrefab(1, ApoX, ApoY, eMappers.MAPPER_HEALER, scale, 0, true, true);
            var found = new List<int>();
            buildings.GetAllBuildings(found, null, eStructs.STRUCT_HEALER, PlayerRelationship.Self, 1);
            _apoId = 0;
            foreach (int id in found)
            {
                if (buildings.TryGetBuildingById(id, out GameBuilding* b) && b->r_AliveState != AliveState.MarkedForDeletion
                    && b->r_TilePositionXBegin == ApoX && b->r_TilePositionYBegin == ApoY)
                {
                    _apoId = id;
                    break;
                }
            }
            if (_apoId <= 0)
            {
                Check("Apothecary placed (MAPPER_HEALER -> STRUCT_HEALER)", false, $"CreatePrefab(scale {scale}) returned {placed}; player 1 STRUCT_HEALER ids: {string.Join(",", found)}");
                _apoStep = 99;
                return;
            }
            buildings.TryGetBuildingById(_apoId, out GameBuilding* apo);
            var begin = buildings.GetBeginPosition(_apoId);
            var end = buildings.GetEndPosition(_apoId);
            _fMinX = Math.Min(begin.X, end.X); _fMaxX = Math.Max(begin.X, end.X);
            _fMinY = Math.Min(begin.Y, end.Y); _fMaxY = Math.Max(begin.Y, end.Y);
            Check("Apothecary placed (MAPPER_HEALER -> STRUCT_HEALER)", apo->r_BuildingType == eStructs.STRUCT_HEALER && apo->r_PlayerIdOwner == 1,
                $"building {_apoId}: type {apo->r_BuildingType}, owner {apo->r_PlayerIdOwner}, state {apo->r_AliveState}, footprint {_fMinX},{_fMinY} - {_fMaxX},{_fMaxY} " +
                $"(SE scale {scale}), workers required {apo->r_TotalWorkersRequired} / current {apo->r_TotalCurrentWorkers}, switched off {apo->r_IsSleeping}");

            int cx = (_fMinX + _fMaxX) / 2, cy = (_fMinY + _fMaxY) / 2;
            _hA = Spawn(HealSubject, _fMinX - 4, cy);
            _hB = Spawn(HealSubject, _fMinX - 14, cy);
            _hC = Spawn(HealSubject, _fMaxX + 4, cy, owner: 2);
            _hD = Spawn(HealSubject, cx, _fMaxY + 4);
            foreach (int id in new[] { _hA, _hB, _hC, _hD }) Pin(id);
            _hMax = units.GetMaxHealth(_hA);
            units.SetCurrentHealth(_hA, _hMax * 47 / 100);
            units.SetCurrentHealth(_hB, _hMax / 2);
            units.SetCurrentHealth(_hC, _hMax / 2);
            Note($"Spearmen (pinned): A {_hA} player 1 at {Pos(_hA)} ({Dist(_hA)} tiles from the walls), B {_hB} player 1 at {Pos(_hB)} ({Dist(_hB)} tiles), " +
                 $"C {_hC} player 2 at {Pos(_hC)} ({Dist(_hC)} tiles), D {_hD} player 1 at {Pos(_hD)} ({Dist(_hD)} tiles); max health {_hMax}; " +
                 $"health A {units.GetCurrentHealth(_hA)}, B {units.GetCurrentHealth(_hB)}, C {units.GetCurrentHealth(_hC)}, D {units.GetCurrentHealth(_hD)}");

            // Gate 1: the file's settings, but NeedsWorker = true; the editor map has no peasants, so no worker.
            var gate = _fileHeal.Clone();
            gate.NeedsWorker = true;
            ApothecaryHealing.Configure(gate, "selftest gate: needs worker");
            BeginHealStep(1);
        }

        private void BeginHealStep(int step)
        {
            _apoStep = step;
            _apoStepTick = global::Director.instance.getSimTickCount();
            _apoPassMark = ApothecaryHealing.PassCount;
            _apoLogMark = HealRecords().Count;
        }

        private double Dist(int unitId)
        {
            var p = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(unitId);
            return Math.Sqrt(ApothecaryHealing.DistanceSquaredToFootprint(p.X, p.Y, _fMinX, _fMinY, _fMaxX, _fMaxY));
        }

        private unsafe void Pin(int unitId)
        {
            var units = GameUnitManagerAPI.Instance;
            if (_speedOffset == 0 && units.TryGetUnitById(unitId, out GameUnit* u))
                _speedOffset = (int)((byte*)&u->r_CurrentSpeed2 - (byte*)u);
            units.SetSpeed(unitId, 30000);
            WriteUnitField16(unitId, _speedOffset, 30000);
        }

        private static List<ApothecaryHealing.HealRecord> HealRecords()
        {
            lock (ApothecaryHealing.HealLog) return ApothecaryHealing.HealLog.ToList();
        }

        /// <summary>One frame of the apothecary stage; true when it is finished.</summary>
        private unsafe bool ApothecaryTick()
        {
            var units = GameUnitManagerAPI.Instance;
            int tick = global::Director.instance.getSimTickCount();
            int passes = ApothecaryHealing.PassCount - _apoPassMark;
            if (_apoStep == 99) return true;

            if (_apoStep == 1 || _apoStep == 2)
            {
                if (passes < 2)
                {
                    if (tick - _apoStepTick > 10 * HealTestInterval)
                    {
                        Check($"Heal passes run (step {_apoStep})", false, $"{passes} pass(es) in {tick - _apoStepTick} ticks (the Script Extender's game tick event did not drive the heal pass)");
                        return EndApothecaryTest();
                    }
                    return false;
                }
                var records = HealRecords();
                if (_apoStep == 1)
                {
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(_apoId, out GameBuilding* apo);
                    Check("Apothecary without its worker does not heal (NeedsWorker = true)", records.Count == 0,
                        $"{passes} passes, {records.Count} heals; workers required {apo->r_TotalWorkersRequired}, current {apo->r_TotalCurrentWorkers}; A health {units.GetCurrentHealth(_hA)}");
                    ApothecaryHealing.Configure(_fileHeal, "selftest gate: switched off");
                    GameBuildingManagerAPI.Instance.SetSleeping(_apoId, true);
                    BeginHealStep(2);
                    return false;
                }
                Check("Switched-off apothecary does not heal", records.Count == 0, $"{passes} passes, {records.Count} heals; A health {units.GetCurrentHealth(_hA)}");
                GameBuildingManagerAPI.Instance.SetSleeping(_apoId, false);

                // Healing on (the file's settings). D is hit now, through the game's melee damage function.
                _hStartA = units.GetCurrentHealth(_hA);
                _hStartB = units.GetCurrentHealth(_hB);
                _hStartC = units.GetCurrentHealth(_hC);
                int dBefore = units.GetCurrentHealth(_hD);
                units.DamageUnitMelee(_hC, _hD, _hMax / 4);
                _apoHitTick = ApothecaryHealing.LastCombatTick(_hD);
                Check("A melee hit stamps combat on both units", _apoHitTick >= 0 && ApothecaryHealing.LastCombatTick(_hC) == _apoHitTick,
                    $"C {_hC} hit D {_hD} at tick {tick}: D health {dBefore} -> {units.GetCurrentHealth(_hD)}; stamps D {_apoHitTick}, C {ApothecaryHealing.LastCombatTick(_hC)}");
                BeginHealStep(3);
                return false;
            }

            if (_apoStep == 3)
            {
                var records = HealRecords();
                bool aFull = units.GetCurrentHealth(_hA) >= _hMax, dHealed = records.Any(r => r.UnitId == _hD);
                if ((!aFull || !dHealed) && tick - _apoStepTick < HealStepTicks) return false;
                ReportHealing(records, tick);

                // Back to -1 = off: A wounded again must stay as it is.
                ApothecaryHealing.Configure(ApothecaryHealing.Parse(new TomlTable { ["HealPercent"] = -1L, ["HealHitPoints"] = -1L }, _ => { }), "selftest off");
                units.SetCurrentHealth(_hA, _hMax * 47 / 100);
                _hStartA = units.GetCurrentHealth(_hA);
                BeginHealStep(4);
                return false;
            }

            if (_apoStep == 4)
            {
                if (tick - _apoStepTick < 3 * HealTestInterval + 10) return false;
                int healsAfter = HealRecords().Count - _apoLogMark;   // not by tick: the last heal pass may share the step's start tick
                Check("HealPercent / HealHitPoints = -1: no healing (game behaviour)", healsAfter == 0 && units.GetCurrentHealth(_hA) == _hStartA && passes == 0,
                    $"{tick - _apoStepTick} ticks: {passes} passes, {healsAfter} heals, A health {_hStartA} -> {units.GetCurrentHealth(_hA)}");
                return EndApothecaryTest();
            }
            return true;
        }

        private unsafe void ReportHealing(List<ApothecaryHealing.HealRecord> records, int tick)
        {
            var units = GameUnitManagerAPI.Instance;
            int amount = ApothecaryHealing.HealAmount(_hMax, _fileHeal);
            var a = records.Where(r => r.UnitId == _hA).ToList();
            var d = records.Where(r => r.UnitId == _hD).ToList();
            string Steps(List<ApothecaryHealing.HealRecord> rs) => string.Join(", ", rs.Select(r => $"t{r.Tick}: {r.Before}->{r.After}"));

            bool aSteps = a.Count > 0 && a[0].Before == _hStartA && a.Last().After == _hMax
                && a.Take(a.Count - 1).All(r => r.After - r.Before == amount) && a.Last().After - a.Last().Before <= amount
                && a.Zip(a.Skip(1), (x, y) => x.After == y.Before).All(ok => ok);
            Check($"Wounded soldier near its apothecary heals {HealTestPercent}% of max health per heal, capped at max",
                aSteps && units.GetCurrentHealth(_hA) == _hMax,
                $"A: {a.Count} heals of {amount} (max {_hMax}): {Steps(a)}; health now {units.GetCurrentHealth(_hA)}");
            var gaps = a.Zip(a.Skip(1), (x, y) => y.Tick - x.Tick).ToList();
            Check($"One heal every {HealTestInterval} ticks", gaps.Count > 0 && gaps.All(g => g == HealTestInterval), $"gaps between A's heals: {string.Join(", ", gaps)} ticks");

            Check($"Unit {Dist(_hB):0.#} tiles away (radius {HealTestRadius}) is not healed", records.All(r => r.UnitId != _hB) && units.GetCurrentHealth(_hB) == _hStartB,
                $"B health {_hStartB} -> {units.GetCurrentHealth(_hB)}");
            Check("Another player's unit next to the apothecary is not healed", records.All(r => r.UnitId != _hC) && units.GetCurrentHealth(_hC) == _hStartC,
                $"C (player 2) health {_hStartC} -> {units.GetCurrentHealth(_hC)}");
            int healedDuringDelay = a.Count(r => r.Tick >= _apoHitTick && r.Tick - _apoHitTick < HealTestOutOfCombat);
            Check($"A unit hit in melee waits {HealTestOutOfCombat} ticks before it heals",
                _apoHitTick >= 0 && d.Count > 0 && d[0].Tick - _apoHitTick >= HealTestOutOfCombat && d[0].Tick - _apoHitTick < HealTestOutOfCombat + HealTestInterval
                && healedDuringDelay > 0 && d.All(r => r.After - r.Before == amount || r.After == _hMax),
                $"hit at tick {_apoHitTick}; D's heals: {Steps(d)}; meanwhile A healed {healedDuringDelay} time(s)");
            Check("Each unit heals at most once per pass", records.GroupBy(r => new { r.Tick, r.UnitId }).All(g => g.Count() == 1),
                $"{records.Count} heals in total, units {string.Join(",", records.Select(r => r.UnitId).Distinct())}");

            // Health percentage and health-bar blocks as the game's own heal writes them (RVA 0x17491A).
            string fields = "";
            bool fieldsOk = true;
            foreach (int id in new[] { _hA, _hD })
            {
                if (!units.TryGetUnitById(id, out GameUnit* u)) { fieldsOk = false; continue; }
                int hp = (int)u->r_CurrentHealth, max = (int)u->r_MaxHealth, pct = u->r_CurrentHealthPercentage, blocks = *(ushort*)&u->r_HealthBarBlocks;
                int wantPct = max == 0 ? 100 : hp * 100 / max, wantBlocks = hp >= max ? 10 : wantPct / 10;
                fieldsOk &= pct == wantPct && blocks == wantBlocks && max == _hMax;
                fields += $"unit {id}: health {hp}/{max}, percentage {pct} (want {wantPct}), bar blocks {blocks} (want {wantBlocks}); ";
            }
            Check("Health percentage and health bar follow the heal", fieldsOk, fields +
                  $"SE offsets: health +0x{FieldOffset(_hA, 0)}, percentage +0x{FieldOffset(_hA, 1)}, bar +0x{FieldOffset(_hA, 2)} (game +0xA20 / +0x92C / +0x690 minus 0x65C = 0x3C4 / 0x2D0 / 0x34)");
            Note($"Apothecary heal stage ended at tick {tick} ({tick - _apoStepTick} ticks after healing started)");
        }

        private static unsafe string FieldOffset(int unitId, int which)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u)) return "?";
            byte* f = which == 0 ? (byte*)&u->r_CurrentHealth : which == 1 ? (byte*)&u->r_CurrentHealthPercentage : (byte*)&u->r_HealthBarBlocks;
            return ((int)(f - (byte*)u)).ToString("X");
        }

        private bool EndApothecaryTest()
        {
            ApothecaryHealing.Configure(null, "selftest end");
            var units = GameUnitManagerAPI.Instance;
            foreach (int id in new[] { _hA, _hB, _hC, _hD }) if (id > 0) units.KillUnit(id);
            if (_apoId > 0) GameBuildingManagerAPI.Instance.DeleteBuildingSafe(_apoId);
            _apoStep = 99;
            return true;
        }

        // ===================================================
        // B. Bedouin heal multiplier 0
        // ===================================================

        private void StartBedouinHealTest()
        {
            var units = GameUnitManagerAPI.Instance;
            var modifiable = global::CrusaderDETweaker.Config.DamageMatrix.Core.UnitMatrixHelper.GetModifiableUnits().ToList();
            var nonZero = modifiable.Where(t => units.GetBedouinHeal(t) != 0).ToList();
            Check($"BedouinHealMultiplier = {BedouinHealTestMultiplier}: Bedouin heal table is 0 for every unit", modifiable.Count > 0 && nonZero.Count == 0,
                $"{modifiable.Count} unit types, non-zero: {(nonZero.Count == 0 ? "none" : string.Join(", ", nonZero.Select(t => $"{t}={units.GetBedouinHeal(t)}")))}");

            _bedTarget = Spawn(HealSubject, 420, 360);
            Pin(_bedTarget);
            units.SetCurrentHealth(_bedTarget, units.GetMaxHealth(_bedTarget) / 2);
            _bedHealer = Spawn(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 422, 360);
            _bedSub = UnitR3EventHooks.OnUnitHealByBedouinHealer.Observable
                .Where(args => args.Phase == EventHookPhase.Pre && args.HealedUnitId == _bedTarget)
                .Subscribe(_ => _bedEvents++);
            Note($"Bedouin healer {_bedHealer} at {Pos(_bedHealer)} next to wounded Spearman {_bedTarget} (pinned) at {Pos(_bedTarget)}, health {units.GetCurrentHealth(_bedTarget)}/{units.GetMaxHealth(_bedTarget)}");
            BeginBedouinStep(1);
        }

        private void BeginBedouinStep(int step)
        {
            _bedStep = step;
            _bedStepTick = global::Director.instance.getSimTickCount();
            _bedEvents = 0;
            _bedHpStart = GameUnitManagerAPI.Instance.GetCurrentHealth(_bedTarget);
        }

        /// <summary>One frame of the Bedouin stage; true when it is finished.</summary>
        private bool BedouinHealTick()
        {
            var units = GameUnitManagerAPI.Instance;
            int ticks = global::Director.instance.getSimTickCount() - _bedStepTick;
            if (_bedEvents < 3 && ticks < 600) return false;
            int hp = units.GetCurrentHealth(_bedTarget), gained = hp - _bedHpStart;
            if (_bedStep == 1)
            {
                if (_bedEvents == 0)
                    Note($"SKIP Bedouin healer heals 0 with BedouinHealMultiplier 0: the healer did not heal within {ticks} ticks in the editor map, so it cannot be measured (table check above still applies)");
                else
                    Check($"BedouinHealMultiplier 0: a Bedouin healer heals nothing", gained == 0, $"{_bedEvents} heals in {ticks} ticks, health {_bedHpStart} -> {hp}");
                units.SetBedouinHeal(HealSubject, 10);   // control: the game's value
                BeginBedouinStep(2);
                return false;
            }
            if (_bedEvents == 0)
                Note($"SKIP Bedouin heal control (table 10): no heal within {ticks} ticks");
            else
                Check("Control: with the table at 10 the same healer heals 10 per heal", gained == 10 * _bedEvents, $"{_bedEvents} heals in {ticks} ticks, health {_bedHpStart} -> {hp}");
            _bedSub?.Dispose();
            units.KillUnit(_bedHealer);
            units.KillUnit(_bedTarget);
            return true;
        }
    }
}
