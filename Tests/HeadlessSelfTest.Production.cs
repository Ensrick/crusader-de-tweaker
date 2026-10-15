// Tests/HeadlessSelfTest.Production.cs
//
// PURPOSE: Real Quarry and Mill trips for the GoodYieldMultiplier of the Quarry Grunt and the Miller (Nexus report
//          2026-10-13: both at 50, still 12 stone and 1 flour per trip). Part of the economy stages
//          (Tests/HeadlessSelfTest.Economy.cs); runs while the woodcutter window runs, in the same game type 2 window.
//
// WHAT IT DOES:
//   Setup (with the woodcutter's real delivery): a Quarry, an Ox Tether and a Mill east of the fixture Stockpile, 40
//   wheat in the Stockpile, 8 more peasants. The runner's Units file sets both multipliers to 3, so the file path must
//   have switched the Miller's code patch on (Config/Core/MillerFlourPatch.cs).
//   Then three phases each for the quarry and the mill, one measured trip per phase (the first trip loaded after the
//   phase started; trips of other workers that straddle a phase change are not counted):
//     1. game value (multiplier 1; the Miller's code is the game's),
//     2. the 2.11.0 code at 3 (quarry: multiplier looked up by the ox's own type; mill: patch off),
//     3. the fix at 3.
//   Quarry trip: the ox's load = its carried count (+0x9E0) right after the Quarry Grunt's yield call for it; stones
//   delivered = the Script Extender's OnQuarryOxDropOffStone events of that ox (one per stone, after each goods add)
//   until its count is 0; the player's stone must grow by the same amount. Mill trip: the yield at the wheat pickup,
//   then OnMillerDropOffFlour events of that miller (one per bag, before each goods add) until its count is 0; the
//   player's flour must grow by the drop-off events of all millers in that time.
//
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Core;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        private const int ProductionStage = 20;
        internal const float QuarryGruntYield = 3f, MillerYield = 3f;   // written into the Units file by the runner
        private const int ProductionTicks = 25000;
        private const int QuarryX = 386, QuarryY = 398, TetherX = 386, TetherY = 405, MillX = 393, MillY = 398;

        private sealed class Trip
        {
            internal int Unit, Load = -1, Yield, Deposits, OthersDeposits, GoodBefore, Delta, StartTick, EndTick;
            internal bool Done;
        }

        private sealed class ProductionLine
        {
            internal string Name;
            internal eChimps Worker;      // the multiplier's worker type
            internal eChimps YieldUnit;   // the type the yield call is made for
            internal eGoods Good;
            internal int Phase = -1, PhaseTick;
            internal Trip Current;
            internal readonly Trip[] Trips = new Trip[3];
            internal readonly string[] Detail = { "not run", "not run", "not run" };
            internal readonly Dictionary<int, int> Deposits = new Dictionary<int, int>();
            internal int AllDeposits;
        }

        private static readonly string[] ProductionPhases = { "game value (multiplier 1)", "2.11.0 code at 3", "fix at 3" };
        private ProductionLine _quarry, _mill;
        private readonly List<IDisposable> _prodWatch = new List<IDisposable>();
        private int _prodTick0 = -1, _quarryId, _tetherId, _millId;
        private float _prodNoteTime;
        private bool _prodReported;

        /// <summary>Places the quarry, tether and mill and starts phase 1 (inside the woodcutter window's setup).</summary>
        private void ProductionSetup()
        {
            Check("GoodYieldMultiplier for the Quarry Grunt and the Miller from the Units file in effect (the Miller's code patch on)",
                Math.Abs(GoodYield.Get(eChimps.CHIMP_TYPE_QUARRY_GRUNT) - QuarryGruntYield) < 1e-3f && Math.Abs(GoodYield.Get(eChimps.CHIMP_TYPE_MILLER) - MillerYield) < 1e-3f
                && MillerFlourPatch.Active,
                $"Quarry Grunt {GoodYield.Get(eChimps.CHIMP_TYPE_QUARRY_GRUNT)}, Miller {GoodYield.Get(eChimps.CHIMP_TYPE_MILLER)}; Miller code {MillerFlourPatch.Describe()}");

            // Boulders under the quarry, painted the way the editor does (EditorDirector.changeLand -> PlaceMapperItem).
            int boulders = (int)eMappers.MAPPER_BOULDERS;
            for (int dx = 0; dx < 6; dx += 2)
                for (int dy = 0; dy < 6; dy += 2)
                {
                    global::EngineInterface.StartMapperItem(boulders);
                    global::EngineInterface.PlaceMapperItem(boulders, QuarryX + dx, QuarryY + dy, 2, 1, false, false, 1);
                }
            global::MainControls.instance?.StopAllPlacement();
            _quarryId = Prefab(eMappers.MAPPER_QUARRY, QuarryX, QuarryY, BuildingScales.GetScale(eMappers.MAPPER_QUARRY), eStructs.STRUCT_QUARRY, free: true, bypass: true);
            _tetherId = Prefab(eMappers.MAPPER_OXENBASE, TetherX, TetherY, BuildingScales.GetScale(eMappers.MAPPER_OXENBASE), eStructs.STRUCT_OXEN_BASE, free: true, bypass: true);
            _millId = Prefab(eMappers.MAPPER_MILL, MillX, MillY, BuildingScales.GetScale(eMappers.MAPPER_MILL), eStructs.STRUCT_MILL, free: true, bypass: true);
            var p = GamePlayerManagerAPI.Instance;
            for (int i = 0; i < 6 && p.GetGoodAmount(1, eGoods.STORED_RAW_WHEAT) < 40; i++) p.TryAddGood(1, eGoods.STORED_RAW_WHEAT, 40 - p.GetGoodAmount(1, eGoods.STORED_RAW_WHEAT));
            for (int i = 0; i < 8; i++) Spawn(eChimps.CHIMP_TYPE_PEASANT, KeepX + i, KeepY + 11);
            Note($"Production: Quarry {_quarryId} at {QuarryX},{QuarryY}, Ox Tether {_tetherId} at {TetherX},{TetherY}, Mill {_millId} at {MillX},{MillY}; " +
                 $"player 1 wheat {p.GetGoodAmount(1, eGoods.STORED_RAW_WHEAT)}, stone {p.GetGoodAmount(1, eGoods.STORED_STONE_BLOCKS)}, flour {p.GetGoodAmount(1, eGoods.STORED_FLOUR)}; 8 more peasants");

            _quarry = new ProductionLine { Name = "Quarry", Worker = eChimps.CHIMP_TYPE_QUARRY_GRUNT, YieldUnit = eChimps.CHIMP_TYPE_QUARRY_OX, Good = eGoods.STORED_STONE_BLOCKS };
            _mill = new ProductionLine { Name = "Mill", Worker = eChimps.CHIMP_TYPE_MILLER, YieldUnit = eChimps.CHIMP_TYPE_MILLER, Good = eGoods.STORED_FLOUR };
            _prodWatch.Add(UnitR3EventHooks.OnCalculateBonusYield.Observable.Where(a => a.Phase == EventHookPhase.Post).Subscribe(OnProductionYield));
            _prodWatch.Add(UnitR3EventHooks.OnQuarryOxDropOffStone.Observable.Subscribe(a => CountDeposit(_quarry, a.UnitId)));
            _prodWatch.Add(UnitR3EventHooks.OnMillerDropOffFlour.Observable.Where(a => a.Phase == EventHookPhase.Pre).Subscribe(a => CountDeposit(_mill, a.UnitId)));
            _prodTick0 = global::Director.instance.getSimTickCount();
            NextProductionPhase(_quarry);
            NextProductionPhase(_mill);
        }

        private void CountDeposit(ProductionLine line, int unit)
        {
            if (line == null || line.Phase < 0) return;
            line.Deposits.TryGetValue(unit, out int n);
            line.Deposits[unit] = n + 1;
            line.AllDeposits++;
        }

        /// <summary>A yield call for the line's unit type starts that phase's measured trip (the first one after the phase began).</summary>
        private void OnProductionYield(UnitCalculateBonusYieldEventArgs a)
        {
            try
            {
                if (_directYieldUnits.Contains(a.UnitId)) return;
                eChimps type = GameUnitManagerAPI.Instance.GetType(a.UnitId);
                foreach (ProductionLine line in new[] { _quarry, _mill })
                {
                    if (line == null || line.Phase < 0 || line.Phase >= ProductionPhases.Length || type != line.YieldUnit || line.Current != null) continue;
                    line.Deposits.TryGetValue(a.UnitId, out int before);
                    line.Current = new Trip
                    {
                        Unit = a.UnitId, Yield = (int)a.ReturnValue, Deposits = -before, OthersDeposits = -(line.AllDeposits - before),
                        GoodBefore = GamePlayerManagerAPI.Instance.GetGoodAmount(1, line.Good), StartTick = global::Director.instance.getSimTickCount(),
                    };
                    Note($"{line.Name} phase '{ProductionPhases[line.Phase]}': trip of unit {a.UnitId} ({type}) loaded at tick {line.Current.StartTick}: " +
                         $"yield call base {a.GoodAmount}, bonus flag {a.b50PercentBonus}, returned {a.ReturnValue}");
                }
            }
            catch (Exception ex) { Note("production yield watch: " + ex.Message); }
        }

        /// <summary>Per tick: completes trips and moves the lines through their phases; true when both lines are done.</summary>
        private bool ProductionTick(float now)
        {
            if (_quarry == null) return true;
            int tick = global::Director.instance.getSimTickCount();
            foreach (ProductionLine line in new[] { _quarry, _mill })
            {
                if (line.Phase >= ProductionPhases.Length) continue;
                if (line == _quarry && line.Current == null && tick - line.PhaseTick >= GruntWaitTicks) LoadOxLikeTheGrunt(tick);
                Trip t = line.Current;
                if (t != null)
                {
                    int carried = ReadNativeUnitField16(t.Unit, 0x9E0);
                    if (t.Load < 0 && carried > 0) t.Load = carried;   // the count the game stored (the ox's load, the miller's wheat-time count)
                    line.Deposits.TryGetValue(t.Unit, out int dep);
                    if (dep + t.Deposits > 0 && carried == 0)
                    {
                        t.Deposits += dep;
                        t.OthersDeposits += line.AllDeposits - dep;
                        t.Delta = GamePlayerManagerAPI.Instance.GetGoodAmount(1, line.Good) - t.GoodBefore;
                        t.EndTick = tick;
                        t.Done = true;
                        line.Trips[line.Phase] = t;
                        line.Detail[line.Phase] = $"unit {t.Unit}: yield returned {t.Yield}, carried {t.Load}, delivered {t.Deposits} (drop-off events) in {t.EndTick - t.StartTick} ticks; " +
                                                  $"player {line.Good} +{t.Delta} (drop-offs of other workers meanwhile {t.OthersDeposits})";
                        Note($"{line.Name} phase '{ProductionPhases[line.Phase]}': {line.Detail[line.Phase]}");
                        NextProductionPhase(line);
                    }
                    else if (carried == int.MinValue)
                    {
                        Note($"{line.Name} phase '{ProductionPhases[line.Phase]}': unit {t.Unit} is gone; waiting for another trip");
                        line.Current = null;
                    }
                }
            }
            if (now - _prodNoteTime >= 30f)
            {
                _prodNoteTime = now;
                var p = GamePlayerManagerAPI.Instance;
                Note($"production progress: tick {tick} (+{tick - _prodTick0}); quarry phase {_quarry.Phase} trip {(_quarry.Current != null ? _quarry.Current.Unit + " carried " + ReadNativeUnitField16(_quarry.Current.Unit, 0x9E0) : "-")}, " +
                     $"mill phase {_mill.Phase} trip {(_mill.Current != null ? _mill.Current.Unit + " carried " + ReadNativeUnitField16(_mill.Current.Unit, 0x9E0) : "-")}; " +
                     $"stone {p.GetGoodAmount(1, eGoods.STORED_STONE_BLOCKS)}, flour {p.GetGoodAmount(1, eGoods.STORED_FLOUR)}, wheat {p.GetGoodAmount(1, eGoods.STORED_RAW_WHEAT)}; " +
                     $"ox drop-offs {_quarry.AllDeposits}, miller drop-offs {_mill.AllDeposits}; workers: quarry {Workers(_quarryId)}, ox tether {Workers(_tetherId)}, mill {Workers(_millId)}; " +
                     $"ox {OxState()}; {WorkerCensus()}");
            }
            bool done = _quarry.Phase >= ProductionPhases.Length && _mill.Phase >= ProductionPhases.Length;
            if (!done && tick - _prodTick0 < ProductionTicks) return false;
            ReportProduction(tick);
            return true;
        }

        private static unsafe string Workers(int building) =>
            building > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(building, out GameBuilding* b) ? b->r_TotalCurrentWorkers.ToString() : "-";

        /// <summary>Every alive unit type with its owner (the quarry's workers were missing in a first run).</summary>
        private string WorkerCensus()
        {
            var units = GameUnitManagerAPI.Instance;
            return "units: " + string.Join(", ", units.GetAllAliveUnits().GroupBy(id => $"{units.GetType(id).ToString().Replace("CHIMP_TYPE_", "")} p{units.GetOwner(id)}")
                .OrderBy(g => g.Key).Select(g => $"{g.Key} x{g.Count()}"));
        }

        // In the editor map (run as a custom mission) the Quarry and the Ox Tether never got workers (runs selftest-20261014-205614
        // and -213013: the mill and the woodcutter were staffed, the other peasants left, the tether's ox was there). Then the test
        // makes the Quarry Grunt's call itself: yield(ox, 8, bonus flag 1) through the function's hooked entry, result into the
        // ox's carried count, exactly the game's RVA 0x131C8C-0x131C9B; the ox's own update then makes the trip (it leaves with
        // 8 or more, 0x132A09) and delivers to the Stockpile.
        private const int GruntWaitTicks = 1500;
        private int _gruntLoads;

        private string OxState()
        {
            var units = GameUnitManagerAPI.Instance;
            int ox = units.GetAllAliveUnits().FirstOrDefault(id => units.GetOwner(id) == 1 && units.GetType(id) == eChimps.CHIMP_TYPE_QUARRY_OX);
            return ox > 0 ? $"{ox} at {Pos(ox)} state {GU16(ox, 0x918)} carried {GU16(ox, 0x9E0)} home {GU16(ox, 0x990)}" : "none";
        }

        private unsafe void LoadOxLikeTheGrunt(int tick)
        {
            var units = GameUnitManagerAPI.Instance;
            if (Workers(_quarryId) != "0" && Workers(_quarryId) != "-") return;   // a real Quarry Grunt works: wait for its load
            int ox = units.GetAllAliveUnits().FirstOrDefault(id => units.GetOwner(id) == 1 && units.GetType(id) == eChimps.CHIMP_TYPE_QUARRY_OX);
            int oxState = GU16(ox, 0x918);
            // The ox must be home and empty: state 0 or 4 (waiting at the tether), both of which leave with 8 or more (0x132A09, 0x132C17).
            if (ox <= 0 || GU16(ox, 0x9E0) != 0 || (oxState != 0 && oxState != 4)) return;
            if (!GameCode.TryFindUnique(YieldPattern, out uint rva, out string problem)) { Note("Quarry: yield function not found: " + problem); _gruntLoads = int.MaxValue; return; }
            if (_gruntLoads++ >= 6) return;
            var yield = (YieldFunction)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer((IntPtr)((long)GameCode.ModuleBase + rva), typeof(YieldFunction));
            long load = yield(ox, 8, 1);
            SetGU16(ox, 0x9E0, (short)load);
            Note($"Quarry: no Quarry Grunt works in the editor map (quarry workers {Workers(_quarryId)}), so the test makes its load call at tick {tick}: " +
                 $"yield(ox {ox}, 8, bonus flag 1) returned {load}, stored as the ox's carried count (+0x9E0) as RVA 0x131C9B does; the ox's own update makes the trip");
        }

        /// <summary>Starts the line's next phase: sets the multiplier and the code the phase measures.</summary>
        private void NextProductionPhase(ProductionLine line)
        {
            line.Current = null;
            line.Phase++;
            if (line.Phase >= ProductionPhases.Length) { RestoreProductionCode(line); return; }
            GoodYield.Set(line.Worker, line.Phase == 0 ? 1f : line.Worker == eChimps.CHIMP_TYPE_QUARRY_GRUNT ? QuarryGruntYield : MillerYield);
            if (line.Worker == eChimps.CHIMP_TYPE_QUARRY_GRUNT) GoodYield.OldQuarryLookupForSelfTest = line.Phase == 1;
            else MillerFlourPatch.Apply(line.Phase == 2);   // 1 and the 2.11.0 code at 3: the game's drop-off; the fix: patched
            line.PhaseTick = global::Director.instance.getSimTickCount();
            Note($"{line.Name} phase '{ProductionPhases[line.Phase]}' from tick {line.PhaseTick}: {line.Worker} multiplier {GoodYield.Get(line.Worker)}" +
                 (line.Worker == eChimps.CHIMP_TYPE_MILLER ? $", Miller code {MillerFlourPatch.Describe()}" : $", ox lookup {(GoodYield.OldQuarryLookupForSelfTest ? "by the ox's own type (2.11.0)" : "Quarry Grunt (fix)")}"));
        }

        private void RestoreProductionCode(ProductionLine line)
        {
            if (line.Worker == eChimps.CHIMP_TYPE_QUARRY_GRUNT) { GoodYield.OldQuarryLookupForSelfTest = false; GoodYield.Set(line.Worker, QuarryGruntYield); }
            else { GoodYield.Set(line.Worker, MillerYield); MillerFlourPatch.Apply(true); }
        }

        private void ReportProduction(int tick)
        {
            if (_prodReported) return;
            _prodReported = true;
            foreach (IDisposable d in _prodWatch) d.Dispose();
            _prodWatch.Clear();
            foreach (ProductionLine line in new[] { _quarry, _mill })
            {
                if (line.Phase < ProductionPhases.Length) { Note($"{line.Name}: phase '{ProductionPhases[line.Phase]}' had no finished trip within {ProductionTicks} ticks ({WorkerCensus()})"); line.Phase = ProductionPhases.Length; RestoreProductionCode(line); }
                bool stone = line.Worker == eChimps.CHIMP_TYPE_QUARRY_GRUNT;
                Trip game = line.Trips[0], old = line.Trips[1], fix = line.Trips[2];
                // Every delivered unit is one drop-off event, and the player's stock grows by the drop-offs of all workers.
                foreach (int i in Enumerable.Range(0, 3).Where(i => line.Trips[i] != null))
                {
                    Trip t = line.Trips[i];
                    Check($"{line.Name} ({ProductionPhases[i]}): the player's {line.Good} grows by the drop-offs", t.Delta == t.Deposits + t.OthersDeposits,
                        $"+{t.Delta}; drop-offs of this worker {t.Deposits}, of others {t.OthersDeposits}");
                }
                string skip = $"SKIP {line.Name} trip ({{0}}): no trip finished within {ProductionTicks} ticks of the setup";
                if (game == null) Note(string.Format(skip, ProductionPhases[0]));
                else if (stone)
                    Check("Quarry, game value: the ox delivers the load the Quarry Grunt gave it (base 8)", game.Deposits == game.Load && game.Load == game.Yield && game.Load >= 8,
                        line.Detail[0]);
                else
                    Check("Mill, game value: the Miller delivers 1 flour per trip (the game's code)", game.Deposits == 1, line.Detail[0]);

                if (old == null) Note(string.Format(skip, ProductionPhases[1]));
                else if (stone)
                    Note($"Quarry, 2.11.0 code at 3 (the report): the ox delivered {old.Deposits} (load {old.Load}; game value trip {game?.Load.ToString() ?? "-"}): " +
                         (old.Load == game?.Load ? "the multiplier did not reach the ox (reproduced)" : "different from the game value"));
                else
                    Note($"Mill, 2.11.0 code at 3 (the report): the yield returned {old.Yield}, the Miller delivered {old.Deposits}: " +
                         (old.Deposits == 1 && old.Yield >= 3 ? "the multiplier did not reach the Stockpile (reproduced)" : "not the reported behaviour"));

                if (fix == null) Note(string.Format(skip, ProductionPhases[2]));
                else if (stone)
                    Check("Quarry, fix at 3: the ox carries 8 x 3 (plus the productivity bonus) and the Stockpile receives all of it",
                        fix.Load == fix.Yield && fix.Load >= 24 && fix.Deposits == fix.Load && (game == null || fix.Load >= 3 * game.Load - 2), line.Detail[2]);
                else
                    Check("Mill, fix at 3: the Miller delivers the 3 bags its yield returned",
                        fix.Yield >= 3 && fix.Deposits == fix.Yield, line.Detail[2]);
            }
            Note($"Production stage ended at tick {tick} (+{tick - _prodTick0}); Quarry Grunt multiplier {GoodYield.Get(eChimps.CHIMP_TYPE_QUARRY_GRUNT)}, Miller {GoodYield.Get(eChimps.CHIMP_TYPE_MILLER)}, Miller code {MillerFlourPatch.Describe()}");
        }
    }
}
