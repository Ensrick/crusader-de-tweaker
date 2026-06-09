// Config/Core/IConfigSystem.cs
//
// PURPOSE: Unified interface for all configuration systems (TOML and CSV).
//
// USAGE:
// - Implemented by: UnitConfigSystem, StructureConfigSystem, DamageMatrixConfigSystem
// - Used by: ConfigManager to orchestrate initialization of all config systems
//
// IMPORTANT FOR AI AGENTS:
// - All config systems must implement this interface
// - ConfigManager uses this interface to manage all systems uniformly
// - GenerateDefaults(): Creates template files if missing (won't overwrite existing)
// - Load(): Reads and applies config files
// - Validate(): Optional validation step (can return true if not needed)
//
namespace CrusaderDETweaker.Config.Core
{
    /// <summary>
    /// Unified interface for all configuration systems.
    /// 
    /// Provides a consistent way to initialize, generate defaults, and load configurations.
    /// 
    /// All config systems (TOML and CSV) implement this interface, allowing ConfigManager
    /// to orchestrate initialization uniformly regardless of config type.
    /// </summary>
    internal interface IConfigSystem
    {
        /// <summary>
        /// The name of this config system (for logging and identification).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Generate default configuration files if they don't exist.
        /// </summary>
        void GenerateDefaults();

        /// <summary>
        /// Load and apply configuration from files.
        /// </summary>
        void Load();

        /// <summary>
        /// Whether this config system has been successfully loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Optional validation step after loading.
        /// Returns true if validation passed, false otherwise.
        /// Systems that don't need validation should return true.
        /// </summary>
        /// <returns>True if validation passed, false otherwise</returns>
        bool Validate();
    }
}

