// Config/Core/UnitTransitionDispatcher.cs
//
// PURPOSE: One-tick deferral bridge for SHCDE-SE's OnUnitTransition event.
//
// WHY THIS EXISTS:
//   Recruiting (Barracks / Mercenary Post), worker assignment, and disband do NOT go through
//   c_game_unit_spawn_ex, so OnUnitCreate never fires for those units (see MakeTroopRecruitHook.cs
//   for the verified analysis). SHCDE-SE 1.35+ raises OnUnitTransition on those paths instead,
//   but it raises at the START of the native transformation (Pre phase, so subscribers can
//   replace NextUnitType). At that moment the unit still carries its OLD type's stats; the game
//   overwrites the unit struct with the new type's template stats right after the hook returns,
//   so any per-unit stat write made inside the hook is lost.
//
//   This dispatcher queues each transition and re-raises it on a later game tick (SE
//   GameTimeManagerAPI.OnTick, raised inside the native game loop), when the transformation is
//   complete and per-unit reads/writes (GetMaxHealth, stable linking) see the final unit.
//
// 2.11.7 (GitLab #13, plan findings FWP-E-05 / FWP-C-06): drained from the game tick, no longer from
//   Plugin.Update (screen frames). Both consumers write the game (health, stable slots), so in a
//   multiplayer match they must run on the same game tick on every PC; a frame-driven drain landed on
//   whatever tick each PC happened to be at. An entry queued during the ticks after OnTick number N
//   is raised at OnTick N + 1, in queue order. The queue is dropped at every map unload (its unit ids
//   belong to the old map). Plugin.Update may also never run (FWP-C-06); the tick does not depend on it.
//
// CONSUMERS:
//   - HealthMultiplierHandler: applies UnitHealthMultiplier to recruited/transitioned units.
//   - ConfigLoader stable tracking: links RequiresHorse recruits to a stable slot.
//
using System;
using System.Collections.Generic;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Core
{
    /// <summary>
    /// Defers OnUnitTransition events so subscribers see the fully transformed unit.
    /// Subscribe via <see cref="TransitionSettled"/>; raised from the game tick (SE OnTick) after the
    /// native transformation has completed.
    /// </summary>
    internal static class UnitTransitionDispatcher
    {
        private struct PendingTransition
        {
            public int UnitId;
            public eChimps NewUnitType;
            public int Tick;
        }

        private static readonly object _lock = new object();
        private static readonly List<PendingTransition> _queue = new List<PendingTransition>();
        private static readonly List<PendingTransition> _ready = new List<PendingTransition>();

        // Number of game ticks seen (SE OnTick calls) since the last map unload. An entry is stamped with it when
        // queued and raised at the first OnTick that counts higher, i.e. on the next game tick.
        private static int _tick;
        private static bool _initialized;

        /// <summary>
        /// Raised from the game tick after a unit finished transforming into a new type:
        /// recruit at Barracks/Mercenary Post, worker assignment, or disband back to peasant.
        /// Args: (unitId, new unit type).
        /// </summary>
        internal static event Action<int, eChimps> TransitionSettled;

        /// <summary>
        /// Subscribe to the SHCDE-SE hook. Safe at LibraryLoaded: this only registers an event
        /// subscription; the game raises transitions exclusively in-session.
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // SE raises transitions once, at Pre phase (so the hook may rewrite NextUnitType).
            // Filter on Pre so a future Post raise cannot double-queue a unit.
            UnitR3EventHooks.OnUnitTransition.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    lock (_lock)
                    {
                        _queue.Add(new PendingTransition
                        {
                            UnitId = args.UnitId,
                            NewUnitType = args.NextUnitType,
                            Tick = _tick
                        });
                    }
                });

            GameTimeManagerAPI.Instance.OnTick += _ => Drain();

            MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ =>
                {
                    lock (_lock)
                    {
                        _queue.Clear();
                        _tick = 0;
                    }
                });

            Plugin.Logger.LogInfo("[Transitions] OnUnitTransition deferral dispatcher initialized (raised on the game tick).");
        }

        /// <summary>
        /// Called on every game tick (SE OnTick). Raises TransitionSettled, in queue order, for every
        /// transition queued before this tick.
        /// </summary>
        internal static void Drain()
        {
            lock (_lock)
            {
                int current = ++_tick;
                if (_queue.Count == 0) return;
                int kept = 0;
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (_queue[i].Tick < current) _ready.Add(_queue[i]);
                    else _queue[kept++] = _queue[i];
                }
                _queue.RemoveRange(kept, _queue.Count - kept);
            }

            if (_ready.Count == 0) return;

            var handler = TransitionSettled;
            if (handler != null)
            {
                foreach (var t in _ready)
                {
                    try
                    {
                        handler(t.UnitId, t.NewUnitType);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning(
                            $"[Transitions] Settled-handler error for unit {t.UnitId} ({t.NewUnitType}): {ex.Message}");
                    }
                }
            }
            _ready.Clear();
        }
    }
}
