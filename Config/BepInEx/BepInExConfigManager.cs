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

            // PHASE 1: Initialize all systems (bind ConfigEntry properties)
            ConfigSystemInitializer.InitializeAll(ConfigSystems, config);

            // PHASE 2: Apply all systems (subscribe to event hooks, apply API changes, etc.)
            ConfigSystemInitializer.ApplyAll(ConfigSystems);

            // Cache config system instances for static accessors
            _unitMultipliersConfig = ConfigSystems.OfType<UnitMultipliersConfig>().FirstOrDefault();
            _structureMultipliersConfig = ConfigSystems.OfType<StructureMultipliersConfig>().FirstOrDefault();
            _wallCostConfig = ConfigSystems.OfType<WallCostConfig>().FirstOrDefault();
            _fireAndHealMultipliersConfig = ConfigSystems.OfType<FireAndHealMultipliersConfig>().FirstOrDefault();
        }
    }
}

