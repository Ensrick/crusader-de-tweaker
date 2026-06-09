// Config/Toml/Core/ConfigFileHelper.cs
//
// PURPOSE: Centralizes config file I/O operations with validation and error handling.
//
// USAGE:
// - ValidateConfigFilePath(): Validates file paths before use (prevents security issues)
// - ConfigFileExists(): Checks if config file exists (with path validation)
// - ReadConfigFile(): Reads config file content (with validation and error handling)
// - WriteConfigFile(): Writes config file content (creates directory if needed)
//
// IMPORTANT FOR AI AGENTS:
// - All config file operations should go through this class for consistency
// - Path validation prevents security issues (path traversal, invalid characters, etc.)
// - Centralized error handling ensures consistent behavior across all config operations
//
using System;
using System.IO;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Helper utilities for config file operations.
    /// 
    /// Centralizes common file operations to:
    /// - Reduce duplication across config systems
    /// - Provide consistent validation and error handling
    /// - Enable future enhancements (logging, caching, etc.)
    /// 
    /// All config file I/O should use these methods rather than direct File operations.
    /// </summary>
    internal static class ConfigFileHelper
    {
        /// <summary>
        /// Validate that a config file path is safe to use.
        /// Checks for null, empty, invalid characters, and path length.
        /// </summary>
        /// <param name="filePath">Path to validate</param>
        /// <param name="errorMessage">Output error message if validation fails</param>
        /// <returns>True if path is valid, false otherwise</returns>
        public static bool ValidateConfigFilePath(string filePath, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMessage = "Config file path is null or empty";
                return false;
            }

            // Check for invalid path characters
            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                errorMessage = $"Config file path contains invalid characters: {filePath}";
                return false;
            }

            // Check path length (Windows MAX_PATH is 260, but we'll be more conservative)
            if (filePath.Length > 250)
            {
                errorMessage = $"Config file path is too long ({filePath.Length} characters, max 250): {filePath}";
                return false;
            }

            // Try to get full path to validate it's well-formed
            try
            {
                Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                errorMessage = $"Config file path is invalid: {ex.Message}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if a config file exists. Provides a centralized point for file existence checks.
        /// Validates the path before checking existence.
        /// </summary>
        /// <param name="filePath">Path to the config file</param>
        /// <returns>True if the file exists, false otherwise</returns>
        public static bool ConfigFileExists(string filePath)
        {
            if (!ValidateConfigFilePath(filePath, out var errorMessage))
            {
                Core.ErrorLogging.LogConfigLoadException("Config File", new ArgumentException(errorMessage));
                return false;
            }

            return File.Exists(filePath);
        }

        /// <summary>
        /// Read all text from a config file. Provides a centralized point for file reading.
        /// Validates the path before reading.
        /// </summary>
        /// <param name="filePath">Path to the config file</param>
        /// <returns>Contents of the file as a string</returns>
        /// <exception cref="ArgumentException">Thrown if file path is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown if file does not exist</exception>
        public static string ReadConfigFile(string filePath)
        {
            if (!ValidateConfigFilePath(filePath, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Config file not found: {filePath}", filePath);
            }

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Write all text to a config file. Provides a centralized point for file writing.
        /// Validates the path and creates directory if needed.
        /// </summary>
        /// <param name="filePath">Path to the config file</param>
        /// <param name="content">Content to write to the file</param>
        /// <exception cref="ArgumentException">Thrown if file path is invalid</exception>
        public static void WriteConfigFile(string filePath, string content)
        {
            if (!ValidateConfigFilePath(filePath, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(filePath));
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        }
    }
}

