// Config/BepInEx/Core/IBepInExConfigSystem.cs
using BepInEx.Configuration;

namespace CrusaderDETweaker.Config.BepInEx.Core
{
    /// <summary>
    /// Unified interface for all BepInEx configuration systems.
    /// BepInEx configs are different from TOML/CSV configs - they use ConfigFile and ConfigEntry,
    /// and often require real-time event hooks for runtime modifications.
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

        /// <summary>
        /// Whether this config system has been successfully initialized.
        /// </summary>
        bool IsInitialized { get; }
    }
}

