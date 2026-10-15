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
//   Two workers differ (traced 2026-10-14, Nexus report: Quarry Grunt and Miller at 50 still delivered 12 stone and
//   1 flour per trip):
//   - Quarry Grunt (update 0x1319A0): when 8 stones are ready it loads the quarry's ox: 0x131C8F calls the yield
//     function for the OX (unit id from its workplace record +0x2C8, which RVA 0xAD3E1 fills with the Quarry Ox it
//     creates), base 8, bonus flag 1, and stores the result as the ox's carried count. The ox leaves only with 8 or
//     more (0x132A09, 0x132C17) and delivers them one per tick (0x132E4B / 0x132E6E). The type of the yield call's
//     unit is therefore CHIMP_TYPE_QUARRY_OX, which has no multiplier of its own.
//   - Miller (update 0x1377C0): the count from its yield is overwritten with 1 at the flour pickup and the drop-off
//     adds 1 and clears it: see Config/Core/MillerFlourPatch.cs.
//
// WHAT THIS DOES:
//   The Script Extender hooks that function as UnitR3EventHooks.OnCalculateBonusYield (SE 2.14.0
//   BulkUnitDetours.cs:1420-1445: Pre GoodAmount is passed to the game, Post ReturnValue is returned).
//   Pre: GoodAmount = base x multiplier, with this mod's own per-unit remainder in thousandths, so 1.25 on base 3
//   gives 3, 4, 4, 4 (15 = 12 x 1.25); at most 32767. The game then adds its productivity bonus on the scaled amount.
//   Post: the returned count is clamped to 32767 (the 16-bit carried field). Multiplier 1 (or -1 in the file) leaves
//   the call untouched. Remainders are dropped when the unit is deleted or changes type, and on every map unload.
//   Quarry: a yield call for a Quarry Ox uses the Quarry Grunt's multiplier, and the load is at least 8 (OxDepartLoad;
//   a smaller one would keep the ox at the quarry for good). Miller: a multiplier other than 1 switches
//   MillerFlourPatch on, so the Miller delivers its scaled count; 1 puts the game's code back.
//
// IMPORTANT FOR AI AGENTS:
// - Never set SkipOriginalFunction on this event: the hook then returns 0 (BulkUnitDetours.cs:1428).
// - The multiplier table is a template value: GoodYieldMultiplierProperty writes it through TemplateBaseline, and the
//   restore puts 1 back. Every multiplayer peer runs the same hook in the same order, so the remainders stay in sync
//   when everyone uses the same Units file (host config sync sends it).
// - Not covered (the game never calls the yield function for them): Quarry Mason, Miner1. The Quarry Ox's calls come
//   from the Quarry Grunt's update and use its multiplier (YieldOwner).
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

        /// <summary>The quarry's ox leaves only with this many stones or more (RVA 0x132A09 / 0x132C17: cmp word [ox + 0x9E0], 8).</summary>
        internal const int OxDepartLoad = 8;

        /// <summary>
        /// The worker type whose multiplier applies to a yield call for a unit of <paramref name="type"/>: the Quarry Grunt
        /// loads the quarry's ox by calling the yield function for the ox (RVA 0x131C8F).
        /// </summary>
        internal static eChimps YieldOwner(eChimps type) =>
            type == eChimps.CHIMP_TYPE_QUARRY_OX && !OldQuarryLookupForSelfTest ? eChimps.CHIMP_TYPE_QUARRY_GRUNT : type;

        /// <summary>Self-test only: true = the 2.11.0 lookup (by the ox's own type), to measure the old behaviour.</summary>
        internal static bool OldQuarryLookupForSelfTest;

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
                // A changed multiplier starts from a clean remainder for that type (the quarry's oxen for the Quarry Grunt).
                var stale = new List<int>();
                foreach (var kv in _remainders) if (YieldOwner(kv.Value.Type) == unit) stale.Add(kv.Key);
                foreach (int id in stale) _remainders.Remove(id);
            }
            if (milli != GameMilli)
                Plugin.Logger.LogInfo($"[GoodYield] {unit}: workers carry {multiplier:0.###}x the game's amount per work cycle." +
                    (unit == eChimps.CHIMP_TYPE_QUARRY_GRUNT
                        ? $" The quarry's ox carries the stone: {OxDepartLoad} x {multiplier:0.###} per ox trip plus the productivity bonus, at least {OxDepartLoad} (the ox leaves only with {OxDepartLoad} or more)."
                        : ""));
            if (unit == eChimps.CHIMP_TYPE_MILLER) MillerFlourPatch.Apply(milli != GameMilli);
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
                    int milli = _milli[(int)YieldOwner(type)];
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
            long value = args.ReturnValue;
            if (value <= MaxAmount && value >= OxDepartLoad) return;
            if (!TryGetType(args.UnitId, out eChimps type)) return;
            lock (_lock) if (_milli[(int)YieldOwner(type)] == GameMilli) return;
            args.ReturnValue = ClampReturn(type, value);
        }

        /// <summary>The carried count the game may store for a scaled yield: at most 32767, and for the quarry's ox at least 8.</summary>
        internal static long ClampReturn(eChimps type, long value)
        {
            if (value > MaxAmount) return MaxAmount;
            if (type == eChimps.CHIMP_TYPE_QUARRY_OX && value < OxDepartLoad) return OxDepartLoad;
            return value;
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
