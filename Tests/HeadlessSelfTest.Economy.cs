// Tests/HeadlessSelfTest.Economy.cs
//
// PURPOSE: Economy stages of the windowless self-test (Tests/HeadlessSelfTest.cs): the Stockpile build cost
//          (GitLab #8), the worker GoodYieldMultiplier (GitHub #1) and the skirmish starting troops (GitHub #2).
//
// WHAT IT DOES (after the range stages, or alone with "-cdt-selftest-only economy"):
//   1. Stockpile: the Structures file cost (Gold 20, Wood 5, written by the runner) must be in the Script Extender's
//      default table, in the map's own cost table and in the build menu's cost table. A keep and a free Stockpile are
//      placed, player 1 gets 50 wood and 200 gold. A paid Stockpile is placed as the editor map is (game type 1,
//      expected free: the pay function returns at RVA 0xC8AC2), then, with the game type switched to 2 (custom
//      mission) for the two calls only, a paid Woodcutter's hut (control: its 3 wood) and a paid Stockpile, which
//      must cost exactly 20 gold and 5 wood.
//   2. Yield: the yield function (RVA 0x18D940, found by the Script Extender's own pattern) is called through its
//      hooked entry under the engine lock: Woodcutter (GoodYieldMultiplier 2) with 12 -> 24, Hunter (1.25) with 3
//      four times -> 3, 4, 4, 4, Fletcher (not set) with 12 -> 12. Then a real delivery is attempted: trees, three
//      peasants and the paid Woodcutter's hut; if a woodcutter works within the window, its trip must carry 24 and
//      the Stockpile must gain 24 wood, else a SKIP note. The killed test Hunter must lose its remainder.
//   3. Starting troops: the troop queue, the game's table and the human-player array are found by pattern, rows 90,
//      91 and 93 must hold the game's defaults, the OnStartMap Pre/Post canary is checked both ways (no rebuild:
//      everything restored; rebuild: the editor is no custom skirmish, so nothing applied) and a direct write / read-back
//      of player 1's queue (3 Archers, 2 Knights); the queue is put back. The editor map never delivers the queue:
//      SKIP note.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        private const int EconomyStage = 10;

        // Written into the configs by scripts/test_headless.ps1.
        internal const int StockpileGoldCost = 20, StockpileWoodCost = 5;
        internal const float WoodcutterYield = 2f, HunterYield = 1.25f;
        internal const int TroopArchers = 3, TroopKnights = 2;   // ["Skirmish Starting Troops".Normal]

        private const int KeepX = 378, KeepY = 384, StockpileX = 365, StockpileY = 398;
        private const int RealDeliveryTicks = 4000, DropOffTicks = 2000, DeleteWaitTicks = 600;
        private const string YieldPattern = "48 63 C1 4C 8D 1D ? ? ? ? 4C 69 C8";   // SE BulkUnitDetours.cs:1418

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long YieldFunction(int unitId, int goodAmount, int bonusFlag);

        private int _woodAttempts, _econTick, _woodcutterHut, _hunter, _yieldWoodBefore = -1;
        private readonly HashSet<int> _directYieldUnits = new HashSet<int>();
        private IDisposable _yieldWatch;
        private int _realUnit, _realBase, _realFlag, _realTick = -1;
        private bool _hunterHadRemainder;
        private long _realReturn;
        private string _econStep = "not started";

        private void StartEconomy(float now)
        {
            _stage = EconomyStage;
            _deadline = now + 420;
            _next = now;
            Note("Economy: Stockpile build cost (GitLab #8), GoodYieldMultiplier (GitHub #1), skirmish starting troops (GitHub #2)");
        }

        private string EconomyProgress() => $"step '{_econStep}', woodcutter hut {_woodcutterHut}, real yield unit {_realUnit} at tick {_realTick}";

        private void EconomyTick(float now)
        {
            int tick = global::Director.instance.getSimTickCount();
            switch (_stage)
            {
                case EconomyStage:
                    _econStep = "stockpile setup";
                    StockpileSetup();
                    _stage++; _next = now + 1;
                    return;
                case EconomyStage + 1:
                    _econStep = "stockpile funds";
                    if (!StockpileFunds(now)) return;
                    _econStep = "stockpile charge";
                    StockpileCharge();
                    _econStep = "yield direct calls";
                    YieldDirect();
                    _econStep = "real delivery setup";
                    RealDeliverySetup();
                    _econTick = _woodcutterHut > 0 ? tick : tick - RealDeliveryTicks;
                    _stage++;
                    return;
                case EconomyStage + 2:
                    _econStep = "real delivery: waiting for a woodcutter";
                    if (_realTick < 0 && tick - _econTick < RealDeliveryTicks) return;
                    if (_realTick < 0)
                    {
                        Note($"SKIP Real woodcutter delivery: no woodcutter of player 1 finished a work cycle within {RealDeliveryTicks} ticks " +
                             $"(hut {_woodcutterHut}; {UnitCensus()}). The direct calls above go through the same hooked function.");
                        RestoreGameType();
                        _stage += 2; _econTick = tick;
                        return;
                    }
                    CheckRealYield();
                    _econTick = tick; _stage++;
                    return;
                case EconomyStage + 3:
                    _econStep = "real delivery: waiting for the drop-off";
                    int wood = GamePlayerManagerAPI.Instance.GetGoodAmount(1, eGoods.STORED_WOOD_PLANKS);
                    if (wood - _yieldWoodBefore < _realReturn && tick - _econTick < DropOffTicks) return;
                    Check("Real delivery: the Stockpile receives the scaled trip", wood - _yieldWoodBefore == _realReturn,
                        $"player 1 wood {_yieldWoodBefore} -> {wood} (+{wood - _yieldWoodBefore}) within {tick - _econTick} ticks of the trip; the trip carried {_realReturn}");
                    RestoreGameType();
                    _econTick = tick; _stage++;
                    return;
                case EconomyStage + 4:
                    _econStep = "yield remainder after delete";
                    if (GoodYield.HasRemainder(_hunter) && tick - _econTick < DeleteWaitTicks) return;
                    if (GoodYield.HasRemainder(_hunter))
                        Note($"SKIP Remainder cleared on unit delete: the killed Hunter {_hunter} was not deleted by the game within {DeleteWaitTicks} ticks");
                    else
                        Check("Remainder cleared on unit delete", _hunterHadRemainder,
                            $"Hunter {_hunter}: remainder kept after its fifth trip {_hunterHadRemainder}, none after the game deleted it");
                    _yieldWatch?.Dispose();
                    _econStep = "starting troops";
                    StartingTroops();
                    _econStep = "done";
                    Finish();
                    return;
            }
        }

        // ===================================================
        // Stockpile build cost
        // ===================================================

        private void StockpileSetup()
        {
            var b = GameBuildingManagerAPI.Instance;
            const eStructs yard = eStructs.STRUCT_GOODS_YARD;
            BuildingCost def = b.GetDefaultCost(yard);
            Check("Stockpile cost in the Script Extender's default table (Structures file)",
                def.Gold == StockpileGoldCost && def.Wood == StockpileWoodCost && def.Stone == 0 && def.Iron == 0 && def.Pitch == 0,
                $"{def} (file: GoldCost {StockpileGoldCost}, WoodCost {StockpileWoodCost}; game 0)");
            Check("Stockpile cost in the map's own cost table (copied from the defaults when the first map starts, RVA 0xC33A0)",
                b.GetGoldCost(yard) == StockpileGoldCost && b.GetWoodCost(yard) == StockpileWoodCost && b.GetStoneCost(yard) == 0
                && b.GetIronIngotCost(yard) == 0 && b.GetRawPitchCost(yard) == 0,
                $"gold {b.GetGoldCost(yard)}, wood {b.GetWoodCost(yard)}, stone {b.GetStoneCost(yard)}, iron {b.GetIronIngotCost(yard)}, pitch {b.GetRawPitchCost(yard)}");
            int wood = 0, stone = 0, iron = 0, pitch = 0, gold = 0;
            global::GameData.getStructureCosts((int)yard, ref wood, ref stone, ref iron, ref pitch, ref gold);
            Check("Stockpile cost in the build menu's table (GameData.getStructureCosts)", gold == StockpileGoldCost && wood == StockpileWoodCost,
                $"wood {wood}, stone {stone}, iron {iron}, pitch {pitch}, gold {gold}");

            Note($"Game type {GamePlayerManagerAPI.Instance.GetCurrentGameTypeMode()} (the map editor's: DLL_PreInitMap_Editor writes 1 at RVA 0x85EFB)");
            int keep = Prefab(eMappers.MAPPER_KEEP1, KeepX, KeepY, 7, eStructs.STRUCT_KEEP_ONE, free: true, bypass: false);
            int yardId = Prefab(eMappers.MAPPER_STORES, StockpileX, StockpileY, 5, yard, free: true, bypass: true);
            if (keep <= 0 || yardId <= 0) throw new InvalidOperationException($"fixture failed: keep {keep}, stockpile {yardId}");
            GamePlayerManagerAPI.Instance.SetPlayerGold(1, 200);
            Note($"Fixture: keep {keep} at {KeepX},{KeepY}, free Stockpile {yardId} at {StockpileX},{StockpileY}, player 1 gold set to 200");
        }

        private bool StockpileFunds(float now)
        {
            var p = GamePlayerManagerAPI.Instance;
            if (_woodAttempts == 0) p.TryAddGood(1, eGoods.STORED_WOOD_PLANKS, 50);
            if (p.GetGoodAmount(1, eGoods.STORED_WOOD_PLANKS) >= 50) return true;
            if (++_woodAttempts >= 12) throw new InvalidOperationException("could not store 50 wood in the fixture Stockpile");
            p.TryAddGood(1, eGoods.STORED_WOOD_PLANKS, 50);
            _next = now + 1;
            return false;
        }

        private unsafe void StockpileCharge()
        {
            var p = GamePlayerManagerAPI.Instance;
            int Wood() => p.GetGoodAmount(1, eGoods.STORED_WOOD_PLANKS);
            int Gold() => p.GetPlayerGold(1);

            int w0 = Wood(), g0 = Gold();
            int editorYard = Prefab(eMappers.MAPPER_STORES, StockpileX, StockpileY + 6, 5, eStructs.STRUCT_GOODS_YARD, free: false, bypass: true);
            Note($"Editor rule: a paid Stockpile placed as the map editor (game type {p.GetCurrentGameTypeMode()}) cost {w0 - Wood()} wood, {g0 - Gold()} gold " +
                 $"(building {editorYard}; the editor never charges, RVA 0xC8ABE)");

            eGameTypeModes* type = p._currentGameTypeMode;
            if (type == null) throw new InvalidOperationException("the Script Extender has no game type pointer");
            eGameTypeModes editor = *type;
            int w1, w2, w3, g2, g3, cutterCost = GameBuildingManagerAPI.Instance.GetWoodCost(eStructs.STRUCT_WOODCUTTERS_HUT);
            int paidYard;
            *type = eGameTypeModes.GAMETYPE_MAP;
            try
            {
                w1 = Wood();
                _woodcutterHut = Prefab(eMappers.MAPPER_WOODSMAN, StockpileX + 6, StockpileY, 3, eStructs.STRUCT_WOODCUTTERS_HUT, free: false, bypass: true);
                w2 = Wood(); g2 = Gold();
                paidYard = Prefab(eMappers.MAPPER_STORES, StockpileX - 5, StockpileY, 5, eStructs.STRUCT_GOODS_YARD, free: false, bypass: true);
                w3 = Wood(); g3 = Gold();
            }
            finally
            {
                *type = editor;
            }
            Check("Control: a paid Woodcutter's hut costs its wood (game type switched to 2 for the call)",
                _woodcutterHut > 0 && w1 - w2 == cutterCost, $"hut {_woodcutterHut}; wood {w1} -> {w2} (cost {cutterCost})");
            Check($"A paid Stockpile costs the configured {StockpileGoldCost} gold + {StockpileWoodCost} wood",
                paidYard > 0 && g2 - g3 == StockpileGoldCost && w2 - w3 == StockpileWoodCost,
                $"Stockpile {paidYard}; gold {g2} -> {g3}, wood {w2} -> {w3}; game type back to {*type}");
        }

        /// <summary>Places a building like a player would (CreatePrefab) and returns its id (0 = not found).</summary>
        private int Prefab(eMappers mapper, int x, int y, int scale, eStructs type, bool free, bool bypass)
        {
            var api = GameBuildingManagerAPI.Instance;
            long result = api.CreatePrefab(1, x, y, mapper, scale, 0, free, bypass);
            var all = api.GetBuildingsAsSpan();
            for (int i = 0; i < all.Length; i++)
                if (all[i].r_BuildingType == type && all[i].r_PlayerIdOwner == 1 && all[i].r_TilePositionXBegin == x && all[i].r_TilePositionYBegin == y)
                    return i + 1;
            Note($"{mapper} at {x},{y} (free {free}): CreatePrefab returned {result}, no building found");
            return 0;
        }

        // ===================================================
        // GoodYieldMultiplier
        // ===================================================

        private unsafe void YieldDirect()
        {
            Check("GoodYieldMultiplier from the Units file in effect",
                Math.Abs(GoodYield.Get(eChimps.CHIMP_TYPE_WOODCUTTER) - WoodcutterYield) < 1e-3f && Math.Abs(GoodYield.Get(eChimps.CHIMP_TYPE_HUNTER) - HunterYield) < 1e-3f
                && GoodYield.Get(eChimps.CHIMP_TYPE_FLETCHER) == 1f,
                $"Woodcutter {GoodYield.Get(eChimps.CHIMP_TYPE_WOODCUTTER)}, Hunter {GoodYield.Get(eChimps.CHIMP_TYPE_HUNTER)}, Fletcher {GoodYield.Get(eChimps.CHIMP_TYPE_FLETCHER)}");

            if (!GameCode.TryFindUnique(YieldPattern, out uint rva, out string problem)) throw new InvalidOperationException(problem);
            var yield = (YieldFunction)Marshal.GetDelegateForFunctionPointer((IntPtr)((long)GameCode.ModuleBase + rva), typeof(YieldFunction));
            GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(1, out GamePlayerResources* res);
            int productivity = res != null ? (int)res->r_ProductivityPercentage : -1;
            Note($"Yield function at RVA 0x{rva:X} (expected 0x18D940), called through its hooked entry; player 1 productivity {productivity}%");

            // The game adds floor(((productivity - 100) * amount + its remainder at +0x95C) / 100), productivity at least
            // 100 (RVA 0x18D964-0x18D9D8; no +50 here, the bonus flag is 0). Read before the call.
            int Expected(int unit, int scaled) => scaled + ((Math.Max(productivity, 100) - 100) * scaled + Math.Max(0, ReadNativeUnitField16(unit, 0x95C))) / 100;
            int Call(int unit, int amount)
            {
                _directYieldUnits.Add(unit);
                return (int)yield(unit, amount, 0);
            }

            int cutter = Spawn(eChimps.CHIMP_TYPE_WOODCUTTER, 390, 395);
            _hunter = Spawn(eChimps.CHIMP_TYPE_HUNTER, 392, 395);
            int fletcher = Spawn(eChimps.CHIMP_TYPE_FLETCHER, 394, 395);

            int expectCutter = Expected(cutter, 24);
            int got = Call(cutter, 12);
            Check($"Woodcutter, GoodYieldMultiplier {WoodcutterYield}: a trip of 12 becomes 24", got == expectCutter, $"returned {got}, expected {expectCutter}");

            var trips = new List<int>();
            for (int i = 0; i < 4; i++) trips.Add(Call(_hunter, 3));
            bool plain = productivity <= 100;
            Check($"Hunter, GoodYieldMultiplier {HunterYield}: four trips of 3 carry 3, 4, 4, 4 (the fractions add up)",
                plain ? trips.SequenceEqual(new[] { 3, 4, 4, 4 }) : trips.Sum() >= 15, $"returned {string.Join(", ", trips)}");

            int expectFletcher = Expected(fletcher, 12);
            got = Call(fletcher, 12);
            Check("Fletcher, GoodYieldMultiplier not set: a trip of 12 stays 12", got == expectFletcher, $"returned {got}, expected {expectFletcher}");

            Call(_hunter, 3);   // leaves a remainder of 0.75 for the delete check
            _hunterHadRemainder = GoodYield.HasRemainder(_hunter);
            GameUnitManagerAPI.Instance.KillUnit(cutter);
            GameUnitManagerAPI.Instance.KillUnit(fletcher);
            GameUnitManagerAPI.Instance.KillUnit(_hunter);
        }

        private void RealDeliverySetup()
        {
            _yieldWatch = UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(a => a.Phase == EventHookPhase.Post && !_directYieldUnits.Contains(a.UnitId))
                .Subscribe(a =>
                {
                    if (_realTick >= 0) return;
                    _realUnit = a.UnitId; _realBase = a.GoodAmount; _realFlag = a.b50PercentBonus; _realReturn = a.ReturnValue;
                    _realTick = global::Director.instance.getSimTickCount();
                    _yieldWoodBefore = GamePlayerManagerAPI.Instance.GetGoodAmount(1, eGoods.STORED_WOOD_PLANKS);
                });
            if (_woodcutterHut <= 0) { Note("Real delivery: no Woodcutter's hut (placement failed above)"); return; }

            // Trees east and south of the hut, three idle peasants next to the keep.
            int oak = (int)eMappers.MAPPER_OAK, placed = 0;
            for (int i = 0; i < 6; i++)
            {
                global::EngineInterface.StartMapperItem(oak);
                global::EngineInterface.PlaceMapperItem(oak, StockpileX + 7 + 2 * i, StockpileY + 12, 1, 1, false, false, 1);
                placed++;
            }
            global::MainControls.instance?.StopAllPlacement();
            for (int i = 0; i < 3; i++) Spawn(eChimps.CHIMP_TYPE_PEASANT, KeepX + 2 * i, KeepY + 9);
            // The map editor assigns no jobs (measured: three peasants stayed idle for 4000 ticks at game type 1), so the
            // window runs as a custom mission (game type 2); RestoreGameType puts the editor's type back.
            SwitchGameType(eGameTypeModes.GAMETYPE_MAP);
            Note($"Real delivery: {placed} oaks at {StockpileX + 7},{StockpileY + 12} and east, 3 peasants south of the keep, Woodcutter's hut {_woodcutterHut}; " +
                 $"game type {GamePlayerManagerAPI.Instance.GetCurrentGameTypeMode()} for the window; waiting up to {RealDeliveryTicks} ticks for a work cycle");
        }

        private eGameTypeModes? _savedGameType;

        private unsafe void SwitchGameType(eGameTypeModes type)
        {
            eGameTypeModes* p = GamePlayerManagerAPI.Instance._currentGameTypeMode;
            if (p == null) throw new InvalidOperationException("the Script Extender has no game type pointer");
            if (_savedGameType == null) _savedGameType = *p;
            *p = type;
        }

        private unsafe void RestoreGameType()
        {
            if (_savedGameType == null) return;
            lock (_engineLock) *GamePlayerManagerAPI.Instance._currentGameTypeMode = _savedGameType.Value;
            Note($"Game type back to {GamePlayerManagerAPI.Instance.GetCurrentGameTypeMode()}");
            _savedGameType = null;
        }

        private unsafe void CheckRealYield()
        {
            eChimps type = eChimps.CHIMP_TYPE_NULL;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(_realUnit, out GameUnit* u)) type = u->r_UnitChimp;
            float multiplier = GoodYield.Get(type);
            int carried = ReadNativeUnitField16(_realUnit, 0x9E0);
            GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(1, out GamePlayerResources* res);
            int productivity = res != null ? (int)res->r_ProductivityPercentage : -1;
            Note($"Real work cycle: unit {_realUnit} ({type}) at tick {_realTick}: game amount {_realBase}, bonus flag {_realFlag}, productivity {productivity}%, " +
                 $"returned {_realReturn}; carried (+0x9E0) now {carried}");
            // Exact without a productivity bonus; with one the game adds its share on top of the scaled amount.
            long scaled = (long)Math.Round(_realBase * multiplier);
            bool exact = _realFlag == 0 && productivity <= 100;
            Check("Real work cycle: the trip is the game's amount x GoodYieldMultiplier",
                type == eChimps.CHIMP_TYPE_WOODCUTTER && (exact ? _realReturn == scaled : _realReturn >= scaled),
                $"{type}: {_realBase} x {multiplier} -> {_realReturn} ({(exact ? "no productivity bonus: exact" : "productivity bonus on top")})");
        }

        /// <summary>
        /// A 16-bit unit field at a native offset (as the game's code addresses it: unit array base 0x67E8400 +
        /// id * 0x490 + offset). The Script Extender's GameUnit pointer starts later: its r_UnitChimp is the game's
        /// +0x6E6 (unit type, read at RVA 0x6D7E3).
        /// </summary>
        private static unsafe int ReadNativeUnitField16(int unitId, int nativeOffset)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* u)) return int.MinValue;
            int seTypeOffset = (int)((byte*)&u->r_UnitChimp - (byte*)u);
            return ReadUnitField16(unitId, nativeOffset - (0x6E6 - seTypeOffset));
        }

        private string UnitCensus()
        {
            var counts = new Dictionary<eChimps, int>();
            foreach (int id in GameUnitManagerAPI.Instance.GetAllAliveUnits())
            {
                eChimps t = GameUnitManagerAPI.Instance.GetType(id);
                counts.TryGetValue(t, out int n);
                counts[t] = n + 1;
            }
            return "alive units: " + string.Join(", ", counts.Select(kv => $"{kv.Key} x{kv.Value}"));
        }

        // ===================================================
        // Skirmish starting troops
        // ===================================================

        private unsafe void StartingTroops()
        {
            bool ok = SkirmishStartingTroops.TryResolve(out string problem);
            Check("Starting troop queue, table and human-player array found and the table signature holds", ok,
                ok ? $"queue RVA 0x{SkirmishStartingTroops.QueueRva:X} (expected 0x382D354), table 0x{SkirmishStartingTroops.TableRva:X} (0x2D4FA0), humans 0x{SkirmishStartingTroops.HumansRva:X} (0x8574BCC)" : problem);
            if (!ok) return;

            foreach (var (row, name, expect) in new[]
            {
                (90, "Normal, European lord", "5 Archer + 7 Spearman"),
                (91, "Crusader, European lord", "40 Archer + 10 Swordsman + 4 Knight"),
                (93, "Normal, Arabian lord", "6 ArabianBow + 6 Slave"),
            })
            {
                int[] counts = SkirmishStartingTroops.ReadRow(row);
                string got = string.Join(" + ", SkirmishStartingTroops.Slots.Where(s => counts[s.Slot] > 0).Select(s => $"{counts[s.Slot]} {s.Key}"));
                Check($"Game table row {row} ({name})", got == expect && counts.Where((c, i) => SkirmishStartingTroops.Slots.All(s => s.Slot != i)).All(c => c == 0),
                    $"{got} (expected {expect})");
            }

            var players = GamePlayerManagerAPI.Instance;
            Note("Editor map players: " + string.Join(", ", Enumerable.Range(1, SkirmishStartingTroops.MaxPlayer)
                .Select(p => $"{p} {(SkirmishStartingTroops.IsHuman(p) ? "human" : players.IsAIPlayer(p) ? "AI" : "-")}")) +
                $"; skirmish game mode {players.GetCurrentSkirmishGameMode()}, start option {players.GetCurrentSkirmishMode()}");

            int[,] before = SnapshotQueue();
            try
            {
                // OnStartMap without a rebuild (any non-skirmish start): canaries written, then put back.
                SkirmishStartingTroops.OnStartPre();
                bool canaries = Enumerable.Range(1, SkirmishStartingTroops.MaxPlayer)
                    .All(p => *SkirmishStartingTroops.QueueSlot(p, SkirmishStartingTroops.CanarySlot) == SkirmishStartingTroops.Canary);
                SkirmishStartingTroops.OnStartPost();
                Check("OnStartMap Pre writes a canary into the unread slot 7 of players 1-8 (file sets Normal counts)", canaries, $"canaries present: {canaries}");
                Check("OnStartMap Post without a queue rebuild puts everything back", SameQueue(before, SnapshotQueue(), out string diff), diff);

                // OnStartMap with a rebuild (the game overwrites every slot): applies only in a custom skirmish.
                SkirmishStartingTroops.OnStartPre();
                for (int p = 1; p <= SkirmishStartingTroops.MaxPlayer; p++) *SkirmishStartingTroops.QueueSlot(p, SkirmishStartingTroops.CanarySlot) = 0;
                int[,] rebuilt = SnapshotQueue();
                SkirmishStartingTroops.OnStartPost();
                bool applies = SkirmishStartingTroops.ShouldApply(true, players.GetCurrentSkirmishGameMode(), (int)players.GetCurrentSkirmishMode(), out string reason);
                Check("OnStartMap Post after a rebuild follows the start type (the editor map is no custom skirmish)",
                    applies || SameQueue(rebuilt, SnapshotQueue(), out diff), applies ? "applied (custom skirmish)" : $"not applied: {reason}; queue unchanged");

                // The write itself, on player 1.
                var settings = new SkirmishStartingTroops.Settings { ApplyToAI = true };
                settings.Counts[0][0] = TroopArchers;
                settings.Counts[0][6] = TroopKnights;
                int[,] beforeWrite = SnapshotQueue();
                string log = SkirmishStartingTroops.Apply(settings, 1, p => p == 1);
                int[,] after = SnapshotQueue();
                bool othersSame = true;
                for (int p = 0; p <= SkirmishStartingTroops.MaxPlayer; p++)
                    for (int s = 0; s < SkirmishStartingTroops.SlotCount; s++)
                        if (!(p == 1 && (s == 0 || s == 6)) && !(SkirmishStartingTroops.IsHuman(p) && p > 1 && (s == 0 || s == 6)) && after[p, s] != beforeWrite[p, s]) othersSame = false;
                Check($"Queue write: player 1 gets {TroopArchers} Archers and {TroopKnights} Knights, nothing else changes",
                    after[1, 0] == TroopArchers && after[1, 6] == TroopKnights && othersSame, $"slot 0 = {after[1, 0]}, slot 6 = {after[1, 6]}; log: {log}");
            }
            finally
            {
                RestoreQueue(before);
            }
            Check("Queue restored", SameQueue(before, SnapshotQueue(), out string restored), restored);
            // Tried 2026-10-08: running the editor map as game type 3 with game mode 0x63 for a window stopped the
            // simulation within 500 ticks without a delivery, so the delivery stays unmeasured.
            Note("SKIP Starting troop delivery: the delivery (RVA 0x119050) returns at once in the map editor (game type 1, check at RVA 0x11906C), " +
                 "reads the per-player queue only in the skirmish game mode (0x1190F9), and the editor map never runs the skirmish start handler " +
                 "(RVA 0x94350); traced in the game code only.");
        }

        private static unsafe int[,] SnapshotQueue()
        {
            var q = new int[SkirmishStartingTroops.MaxPlayer + 1, SkirmishStartingTroops.SlotCount];
            for (int p = 0; p <= SkirmishStartingTroops.MaxPlayer; p++)
                for (int s = 0; s < SkirmishStartingTroops.SlotCount; s++) q[p, s] = *SkirmishStartingTroops.QueueSlot(p, s);
            return q;
        }

        private static unsafe void RestoreQueue(int[,] q)
        {
            for (int p = 0; p <= SkirmishStartingTroops.MaxPlayer; p++)
                for (int s = 0; s < SkirmishStartingTroops.SlotCount; s++) *SkirmishStartingTroops.QueueSlot(p, s) = q[p, s];
        }

        private static bool SameQueue(int[,] a, int[,] b, out string diff)
        {
            var d = new List<string>();
            for (int p = 0; p <= SkirmishStartingTroops.MaxPlayer; p++)
                for (int s = 0; s < SkirmishStartingTroops.SlotCount; s++)
                    if (a[p, s] != b[p, s]) d.Add($"player {p} slot {s}: {a[p, s]} -> {b[p, s]}");
            diff = d.Count == 0 ? "identical" : string.Join("; ", d.Take(8));
            return d.Count == 0;
        }
    }
}
