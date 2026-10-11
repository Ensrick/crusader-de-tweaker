// Config/Core/SpritePool.cs
//
// PURPOSE: Keeps the game's sprite pool from running dry when very many objects are on screen. A safeguard found while
//          investigating GitLab #10 (tower engines without animation at about 8000 units); it is NOT the cause of that
//          report: an empty pool stops the game outright (below), and with 8500 units at most 10344 of 30000 were in use.
//
// GAME FACTS (game 2.8.2):
//   - The Unity side draws every map object with a pooled sprite object (Assembly-CSharp ObjectPool). Pool 0
//     ("general", 30000 objects, ObjectPool.setupGeneralPrefabList) serves units (GameMap.addUpdateChimp: a body plus
//     up to 6 more for extra layers, siege flag, health bar, oil pot and target marker), projectiles and effects
//     (addUpdateFly), building animations, wall fill-ins and pixies; pool 1 (4000) serves trees and rocks. The pools
//     are filled once at startup and never grow.
//   - Only objects the native renderer draws this frame hold a sprite: the draw list builder (CrusaderDE.dll RVA
//     0x41D60, called from DLL_RunTick) adds the objects inside the camera's render bounds, and the sweep at RVA
//     0x19D430 sends a delete for every object it did not draw this frame, which returns the sprite to the pool.
//   - An empty pool: ObjectPool.GetObjectForType logs "Pool Empty" and returns null. A new unit then has no sprite and
//     GameMap.addUpdateChimp dereferences the missing Chimp (chimp.baseSortLayer) with a NullReferenceException that
//     ends GameMap.processTestMap for the rest of that frame's draw list. Director.Update then never returns that render
//     buffer (MemoryBuffers), and after 6 such frames no buffer is free: EngineInterface.run stops calling DLL_RunTick
//     and the game stands still (measured: 6 exceptions, then 21 game ticks in 31 s). The extra layers of an existing
//     unit are silently left out.
//
// WHAT THIS DOES: a MonoMod hook on ObjectPool.GetObjectForType. When the requested pool is empty it first adds
//   GrowBy new objects (same prefab, name and material as ObjectPool.fillObjectPools makes them), up to MaxObjects
//   in total, so the call does not fail. A pool that never runs dry is never touched. Installed once at plugin load
//   (it patches a managed method's entry only; the body runs on the Unity main thread inside the game's frame).
//
// IMPORTANT FOR AI AGENTS:
// - Tests/HeadlessSelfTest.Pools.cs measures the pool use with up to 8500 units and switches Enabled off to show the
//   failure (-Only pools, last phase), on to show the fix. Keep Enabled true in normal play.
// - Pool entries are a struct array (ObjPoolEntry[]): change them through the array element, never a copy.
//
using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace CrusaderDETweaker.Config.Core
{
    internal static class SpritePool
    {
        internal const int GrowBy = 2000, MaxObjects = 150000;

        private delegate GameObject GetObjectDelegate(global::ObjectPool self, int poolNo, string objectType, int containerID, int objID);

        private static Hook _hook;
        private static GetObjectDelegate _trampoline;
        private static FieldInfo _entries;
        private static bool _failureLogged;

        /// <summary>Grow an empty pool before handing out an object (the self-test switches it off for its control phase).</summary>
        internal static bool Enabled = true;

        /// <summary>Objects added per pool since launch (index = pool number).</summary>
        internal static readonly int[] Added = new int[2];

        internal static bool Installed => _hook != null;

        internal static void Install()
        {
            if (_hook != null) return;
            try
            {
                _entries = typeof(global::ObjectPool).GetField("Entries", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo target = typeof(global::ObjectPool).GetMethod("GetObjectForType", BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(int), typeof(string), typeof(int), typeof(int) }, null);
                if (_entries == null || _entries.FieldType != typeof(global::ObjPoolEntry[]) || target == null)
                {
                    Plugin.Logger.LogWarning("[SpritePool] ObjectPool.Entries / GetObjectForType not found (a game update?); the sprite pool keeps the game's fixed size.");
                    return;
                }
                _hook = new Hook(target, (GetObjectDelegate)Detour);
                _trampoline = _hook.GenerateTrampoline<GetObjectDelegate>();
                Plugin.Logger.LogInfo("[SpritePool] Sprite pool hook installed: an empty pool grows by " + GrowBy + " instead of failing.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("[SpritePool] Could not install the sprite pool hook: " + ex.Message);
                _hook = null;
                _trampoline = null;
            }
        }

        private static GameObject Detour(global::ObjectPool self, int poolNo, string objectType, int containerID, int objID)
        {
            if (Enabled)
            {
                try { EnsureFree(self, poolNo); }
                catch (Exception ex)
                {
                    if (!_failureLogged) Plugin.Logger.LogError("[SpritePool] Could not grow sprite pool " + poolNo + ": " + ex.Message);
                    _failureLogged = true;
                }
            }
            return _trampoline(self, poolNo, objectType, containerID, objID);
        }

        /// <summary>Free and total objects of a pool, or false when the pool cannot be read.</summary>
        internal static bool TryGetCounts(int poolNo, out int free, out int total)
        {
            free = total = 0;
            var entries = Entries(global::ObjectPool.instance);
            if (entries == null || poolNo < 0 || poolNo >= entries.Length || entries[poolNo].pool == null) return false;
            free = entries[poolNo].objectsInPool;
            total = entries[poolNo].pool.Length;
            return true;
        }

        // The game creates the array once per ObjectPool (field initializer); cached so the hook costs no reflection per call.
        private static global::ObjectPool _cachedPool;
        private static global::ObjPoolEntry[] _cachedEntries;

        private static global::ObjPoolEntry[] Entries(global::ObjectPool pool)
        {
            if (pool == null || _entries == null) return null;
            if (!ReferenceEquals(pool, _cachedPool))
            {
                _cachedEntries = (global::ObjPoolEntry[])_entries.GetValue(pool);
                _cachedPool = pool;
            }
            return _cachedEntries;
        }

        /// <summary>Adds GrowBy objects to pool <paramref name="poolNo"/> when it is empty; returns how many were added.</summary>
        private static int EnsureFree(global::ObjectPool self, int poolNo)
        {
            var entries = Entries(self);
            if (entries == null || poolNo < 0 || poolNo >= entries.Length) return 0;
            if (entries[poolNo].objectsInPool > 0 || entries[poolNo].pool == null || !(entries[poolNo].Prefab is GameObject prefab)) return 0;
            int add = Math.Min(GrowBy, MaxObjects - entries[poolNo].pool.Length);
            if (add <= 0) return 0;

            // PoolObject stores a returned object at pool[objectsInPool++], so the array must hold every object ever made.
            var bigger = new GameObject[entries[poolNo].pool.Length + add];
            Array.Copy(entries[poolNo].pool, bigger, entries[poolNo].objectsInPool);
            entries[poolNo].pool = bigger;
            entries[poolNo].Count += add;
            for (int i = 0; i < add; i++)
            {
                var obj = UnityEngine.Object.Instantiate(prefab);
                obj.name = prefab.name;
                self.PoolObject(poolNo, obj);
                global::spriteLoader.instance?.setDefaultMaterial(obj);
            }
            if (poolNo < Added.Length) Added[poolNo] += add;
            Plugin.Logger.LogInfo($"[SpritePool] Sprite pool {poolNo} was empty: added {add} objects (now {bigger.Length}; the game makes {bigger.Length - (poolNo < Added.Length ? Added[poolNo] : add)}).");
            return add;
        }
    }
}
