// Config/Core/ApothecaryHealing.cs
//
// PURPOSE: The ["Apothecary Healing"] section of the GameplaySettings file (GitLab #4, UCP-style healing):
//          every alive apothecary (STRUCT_HEALER) heals its owner's wounded soldiers (recruitable unit
//          types + the Lord) standing within RadiusTiles of the building, by HealPercent of the unit's
//          max health and / or HealHitPoints, once every IntervalTicks, but only units that have not hit
//          or been hit for OutOfCombatTicks. Off (game behaviour) while HealPercent and HealHitPoints are
//          both -1.
//
// WHY THIS SHAPE: UCP's "Healer heals casualties" (UCP2 o_healer) makes the apothecary's worker walk to
//   wounded units and heal them. The Script Extender (2.14.0) has no hook into that worker's AI or
//   animation, so the mod heals as an aura around the building instead (no walking healer).
//
// HOW (CrusaderDE.dll game 2.8.2, SE 2.14.0 source):
//   - Driven by GameTimeManagerAPI.OnTick (SE StrongholdFrameProvider.OnGameTick, raised from a context hook
//     inside the native game loop, BulkTimeDetours.cs:42-64; not raised while paused). A pass runs when the
//     sim tick enters a new IntervalTicks bucket (tick / IntervalTicks changed), so all players of a match
//     heal on the same ticks.
//   - The heal writes what the game's own Bedouin heal writes (RVA 0x17491A: hp += heal; at or above max:
//     hp = max and health-bar blocks (game +0x690, word) = 10; else health percentage (+0x92C, word) =
//     hp * 100 / max, or 100 when max is 0, and blocks = percentage / 10). SE GameUnit offsets = game - 0x65C:
//     r_CurrentHealth +0x3C4 (game +0xA20), r_MaxHealth +0x3C8 (+0xA24), r_CurrentHealthPercentage +0x2D0
//     (+0x92C), r_HealthBarBlocks +0x34 (+0x690; SE declares 32 bits, the game writes 16, so 16 are written).
//   - Combat: OnUnitTakeMeleeDamage (Pre) and OnUnitTakeProjectileDamageEx (Pre, only real damage) stamp the
//     tick on both the damaged and the attacking unit. Fire, disease, Eunuch fire pots and other damage
//     sources have no such event and do not count as combat.
//
// MULTIPLAYER: every client simulates the match, so the pass must be a pure function of game state and the
//   settings (which the host config sync gives everyone): apothecaries and units are processed in ascending
//   id order, integer maths only, no wall clock, no local player check. Configure() never resets runtime
//   state (it can run mid-match on one client only, e.g. when the local player's market spawns).
//
// IMPORTANT FOR AI AGENTS:
// - Parse / HealAmount / DistanceSquaredToFootprint / IsHealable are pure and covered by
//   Tests/ApothecaryHealingTest.cs; game access stays in RunPass and the event handlers.
// - "Is the apothecary staffed" reads GameBuilding.r_TotalCurrentWorkers; that it is 1 while the healer works
//   there is [unverified] (the windowless self-test map has no peasants).
//
using System;
using System.Collections.Generic;
using System.Globalization;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums; // AliveState, PlayerRelationship
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    internal static class ApothecaryHealing
    {
        internal const string Section = "Apothecary Healing";

        internal const int DefaultRadiusTiles = 10, DefaultIntervalTicks = 100, DefaultOutOfCombatTicks = 200;
        internal const int MaxRadiusTiles = 400, MaxIntervalTicks = 1000000;

        /// <summary>Parsed section. HealBasisPoints: hundredths of a percent of max health (-1 = not set).</summary>
        internal sealed class Settings
        {
            internal int HealBasisPoints = -1;
            internal int HealHitPoints = -1;
            internal int RadiusTiles = DefaultRadiusTiles;
            internal int IntervalTicks = DefaultIntervalTicks;
            internal int OutOfCombatTicks = DefaultOutOfCombatTicks;
            internal bool NeedsWorker = true;

            internal bool Enabled => HealBasisPoints > 0 || HealHitPoints > 0;

            internal Settings Clone() => (Settings)MemberwiseClone();

            public override string ToString() =>
                !Enabled ? "off" :
                $"{(HealBasisPoints > 0 ? (HealBasisPoints / 100.0).ToString("0.##", CultureInfo.InvariantCulture) + "% of max health" : "")}" +
                $"{(HealBasisPoints > 0 && HealHitPoints > 0 ? " + " : "")}{(HealHitPoints > 0 ? HealHitPoints + " HP" : "")}" +
                $" every {IntervalTicks} ticks within {RadiusTiles} tiles, {OutOfCombatTicks} ticks out of combat, " +
                $"{(NeedsWorker ? "only while the apothecary has its worker" : "worker not required")}";
        }

        internal struct HealRecord
        {
            internal int Tick, UnitId, Before, After, Max;
        }

        private static readonly object _lock = new object();
        private static Settings _settings = new Settings();
        private static readonly Dictionary<int, int> _lastCombatTick = new Dictionary<int, int>();
        private static int _lastBucket = int.MinValue;
        private static bool _subscribed, _firstHealLogged;

        /// <summary>Self-test only: when true, every heal is appended to HealLog (lock HealLog to read) and combat is stamped even while off.</summary>
        internal static bool RecordHeals;
        internal static readonly List<HealRecord> HealLog = new List<HealRecord>();
        internal static int PassCount;

        internal static Settings Current { get { lock (_lock) return _settings.Clone(); } }

        // ===================================================
        // Pure parts (Tests/ApothecaryHealingTest.cs)
        // ===================================================

        /// <summary>Reads the section; null = section missing = off. Bad values are reported through warn and ignored.</summary>
        internal static Settings Parse(TomlTable section, Action<string> warn)
        {
            var s = new Settings();
            if (section == null) return s;

            if (section.TryGetValue("HealPercent", out var pct))
            {
                double p;
                if (pct is long l) p = l;
                else if (pct is double d) p = d;
                else { warn($"[ApothecaryHealing] HealPercent: '{pct}' is not a number; healing by percent is off."); p = -1; }
                if (double.IsNaN(p) || p <= 0) p = -1;
                else if (p > 100) { warn($"[ApothecaryHealing] HealPercent={p.ToString(CultureInfo.InvariantCulture)} is above 100; using 100."); p = 100; }
                s.HealBasisPoints = p > 0 ? Math.Max(1, (int)Math.Round(p * 100)) : -1;
            }
            // 0 or below = off, like HealPercent.
            s.HealHitPoints = ReadInt(section, "HealHitPoints", -1, int.MinValue, int.MaxValue, warn);
            if (s.HealHitPoints <= 0) s.HealHitPoints = -1;
            s.RadiusTiles = ReadInt(section, "RadiusTiles", DefaultRadiusTiles, 0, MaxRadiusTiles, warn);
            s.IntervalTicks = ReadInt(section, "IntervalTicks", DefaultIntervalTicks, 1, MaxIntervalTicks, warn);
            s.OutOfCombatTicks = ReadInt(section, "OutOfCombatTicks", DefaultOutOfCombatTicks, 0, int.MaxValue, warn);
            if (section.TryGetValue("NeedsWorker", out var nw))
            {
                if (nw is bool b) s.NeedsWorker = b;
                else warn($"[ApothecaryHealing] NeedsWorker: '{nw}' is not true / false; using true.");
            }
            return s;
        }

        /// <summary>-1 (or missing) = fallback; other values are clamped to [min, max] with a warning.</summary>
        private static int ReadInt(TomlTable section, string key, int fallback, int min, int max, Action<string> warn)
        {
            if (!section.TryGetValue(key, out var raw)) return fallback;
            if (!(raw is long v)) { warn($"[ApothecaryHealing] {key}: '{raw}' is not a whole number; using {(fallback < 0 ? "off" : fallback.ToString())}."); return fallback; }
            if (v == -1) return fallback;
            long clamped = Math.Max(min, Math.Min(max, v));
            if (clamped != v) warn($"[ApothecaryHealing] {key}={v} is outside [{min}, {max}]; using {clamped}.");
            return (int)clamped;
        }

        /// <summary>Health one heal adds to a unit with this max health (before the cap at max). Integer maths only.</summary>
        internal static int HealAmount(int maxHealth, Settings s)
        {
            long amount = 0;
            if (s.HealBasisPoints > 0) amount += (long)Math.Max(0, maxHealth) * s.HealBasisPoints / 10000;
            if (s.HealHitPoints > 0) amount += s.HealHitPoints;
            if (s.Enabled && amount < 1) amount = 1;
            return (int)Math.Min(int.MaxValue, amount);
        }

        /// <summary>Squared distance in tiles from (x, y) to the nearest tile of the footprint [minX..maxX] x [minY..maxY].</summary>
        internal static long DistanceSquaredToFootprint(int x, int y, int minX, int minY, int maxX, int maxY)
        {
            long dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
            long dy = y < minY ? minY - y : y > maxY ? y - maxY : 0;
            return dx * dx + dy * dy;
        }

        /// <summary>Soldiers (recruitable types) and the Lord. Workers, siege engines and animals are not healed.</summary>
        internal static bool IsHealable(eChimps type) =>
            type == eChimps.CHIMP_TYPE_LORD || Data.UnitCategories.IsRecruitable(type);

        // ===================================================
        // Settings and hooks
        // ===================================================

        /// <summary>Takes new settings (null = off). Never resets combat stamps or the pass schedule (see MULTIPLAYER).</summary>
        internal static void Configure(Settings settings, string reason)
        {
            settings = settings ?? new Settings();
            lock (_lock) _settings = settings;
            if (settings.Enabled)
                Plugin.Logger.LogInfo($"[ApothecaryHealing] On ({reason}): {settings}.");
            else if (Plugin.Logger != null)
                Plugin.Logger.LogDebug($"[ApothecaryHealing] Off ({reason}).");
        }

        /// <summary>Subscribes the tick, damage and unload hooks once. Safe during LibraryLoaded (no game write).</summary>
        internal static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            GameTimeManagerAPI.Instance.OnTick += OnTick;

            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => StampCombat(args.DamagedUnitId, args.AttackingUnitId));
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => StampCombat(args.AttackedUnitId, args.AttackingUnitId));

            // Tick counts restart with the next map; old stamps would block healing there.
            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ =>
                {
                    lock (_lock)
                    {
                        _lastCombatTick.Clear();
                        _lastBucket = int.MinValue;
                        _firstHealLogged = false;
                    }
                });
        }

        private static void StampCombat(int damagedUnitId, int attackingUnitId)
        {
            try
            {
                int tick = GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
                lock (_lock)
                {
                    if (!_settings.Enabled && !RecordHeals) return;
                    if (damagedUnitId > 0) _lastCombatTick[damagedUnitId] = tick;
                    if (attackingUnitId > 0) _lastCombatTick[attackingUnitId] = tick;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[ApothecaryHealing] Combat stamp failed: {ex.Message}");
            }
        }

        /// <summary>Tick of the unit's last hit taken or dealt (melee / projectile), or -1.</summary>
        internal static int LastCombatTick(int unitId)
        {
            lock (_lock) return _lastCombatTick.TryGetValue(unitId, out int t) ? t : -1;
        }

        private static void OnTick(int tick)
        {
            Settings s;
            lock (_lock)
            {
                s = _settings;
                if (!s.Enabled) return;
                int bucket = tick / s.IntervalTicks;
                if (bucket == _lastBucket) return;
                _lastBucket = bucket;
            }
            try
            {
                RunPass(tick, s);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[ApothecaryHealing] Heal pass at tick {tick} failed: {ex}");
            }
        }

        // ===================================================
        // Heal pass (game access)
        // ===================================================

        private struct Footprint
        {
            internal int Owner, MinX, MinY, MaxX, MaxY;
        }

        private static readonly List<int> _buildingIds = new List<int>();
        private static readonly List<int> _unitIds = new List<int>();
        private static readonly List<Footprint> _footprints = new List<Footprint>();

        private static unsafe void RunPass(int tick, Settings s)
        {
            var buildings = Plugin.BuildingApi;
            var units = Plugin.UnitApi;
            if (buildings == null || units == null) return;

            // Working apothecaries, ascending id.
            _footprints.Clear();
            buildings.GetAllBuildings(_buildingIds, AliveState.IsAlive, eStructs.STRUCT_HEALER, PlayerRelationship.Any, null);
            foreach (int id in _buildingIds)
            {
                if (!buildings.TryGetBuildingById(id, out GameBuilding* b) || b == null) continue;
                if (b->r_IsSleeping == 1) continue;
                if (s.NeedsWorker && b->r_TotalCurrentWorkers == 0) continue;
                var begin = buildings.GetBeginPosition(id);
                var end = buildings.GetEndPosition(id);
                _footprints.Add(new Footprint
                {
                    Owner = b->r_PlayerIdOwner,
                    MinX = Math.Min(begin.X, end.X), MaxX = Math.Max(begin.X, end.X),
                    MinY = Math.Min(begin.Y, end.Y), MaxY = Math.Max(begin.Y, end.Y),
                });
            }
            lock (_lock) PassCount++;
            if (_footprints.Count == 0) return;

            long radiusSq = (long)s.RadiusTiles * s.RadiusTiles;
            int healed = 0;
            units.GetAllUnits(_unitIds, AliveState.IsAlive);
            foreach (int id in _unitIds)
            {
                if (!units.TryGetUnitById(id, out GameUnit* u) || u == null) continue;
                if (u->r_IsKilledByProjectile != 0 || !IsHealable(u->r_UnitChimp)) continue;
                int hp = (int)u->r_CurrentHealth, max = (int)u->r_MaxHealth;
                if (hp <= 0 || hp >= max) continue;

                int owner = u->r_ControllableForPlayerId;
                bool inRange = false;
                foreach (var f in _footprints)
                {
                    if (f.Owner == owner && DistanceSquaredToFootprint(u->r_CurrentTilePositionX, u->r_CurrentTilePositionY, f.MinX, f.MinY, f.MaxX, f.MaxY) <= radiusSq)
                    {
                        inRange = true;
                        break;
                    }
                }
                if (!inRange) continue;

                if (s.OutOfCombatTicks > 0)
                {
                    int last = LastCombatTick(id);
                    // A stamp from the future belongs to an earlier map (cleared on unload; belt and braces).
                    if (last >= 0 && last <= tick && tick - last < s.OutOfCombatTicks) continue;
                }

                // Same writes as the game's Bedouin heal (RVA 0x17491A).
                int after = (int)Math.Min(max, (long)hp + HealAmount(max, s));
                u->r_CurrentHealth = (uint)after;
                int percent = max == 0 ? 100 : (int)((long)after * 100 / max);
                u->r_CurrentHealthPercentage = (ushort)percent;
                *(ushort*)&u->r_HealthBarBlocks = (ushort)(after >= max ? 10 : percent / 10);
                healed++;

                if (RecordHeals)
                    lock (HealLog) HealLog.Add(new HealRecord { Tick = tick, UnitId = id, Before = hp, After = after, Max = max });
                if (!_firstHealLogged)
                {
                    _firstHealLogged = true;
                    Plugin.Logger.LogInfo($"[ApothecaryHealing] First heal this map: tick {tick}, player {owner}'s {u->r_UnitChimp} (unit {id}) {hp} -> {after} of {max} HP.");
                }
            }
            if (healed > 0 && (global::CrusaderDETweaker.Config.BepInEx.BepInExConfigManager.DebugLogging?.Value ?? false))
                Plugin.Logger.LogInfo($"[ApothecaryHealing] Tick {tick}: {_footprints.Count} working apothecaries healed {healed} unit(s).");
        }
    }
}
