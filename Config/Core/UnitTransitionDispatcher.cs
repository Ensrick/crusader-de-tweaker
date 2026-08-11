// Config/Core/UnitTransitionDispatcher.cs
//
// PURPOSE: One-frame deferral bridge for SHCDE-SE's OnUnitTransition event.
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
//   This dispatcher queues each transition and re-raises it from the plugin's Unity update loop
//   (Plugin.Update -> Drain) only after at least one full Update interval has passed, when the
//   transformation is complete and per-unit reads/writes (GetMaxHealth, stable linking) see the
//   final unit.
//
// CONSUMERS:
//   - HealthMultiplierHandler: applies UnitHealthMultiplier to recruited/transitioned units.
//   - ConfigLoader stable tracking: links RequiresHorse recruits to a stable slot.
//
using System;
using System.Collections.Generic;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Core
{
    /// <summary>
    /// Defers OnUnitTransition events so subscribers see the fully transformed unit.
    /// Subscribe via <see cref="TransitionSettled"/>; raised from Plugin.Update() after the
    /// native transformation has completed.
    /// </summary>
    internal static class UnitTransitionDispatcher
    {
        private struct PendingTransition
        {
            public int UnitId;
            public eChimps NewUnitType;
            public int Frame;
        }

        private static readonly object _lock = new object();
        private static readonly List<PendingTransition> _queue = new List<PendingTransition>();
        private static readonly List<PendingTransition> _ready = new List<PendingTransition>();

        // Our own frame counter (incremented per Drain). The native hook may run off the Unity
        // main thread, where UnityEngine.Time is not accessible, so we count Drain calls instead.
        // An entry queued during frame N is processed at the second Drain after it (N+2), which
        // guarantees a full Update-to-Update interval passed and the transformation finished even
        // if the sim runs concurrently with Update.
        private static int _frame;
        private static bool _initialized;

        /// <summary>
        /// Raised from the Unity update loop after a unit finished transforming into a new type:
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
                            Frame = _frame
                        });
                    }
                });

            Plugin.Logger.LogInfo("[Transitions] OnUnitTransition deferral dispatcher initialized.");
        }

        /// <summary>
        /// Called by Plugin.Update() every frame. Raises TransitionSettled for every transition
        /// queued at least one full Drain interval ago.
        /// </summary>
        internal static void Drain()
        {
            if (_queue.Count == 0) return; // unsynchronized fast path; a late add waits one frame

            lock (_lock)
            {
                int current = _frame++;
                for (int i = _queue.Count - 1; i >= 0; i--)
                {
                    if (_queue[i].Frame < current)
                    {
                        _ready.Add(_queue[i]);
                        _queue.RemoveAt(i);
                    }
                }
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
