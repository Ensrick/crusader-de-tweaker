// Config/Core/GoodYield.cs
//
// PURPOSE: Units TOML GoodYieldMultiplier (GitHub #1): how many goods a worker carries back per work cycle, per
//          worker type.
//
// HOW THE GAME DOES IT (CrusaderDE.dll, game 2.8.2):
//   The 17 worker types in Workers call the yield function RVA 0x18D940 (unit id, base amount, bonus flag) when a
//   work cycle ends (19 call sites in their update functions, unit function table RVA 0x321CB0; the Woodcutter at
//   0x12C907 with base 12). It returns base + floor(((productivity - 100) * base + remainder) / 100) and keeps the
//   remainder in the unit (+0x95C); productivity is the owner's r_ProductivityPercentage, at least 100 (+50 with the
//   bonus flag in some modes). The caller stores the result as the worker's carried count (16 bit, unit +0x9E0, e.g.
//   0x12C90C), which the drop-off then deposits one per tick (Woodcutter: goods yard add 0xB6100 with 1,
//   0x12CA0C-0x12CA32).
//
// WHAT THIS DOES:
//   The Script Extender hooks that function as UnitR3EventHooks.OnCalculateBonusYield (SE 2.14.0
//   BulkUnitDetours.cs:1420-1445: Pre GoodAmount is passed to the game, Post ReturnValue is returned).
//   Pre: GoodAmount = base x multiplier, with this mod's own per-unit remainder in thousandths, so 1.25 on base 3
//   gives 3, 4, 4, 4 (15 = 12 x 1.25); at most 32767. The game then adds its productivity bonus on the scaled amount.
//   Post: the returned count is clamped to 32767 (the 16-bit carried field). Multiplier 1 (or -1 in the file) leaves
//   the call untouched. Remainders are dropped when the unit is deleted or changes type, and on every map unload.
//
// IMPORTANT FOR AI AGENTS:
// - Never set SkipOriginalFunction on this event: the hook then returns 0 (BulkUnitDetours.cs:1428).
// - The multiplier table is a template value: GoodYieldMultiplierProperty writes it through TemplateBaseline, and the
//   restore puts 1 back. Every multiplayer peer runs the same hook in the same order, so the remainders stay in sync
//   when everyone uses the same Units file (host config sync sends it).
// - Not covered (the game never calls the yield function for them): Quarry Mason, Quarry Ox, Miner1.
//
using System;
using System.Collections.Generic;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Core
{
    internal static class GoodYield
    {
        /// <summary>The worker types whose update calls the yield function (traced in game 2.8.2).</summary>
        internal static readonly eChimps[] Workers =
        {
            eChimps.CHIMP_TYPE_WOODCUTTER, eChimps.CHIMP_TYPE_FLETCHER, eChimps.CHIMP_TYPE_HUNTER,
            eChimps.CHIMP_TYPE_QUARRY_GRUNT, eChimps.CHIMP_TYPE_PITCHMAN, eChimps.CHIMP_TYPE_FARMER_WHEAT,
            eChimps.CHIMP_TYPE_FARMER_HOPS, eChimps.CHIMP_TYPE_FARMER_APPLE, eChimps.CHIMP_TYPE_FARMER_CATTLE,
            eChimps.CHIMP_TYPE_MILLER, eChimps.CHIMP_TYPE_BAKER, eChimps.CHIMP_TYPE_BREWER,
            eChimps.CHIMP_TYPE_POLETURNER, eChimps.CHIMP_TYPE_BLACKSMITH, eChimps.CHIMP_TYPE_ARMOURER,
            eChimps.CHIMP_TYPE_TANNER, eChimps.CHIMP_TYPE_MINER2,
        };

        /// <summary>The game's multiplier, in thousandths.</summary>
        internal const int GameMilli = 1000;

        /// <summary>Largest carried count: the game stores it in 16 bits (unit +0x9E0).</summary>
        internal const int MaxAmount = 32767;

        private static readonly object _lock = new object();
        private static readonly int[] _milli = CreateTable();
        private static readonly Dictionary<int, Remainder> _remainders = new Dictionary<int, Remainder>();
        private static bool _subscribed;

        private struct Remainder
        {
            internal eChimps Type;
            internal int Milli;
        }

        private static int[] CreateTable()
        {
            var table = new int[Enum.GetValues(typeof(eChimps)).Length + 1];
            for (int i = 0; i < table.Length; i++) table[i] = GameMilli;
            return table;
        }

        internal static bool IsWorker(eChimps unit) => Array.IndexOf(Workers, unit) >= 0;

        /// <summary>The multiplier in effect for a worker type (1 = the game's yield).</summary>
        internal static float Get(eChimps unit)
        {
            lock (_lock) return InRange(unit) ? _milli[(int)unit] / 1000f : 1f;
        }

        /// <summary>Sets a worker type's multiplier (rounded to thousandths; 1 = the game's yield).</summary>
        internal static void Set(eChimps unit, float multiplier)
        {
            if (!InRange(unit) || !IsWorker(unit)) return;
            int milli = (int)Math.Round(multiplier * 1000.0);
            lock (_lock)
            {
                if (_milli[(int)unit] == milli) return;
                _milli[(int)unit] = milli;
                // A changed multiplier starts from a clean remainder for that type.
                var stale = new List<int>();
                foreach (var kv in _remainders) if (kv.Value.Type == unit) stale.Add(kv.Key);
                foreach (int id in stale) _remainders.Remove(id);
            }
            if (milli != GameMilli)
                Plugin.Logger.LogInfo($"[GoodYield] {unit}: workers carry {multiplier:0.###}x the game's amount per work cycle.");
        }

        /// <summary>Number of units holding a remainder (self-test).</summary>
        internal static int RemainderCount { get { lock (_lock) return _remainders.Count; } }

        internal static bool HasRemainder(int unitId) { lock (_lock) return _remainders.ContainsKey(unitId); }

        /// <summary>
        /// base x milli / 1000 plus the carried remainder (thousandths), clamped to 1..MaxAmount for a positive base.
        /// Pure: the remainder is updated in place.
        /// </summary>
        internal static int Scale(int baseAmount, int milli, ref int remainderMilli)
        {
            if (baseAmount <= 0 || milli == GameMilli) return baseAmount;
            long total = (long)baseAmount * milli + remainderMilli;
            long scaled = total / 1000;
            remainderMilli = (int)(total % 1000);
            if (scaled > MaxAmount) { remainderMilli = 0; return MaxAmount; }
            return (int)scaled;
        }

        /// <summary>Subscribes the yield, unit-delete and map-unload hooks once (ConfigLoader.RegisterSessionHooks).</summary>
        internal static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnYieldPre);
            UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnYieldPost);
            UnitR3EventHooks.OnUnitDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => { lock (_lock) _remainders.Remove((int)args.UnitId); });
            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => { lock (_lock) _remainders.Clear(); });
        }

        private static void OnYieldPre(UnitCalculateBonusYieldEventArgs args)
        {
            try
            {
                if (!TryGetType(args.UnitId, out eChimps type)) return;
                lock (_lock)
                {
                    int milli = _milli[(int)type];
                    if (milli == GameMilli) return;
                    _remainders.TryGetValue(args.UnitId, out Remainder r);
                    if (r.Type != type) r = new Remainder { Type = type };
                    args.GoodAmount = Scale(args.GoodAmount, milli, ref r.Milli);
                    _remainders[args.UnitId] = r;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[GoodYield] unit {args.UnitId}: {ex.Message}");
            }
        }

        private static void OnYieldPost(UnitCalculateBonusYieldEventArgs args)
        {
            if (args.ReturnValue <= MaxAmount) return;
            if (!TryGetType(args.UnitId, out eChimps type)) return;
            lock (_lock) if (_milli[(int)type] == GameMilli) return;
            args.ReturnValue = MaxAmount;
        }

        private static unsafe bool TryGetType(int unitId, out eChimps type)
        {
            type = eChimps.CHIMP_TYPE_NULL;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit)) return false;
            type = unit->r_UnitChimp;
            return InRange(type);
        }

        private static bool InRange(eChimps unit) => (int)unit >= 0 && (int)unit < _milli.Length;
    }
}
