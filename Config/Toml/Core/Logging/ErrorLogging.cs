// Config/Toml/Core/ErrorLogging.cs
//
// PURPOSE: Standardized error logging for TOML config operations.
//
// USAGE:
// - LogGenerationSkipped(): Logs when property generation is skipped (non-critical)
// - LogUnknownProperty(): Logs unknown properties (differentiates obsolete vs truly unknown)
// - LogValidationFailure(): Logs validation failures
// - LogConfigLoadException(): Logs config file loading errors
//
// IMPORTANT FOR AI AGENTS:
// - Provides consistent error message format across all config systems
// - Differentiates obsolete properties (Debug level) from truly unknown (Warning level)
// - All logging goes through Plugin.Logger with appropriate log levels
// - Extracted from ConfigHelpers to improve modularity
//
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Standardized error logging for config operations.
    /// 
    /// Provides consistent error handling across all config systems.
    /// Ensures all error messages follow the same format and use appropriate log levels.
    /// 
    /// Special handling for obsolete properties (logged at Debug level) vs truly
    /// unknown properties (logged at Warning level).
    /// </summary>
    internal static class ErrorLogging
    {
        /// <summary>
        /// Known obsolete properties that have been removed from the system.
        /// These are logged at Debug level instead of Warning since they're expected in old configs.
        /// 
        /// MAINTENANCE NOTE FOR AI AGENTS:
        /// When removing a property handler from a registry (e.g., UnitPropertyRegistry), add its
        /// name here to prevent log spam from old config files. This list should be kept in sync with
        /// removed handlers.
        /// 
        /// Current obsolete properties:
        /// - ArmorValue, MeleeArmorValue, RangedArmorValue, EunuchAoeArmorValue:
        ///   Replaced by MeleeArmorMultiplier and RangedArmorMultipliers in v1.0
        /// </summary>
        private static readonly HashSet<string> ObsoleteProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Armor system v1.0 (replaced with multiplier-based system)
            "ArmorValue",
            "MeleeArmorValue", 
            "RangedArmorValue",
            "EunuchAoeArmorValue"
        };

        /// <summary>
        /// Log a non-critical generation failure (property doesn't apply, skipped, etc.).
        /// Uses LogDebug level as these are expected and non-critical.
        /// </summary>
        public static void LogGenerationSkipped(string propertyName, object entity, string reason)
        {
            Plugin.Logger.LogDebug($"Could not generate {propertyName} for {entity}: {reason}");
        }

        /// <summary>
        /// Log a generation exception (unexpected error during generation).
        /// Uses LogWarning level so failures are visible even without debug logging.
        /// </summary>
        public static void LogGenerationException(string propertyName, object entity, Exception ex)
        {
            Plugin.Logger.LogWarning($"[ConfigGen] Error generating {propertyName} for {entity}: {ex.GetType().Name} - {ex.Message}");
        }

        /// <summary>
        /// Log that the game API could not return a value for a property during generation.
        /// A commented placeholder is written to the config file instead.
        /// </summary>
        public static void LogGenerationApiFailure(string propertyName, object entity)
        {
            Plugin.Logger.LogWarning($"[ConfigGen] Could not read {propertyName} for {entity} from game API — placeholder comment written to config");
        }

        /// <summary>
        /// Log that the game API returned a value that fails validation for a property during generation.
        /// The value is still written to the config file with a warning comment.
        /// </summary>
        public static void LogGenerationInvalidApiValue(string propertyName, object entity, object value)
        {
            Plugin.Logger.LogWarning($"[ConfigGen] {propertyName} for {entity} has unexpected game default value ({value}) — written to config with note");
        }

        /// <summary>
        /// Log a validation failure (invalid value provided).
        /// Uses LogWarning level as this indicates a configuration issue.
        /// </summary>
        public static void LogValidationFailure(string propertyName, object entity, object value, string reason = null)
        {
            if (string.IsNullOrEmpty(reason))
            {
                Plugin.Logger.LogWarning($"{propertyName} for {entity}: Invalid value {value}");
            }
            else
            {
                Plugin.Logger.LogWarning($"{propertyName} for {entity}: Invalid value {value} - {reason}");
            }
        }

        /// <summary>
        /// Log a property loading exception (failed to apply property value).
        /// Uses LogError level as this indicates a failure to apply configuration.
        /// </summary>
        public static void LogPropertyLoadException(string propertyName, object entity, Exception ex)
        {
            Plugin.Logger.LogError($"Failed to apply {propertyName} for {entity}: {ex.Message}");
        }

        /// <summary>
        /// Log a config file loading exception (failed to load entire config file).
        /// Uses LogError level as this is a critical failure.
        /// </summary>
        public static void LogConfigLoadException(string configName, Exception ex)
        {
            Plugin.Logger.LogError($"Failed to load {configName} config: {ex}");
        }

        /// <summary>
        /// Log a config file generation exception (failed to generate default config).
        /// Uses LogError level as this is a critical failure.
        /// </summary>
        public static void LogConfigGenerationException(string configName, Exception ex)
        {
            Plugin.Logger.LogError($"Failed to generate {configName} config: {ex}");
        }

        /// <summary>
        /// Log a missing config file warning (file not found, using defaults).
        /// Uses LogWarning level as this is recoverable (defaults will be used).
        /// </summary>
        public static void LogMissingConfigFile(string filePath, string fallbackMessage = "Using default values")
        {
            Plugin.Logger.LogWarning($"Config file not found: {filePath}");
            Plugin.Logger.LogInfo(fallbackMessage);
        }

        /// <summary>
        /// Log an unknown entity in config (entity name doesn't match any known entity).
        /// Uses LogWarning level as this indicates a configuration issue.
        /// </summary>
        public static void LogUnknownEntity(string entityTypeName, string entityName)
        {
            Plugin.Logger.LogWarning($"Unknown {entityTypeName} in config: {entityName}");
        }

        /// <summary>
        /// Log an unknown property in config (property name doesn't match any handler).
        /// Uses LogWarning level for truly unknown properties, LogDebug for known obsolete properties.
        /// </summary>
        public static void LogUnknownProperty(string entityTypeName, object entity, string propertyName)
        {
            // Check if this is a known obsolete property
            if (ObsoleteProperties.Contains(propertyName))
            {
                // Log at Debug level - these are expected in old configs and are harmless
                Plugin.Logger.LogDebug($"Obsolete property '{propertyName}' for {entityTypeName} {entity} (ignored - property has been removed)");
            }
            else
            {
                // Log at Warning level - this is a truly unknown property
                Plugin.Logger.LogWarning($"Unknown property '{propertyName}' for {entityTypeName} {entity}");
            }
        }

        /// <summary>
        /// Log a skipped property (property handler returned false, but no error).
        /// Uses LogDebug level as this is expected behavior (property may not apply).
        /// </summary>
        public static void LogPropertySkipped(string propertyName, object entity)
        {
            Plugin.Logger.LogDebug($"Skipped {propertyName} for {entity}");
        }
    }
}

