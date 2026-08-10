// Config/BepInEx/Core/IBepInExConfigSystem.cs
//
// PURPOSE: Unified interface for all BepInEx configuration systems.
//
// USAGE:
// - Implemented by: UnitMultipliersConfig, StructureMultipliersConfig, WallCostConfig
// - Used by: BepInExConfigManager to orchestrate initialization
//
// IMPORTANT FOR AI AGENTS:
// - BepInEx configs are different from TOML/CSV - they use ConfigFile/ConfigEntry
// - Two-phase initialization: Initialize() binds ConfigEntry, Apply() subscribes to events
// - All BepInEx config systems must implement this interface
// - Similar to IConfigSystem but for BepInEx-specific configs
//
using BepInEx.Configuration;

namespace CrusaderDETweaker.Config.BepInEx.Core
{
    /// <summary>
    /// Unified interface for all BepInEx configuration systems.
    /// 
    /// BepInEx configs are different from TOML/CSV configs - they use ConfigFile and ConfigEntry,
    /// and often require real-time event hooks for runtime modifications.
    /// 
    /// Two-phase initialization:
    /// - Initialize(): Binds ConfigEntry properties to ConfigFile
    /// - Apply(): Subscribes to event hooks and applies runtime modifications
    /// </summary>
    internal interface IBepInExConfigSystem
    {
        /// <summary>
        /// The name of this config system (for logging and identification).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initialize this config system by binding ConfigEntry properties.
        /// Called once during plugin initialization.
        /// </summary>
        /// <param name="config">The BepInEx ConfigFile to bind entries to</param>
        void Initialize(ConfigFile config);

        /// <summary>
        /// Apply this config system's settings to the game.
        /// For multipliers, this typically means subscribing to event hooks.
        /// Called after Initialize().
        /// </summary>
        void Apply();
    }
}

