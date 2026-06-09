// Config/Toml/Core/ErrorHandlingHelper.cs
//
// PURPOSE: Standardized error handling for TOML config operations.
//
// USAGE:
// - TryExecute(): Wraps void operations (Set operations) with error handling
// - TryGetValue(): Wraps value-returning operations (Get operations) with error handling
// - TryGetValueWithIndexCheck(): Special handling for IndexOutOfRangeException (common with game API arrays)
//
// IMPORTANT FOR AI AGENTS:
// - All TOML property handlers should use these methods for API calls
// - Provides consistent error logging format: "[Operation] Failed for Entity: Exception - Message"
// - Handles IndexOutOfRangeException specially (expected for some units that don't have array data)
// - Returns safe defaults on error (doesn't throw, allows config loading to continue)
//
using System;
using BepInEx.Logging;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Provides standardized error handling patterns for configuration operations.
    /// 
    /// Ensures consistent error logging and context across all config systems.
    /// All TOML property handlers should use these methods when calling game API.
    /// 
    /// Benefits:
    /// - Consistent error message format
    /// - Graceful degradation (returns defaults, doesn't crash)
    /// - Special handling for common API issues (IndexOutOfRangeException)
    /// </summary>
    internal static class ErrorHandlingHelper
    {
        /// <summary>
        /// Executes an API operation with standardized error handling.
        /// Logs errors with full context and returns whether the operation succeeded.
        /// </summary>
        /// <param name="operation">The operation to execute (e.g., "Get Health", "Set Speed")</param>
        /// <param name="entityName">The entity name (unit/structure name)</param>
        /// <param name="action">The action to execute</param>
        /// <returns>True if the operation succeeded, false otherwise</returns>
        public static bool TryExecute(string operation, string entityName, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[{operation}] Failed for {entityName}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes an API operation that returns a value, with standardized error handling.
        /// Logs errors with full context and returns a default value on failure.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation to execute (e.g., "Get Health", "Get Speed")</param>
        /// <param name="entityName">The entity name (unit/structure name)</param>
        /// <param name="func">The function to execute</param>
        /// <param name="defaultValue">The default value to return on failure</param>
        /// <param name="logAsWarning">If true, logs as warning instead of error (for expected failures)</param>
        /// <returns>The result of the operation, or defaultValue on failure</returns>
        public static T TryGetValue<T>(string operation, string entityName, Func<T> func, T defaultValue = default(T), bool logAsWarning = false)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                string message = $"[{operation}] Failed for {entityName}: {ex.GetType().Name} - {ex.Message}";
                
                if (logAsWarning)
                {
                    Plugin.Logger.LogWarning(message);
                }
                else
                {
                    Plugin.Logger.LogError(message);
                }
                
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an API operation that returns a value, with standardized error handling.
        /// Uses an out parameter to indicate success/failure (TryParse-style pattern).
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation to execute (e.g., "Get Health", "Get Speed")</param>
        /// <param name="entityName">The entity name (unit/structure name)</param>
        /// <param name="func">The function to execute</param>
        /// <param name="value">The output value</param>
        /// <param name="defaultValue">The default value to use on failure</param>
        /// <param name="logAsWarning">If true, logs as warning instead of error (for expected failures)</param>
        /// <returns>True if the operation succeeded, false otherwise</returns>
        public static bool TryGetValueWithResult<T>(string operation, string entityName, Func<T> func, out T value, T defaultValue = default(T), bool logAsWarning = false)
        {
            try
            {
                value = func();
                return true;
            }
            catch (Exception ex)
            {
                string message = $"[{operation}] Failed for {entityName}: {ex.GetType().Name} - {ex.Message}";
                
                if (logAsWarning)
                {
                    Plugin.Logger.LogWarning(message);
                }
                else
                {
                    Plugin.Logger.LogError(message);
                }
                
                value = defaultValue;
                return false;
            }
        }

        /// <summary>
        /// Handles IndexOutOfRangeException specifically for array-based API calls.
        /// This is a common issue with units that don't have data in the game's fixed-size arrays.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="entityName">The entity name</param>
        /// <param name="func">The function to execute</param>
        /// <param name="defaultValue">The default value to return on IndexOutOfRangeException</param>
        /// <param name="value">The output value</param>
        /// <returns>True if the operation succeeded, false if IndexOutOfRangeException occurred</returns>
        public static bool TryGetValueWithIndexCheck<T>(string operation, string entityName, Func<T> func, T defaultValue, out T value)
        {
            try
            {
                value = func();
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                // Expected for units that don't have data in the game's array
                Plugin.Logger.LogWarning($"[{operation}] {entityName} does not have data in game's array (IndexOutOfRangeException). Using default value.");
                value = defaultValue;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[{operation}] Failed for {entityName}: {ex.GetType().Name} - {ex.Message}");
                value = defaultValue;
                return false;
            }
        }
    }
}

