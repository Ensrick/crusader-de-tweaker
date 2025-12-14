// Systems/UnitDamageRegistry.cs
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    /// <summary>
    /// Registry that holds damage data for all units.
    /// This will be our single source of truth during development/testing.
    /// Later, this can be populated from TOML config instead of hardcoded values.
    /// </summary>
    internal static class UnitDamageRegistry
    {
        private static Dictionary<eChimps, UnitDamageData> _registry;
        private static Dictionary<eChimps, UnitDamageData> _originalRegistrySnapshot; // Snapshot before TOML loads
        private static bool _isInitialized = false;

        /// <summary>
        /// Get damage data for a specific unit.
        /// Returns null if unit not found.
        /// </summary>
        public static UnitDamageData GetUnitData(eChimps unit)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            return _registry.TryGetValue(unit, out var data) ? data : null;
        }

        /// <summary>
        /// Check if a unit has damage data registered.
        /// </summary>
        public static bool HasData(eChimps unit)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            return _registry.ContainsKey(unit);
        }

        /// <summary>
        /// Initialize the registry with all unit damage data.
        /// This is where we'll hardcode values during testing, then later load from TOML.
        /// </summary>
        private static void Initialize()
        {
            _registry = new Dictionary<eChimps, UnitDamageData>();
            _isInitialized = true;  // Set BEFORE calling initializer to prevent recursion

            // Populate with unit data using the initializer
            UnitDamageRegistryInitializer.Initialize();
        }

        /// <summary>
        /// Register a unit's damage data (for testing/debugging).
        /// </summary>
        public static void RegisterUnit(UnitDamageData data)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            _registry[data.Unit] = data;
        }

        /// <summary>
        /// Clear all registered data (for testing).
        /// </summary>
        public static void Clear()
        {
            _registry?.Clear();
            _isInitialized = false;
        }

        /// <summary>
        /// Get the number of units registered in the damage system.
        /// </summary>
        public static int GetRegisteredCount()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            return _registry.Count;
        }

        /// <summary>
        /// Capture a snapshot of the current registry state (original defaults before TOML loads).
        /// This is used for verification to compare against original game values.
        /// </summary>
        public static void CaptureOriginalSnapshot()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (_originalRegistrySnapshot != null)
            {
                // Already captured
                return;
            }

            _originalRegistrySnapshot = new Dictionary<eChimps, UnitDamageData>();

            // Deep copy all unit data
            foreach (var kvp in _registry)
            {
                var originalData = kvp.Value;
                var snapshotData = new UnitDamageData
                {
                    Unit = originalData.Unit,
                    BaseMeleeDamage = originalData.BaseMeleeDamage,
                    ArmorValue = originalData.ArmorValue
                };

                // Copy tags
                foreach (var tag in originalData.Tags)
                {
                    snapshotData.AddTag(tag);
                }

                // Copy special modifiers
                foreach (var modifier in originalData.SpecialModifiers)
                {
                    snapshotData.AddSpecialModifier(modifier.Key, modifier.Value);
                }

                _originalRegistrySnapshot[kvp.Key] = snapshotData;
            }

            Plugin.Logger.LogInfo($"Captured original registry snapshot: {_originalRegistrySnapshot.Count} units");
        }

        /// <summary>
        /// Get the original unit data from the snapshot (before TOML modifications).
        /// Returns null if not found or snapshot not captured.
        /// </summary>
        public static UnitDamageData GetOriginalUnitData(eChimps unit)
        {
            if (_originalRegistrySnapshot == null)
            {
                return null;
            }

            return _originalRegistrySnapshot.TryGetValue(unit, out var data) ? data : null;
        }
    }
}