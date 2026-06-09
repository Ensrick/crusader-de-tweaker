// Config/Toml/Systems/BaseConfigSystem.cs
using CrusaderDETweaker.Config.Core;

namespace CrusaderDETweaker.Config.Toml.Systems
{
    /// <summary>
    /// Base class for TOML config system wrappers that implement IConfigSystem.
    /// Provides common implementation for IsLoaded tracking and basic validation.
    /// </summary>
    internal abstract class BaseConfigSystem : IConfigSystem
    {
        /// <summary>
        /// The name of this config system (for logging and identification).
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Whether this config system has been successfully loaded.
        /// </summary>
        public bool IsLoaded { get; protected set; }

        /// <summary>
        /// Generate default configuration files if they don't exist.
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract void GenerateDefaults();

        /// <summary>
        /// Load and apply configuration from files.
        /// Must be implemented by derived classes. Derived classes should set IsLoaded = true after successful loading.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Optional validation step after loading.
        /// Returns true if validation passed, false otherwise.
        /// Default implementation returns true (no validation required).
        /// Override to add custom validation logic.
        /// </summary>
        /// <returns>True if validation passed, false otherwise</returns>
        public virtual bool Validate()
        {
            // No validation required by default
            return true;
        }
    }
}

