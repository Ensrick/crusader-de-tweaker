using System;
using System.IO;
using CrusaderDETweaker.Config.ConfigToml;
using CrusaderDETweaker.Config.Toml.Units;
using CrusaderDETweaker.Config.Toml.Structures;
using SHCDESE.Interop;
using Tomlyn;
using Tomlyn.Model;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Loads TOML configuration files using the property handler system.
    /// </summary>
    internal static class ConfigLoader
    {
        /// <summary>
        /// Load and apply unit configurations from TOML file.
        /// </summary>
        internal static void ApplyAllUnitConfigs()
        {
            if (!File.Exists(ConfigPaths.Units)) return;

            try
            {
                int processedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                var tomlString = File.ReadAllText(ConfigPaths.Units);
                var tomlModel = Toml.ToModel(tomlString);

                foreach (var kvp in tomlModel)
                {
                    // Parse unit name
                    if (!Enum.TryParse<eChimps>(kvp.Key, out var unit))
                    {
                        Plugin.Logger.LogWarning($"Unknown unit in config: {kvp.Key}");
                        continue;
                    }

                    // Skip non-modifiable units
                    if (Systems.StatsUnits.NonModableUnits.Contains(unit))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Process this unit's properties
                    if (kvp.Value is TomlTable unitTable)
                    {
                        foreach (var propertyKvp in unitTable)
                        {
                            string propertyName = propertyKvp.Key;
                            object propertyValue = propertyKvp.Value;

                            // Get the appropriate property handler
                            var handler = UnitPropertyRegistry.Instance.GetByName(propertyName);

                            if (handler == null)
                            {
                                Plugin.Logger.LogWarning($"Unknown property '{propertyName}' for unit {unit}");
                                continue;
                            }

                            // Try to apply the value
                            try
                            {
                                if (!handler.TryLoadFromObject(unit, propertyValue))
                                {
                                    Plugin.Logger.LogDebug($"Skipped {propertyName} for {unit}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger.LogError($"Failed to apply {propertyName} for {unit}: {ex.Message}");
                                errorCount++;
                            }
                        }

                        processedCount++;
                    }
                }

                Plugin.Logger.LogInfo($"Applied unit configs: Processed={processedCount}, Skipped={skippedCount}, Errors={errorCount}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load unit configs: {ex}");
            }
        }

        /// <summary>
        /// Load and apply structure configurations from TOML file.
        /// </summary>
        internal static void ApplyAllStructureConfigs()
        {
            if (!File.Exists(ConfigPaths.Structures)) return;

            try
            {
                int processedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                var tomlString = File.ReadAllText(ConfigPaths.Structures);
                var tomlModel = Toml.ToModel(tomlString);

                foreach (var kvp in tomlModel)
                {
                    // Parse structure name
                    if (!Enum.TryParse<eStructs>(kvp.Key, out var structure))
                    {
                        Plugin.Logger.LogWarning($"Unknown structure in config: {kvp.Key}");
                        continue;
                    }

                    // Skip non-modifiable structures
                    if (Systems.StatsStructures.NonModableStructures.Contains(structure))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Process this structure's properties
                    if (kvp.Value is TomlTable structureTable)
                    {
                        foreach (var propertyKvp in structureTable)
                        {
                            string propertyName = propertyKvp.Key;
                            object propertyValue = propertyKvp.Value;

                            // Get the appropriate property handler
                            var handler = StructurePropertyRegistry.Instance.GetByName(propertyName);

                            if (handler == null)
                            {
                                Plugin.Logger.LogWarning($"Unknown property '{propertyName}' for structure {structure}");
                                continue;
                            }

                            // Try to apply the value
                            try
                            {
                                if (!handler.TryLoadFromObject(structure, propertyValue))
                                {
                                    Plugin.Logger.LogDebug($"Skipped {propertyName} for {structure}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger.LogError($"Failed to apply {propertyName} for {structure}: {ex.Message}");
                                errorCount++;
                            }
                        }

                        processedCount++;
                    }
                }

                Plugin.Logger.LogInfo($"Applied structure configs: Processed={processedCount}, Skipped={skippedCount}, Errors={errorCount}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load structure configs: {ex}");
            }
        }
    }
}