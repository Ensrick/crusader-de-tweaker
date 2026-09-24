// Config/BepInEx/BepInExConfigManager.cs
//
// PURPOSE: Central manager for all BepInEx configuration systems (real-time multipliers).
//
// INITIALIZATION SEQUENCE:
// 1. Initialize all systems (bind ConfigEntry properties to ConfigFile)
// 2. Apply all systems (subscribe to event hooks, apply API changes)
// 3. Cache system instances for static access
//
// IMPORTANT FOR AI AGENTS:
// - BepInEx configs are different from TOML/CSV - they use ConfigFile/ConfigEntry
// - BepInEx configs support real-time event hooks (modify game during gameplay)
// - Two-phase initialization: Initialize() then Apply() (prevents order issues)
// - Config systems: UnitMultipliersConfig, StructureMultipliersConfig, WallCostConfig
// - Similar to ConfigManager but for BepInEx-specific configs
//
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Config.BepInEx.Systems;

namespace CrusaderDETweaker.Config.BepInEx
{
    /// <summary>
    /// Manages initialization of all BepInEx configuration systems.
    /// 
    /// BepInEx configs differ from TOML/CSV configs in that they:
    /// - Use ConfigFile and ConfigEntry for type-safe configuration
    /// - Support real-time event hooks for runtime modifications
    /// - Are accessible via BepInEx's built-in config UI
    /// 
    /// Initialization sequence:
    /// 1. Initialize all systems (bind ConfigEntry properties)
    /// 2. Apply all systems (subscribe to event hooks, apply API changes)
    /// 3. Cache system instances for static access
    /// 
    /// This manager is similar to ConfigManager but handles BepInEx-specific configurations
    /// that require event hooks for real-time modifications (e.g., damage multipliers).
    /// </summary>
    internal static class BepInExConfigManager
    {
        /// <summary>
        /// Gets the debug logging toggle. When true, all event hooks log step-by-step details.
        /// WARNING: Generates very high log volume during gameplay. Use only when diagnosing hook issues.
        /// </summary>
        internal static ConfigEntry<bool> DebugLogging { get; private set; }

        private static readonly IBepInExConfigSystem[] ConfigSystems = new IBepInExConfigSystem[]
        {
            new UnitMultipliersConfig(),
            new StructureMultipliersConfig(),
            new WallCostConfig(),
            new FireAndHealMultipliersConfig()
        };

        private static UnitMultipliersConfig _unitMultipliersConfig;
        private static StructureMultipliersConfig _structureMultipliersConfig;
        private static WallCostConfig _wallCostConfig;
        private static FireAndHealMultipliersConfig _fireAndHealMultipliersConfig;

        /// <summary>
        /// Gets the unit multipliers config system instance.
        /// Returns null if not yet initialized.
        /// </summary>
        internal static UnitMultipliersConfig UnitMultipliers => _unitMultipliersConfig;

        /// <summary>
        /// Gets the structure multipliers config system instance.
        /// Returns null if not yet initialized.
        /// </summary>
        internal static StructureMultipliersConfig StructureMultipliers => _structureMultipliersConfig;

        /// <summary>
        /// Gets the wall cost config system instance.
        /// Returns null if not yet initialized.
        /// </summary>
        internal static WallCostConfig WallCost => _wallCostConfig;

        /// <summary>
        /// Gets the fire damage and Bedouin heal multipliers config system instance.
        /// Returns null if not yet initialized.
        /// </summary>
        internal static FireAndHealMultipliersConfig FireAndHeal => _fireAndHealMultipliersConfig;

        /// <summary>
        /// Initialize all BepInEx configuration systems.
        /// 
        /// This method performs a two-phase initialization:
        /// 1. Initialize phase: Binds ConfigEntry properties to the ConfigFile
        /// 2. Apply phase: Subscribes to event hooks and applies runtime modifications
        /// 
        /// The two-phase approach ensures all config entries are bound before any
        /// event hooks are registered, preventing initialization order issues.
        /// </summary>
        /// <param name="config">The BepInEx ConfigFile to bind entries to</param>
        internal static void Initialize(ConfigFile config)
        {
            Plugin.Logger.LogInfo($"Initializing {ConfigSystems.Length} BepInEx config systems...");

            // Bind debug toggle first so handlers can read it during Apply()
            DebugLogging = config.Bind(
                "Debug",
                "DebugLogging",
                false,
                "Enable step-by-step debug logging for all event hooks.\n" +
                "WARNING: Very high log volume during gameplay. Use only when diagnosing hook issues.\n" +
                "Tip: Changes apply in real-time (no restart needed)."
            );

            // PHASE 1: Initialize all systems (bind ConfigEntry properties)
            ConfigSystemInitializer.InitializeAll(ConfigSystems, config);

            // PHASE 2: Apply all systems (subscribe to event hooks, apply API changes, etc.)
            ConfigSystemInitializer.ApplyAll(ConfigSystems);

            // Cache config system instances for static accessors
            _unitMultipliersConfig = ConfigSystems.OfType<UnitMultipliersConfig>().FirstOrDefault();
            _structureMultipliersConfig = ConfigSystems.OfType<StructureMultipliersConfig>().FirstOrDefault();
            _wallCostConfig = ConfigSystems.OfType<WallCostConfig>().FirstOrDefault();
            _fireAndHealMultipliersConfig = ConfigSystems.OfType<FireAndHealMultipliersConfig>().FirstOrDefault();
            _configFile = config;
        }

        // ------------------------------------------------------------------
        // Multiplayer host config sync (Config/Sync/ConfigSyncManager.cs)
        // ------------------------------------------------------------------

        /// <summary>The synced section. [Debug] is local and never synced.</summary>
        internal const string SyncedSection = "Multipliers";

        private static ConfigFile _configFile;
        private static Dictionary<string, float> _ownValues;
        private static bool _ownSaveOnConfigSet;

        /// <summary>True while the lobby host's multiplier values are in effect.</summary>
        internal static bool HostOverrideActive => _ownValues != null;

        /// <summary>
        /// Current values of every float entry in [Multipliers], by key. Empty before Initialize.
        /// </summary>
        internal static Dictionary<string, float> GetMultiplierValues()
        {
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            if (_configFile == null) return values;
            foreach (var kvp in _configFile)
            {
                if (kvp.Key.Section == SyncedSection && kvp.Value is ConfigEntry<float> entry)
                    values[kvp.Key.Key] = entry.Value;
            }
            return values;
        }

        /// <summary>
        /// Put the host's values into this player's entries for the session WITHOUT saving: autosave is
        /// switched off first and the player's own values are kept for RestoreOwnMultipliers. The .cfg
        /// file on disk is never rewritten. Returns (applied, unknownKeys, missingKeys).
        /// </summary>
        internal static (int applied, List<string> unknown, List<string> missing) ApplyHostMultipliers(IDictionary<string, float> hostValues)
        {
            var unknown = new List<string>();
            var missing = new List<string>();
            if (_configFile == null) return (0, unknown, missing);

            if (_ownValues == null)
            {
                _ownValues = GetMultiplierValues();
                _ownSaveOnConfigSet = _configFile.SaveOnConfigSet;
            }
            _configFile.SaveOnConfigSet = false;

            int applied = 0;
            var local = new Dictionary<string, ConfigEntry<float>>(StringComparer.Ordinal);
            foreach (var kvp in _configFile)
                if (kvp.Key.Section == SyncedSection && kvp.Value is ConfigEntry<float> entry)
                    local[kvp.Key.Key] = entry;

            foreach (var kvp in hostValues)
            {
                if (!local.TryGetValue(kvp.Key, out var entry)) { unknown.Add(kvp.Key); continue; }
                entry.Value = kvp.Value;
                applied++;
            }
            foreach (var key in local.Keys)
            {
                if (!hostValues.ContainsKey(key))
                {
                    // Host did not send it (older/newer layout): use this player's own value.
                    local[key].Value = _ownValues[key];
                    missing.Add(key);
                }
            }
            return (applied, unknown, missing);
        }

        /// <summary>Put this player's own values back and restore autosave. No-op when not overridden.</summary>
        internal static void RestoreOwnMultipliers()
        {
            if (_configFile == null || _ownValues == null) return;
            foreach (var kvp in _configFile)
            {
                if (kvp.Key.Section == SyncedSection && kvp.Value is ConfigEntry<float> entry
                    && _ownValues.TryGetValue(kvp.Key.Key, out float own))
                    entry.Value = own;
            }
            _configFile.SaveOnConfigSet = _ownSaveOnConfigSet;
            _ownValues = null;
        }

        /// <summary>
        /// The template writes that depend on multiplier values (wall costs, unit / building fire, Bedouin
        /// heal), in launch order. Called ONLY from ConfigLoader.ReapplyTemplateConfigs, after the tables
        /// were restored to game values. Never re-subscribes the runtime hooks.
        /// </summary>
        internal static void ReapplyTemplateMultipliers()
        {
            _wallCostConfig?.ReapplyTemplateValues();
            _fireAndHealMultipliersConfig?.ReapplyTemplateValues();
        }
    }
}

