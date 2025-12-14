using System;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.ConfigToml;
using CrusaderDETweaker.Config.Toml.Core;
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
            ApplyConfigs(
                filePath: ConfigPaths.Units,
                registry: UnitPropertyRegistry.Instance,
                nonModifiableEntities: Systems.StatsUnits.NonModableUnits,
                entityTypeName: "unit",
                parseEntity: TryParseUnit
            );
        }

        /// <summary>
        /// Load and apply structure configurations from TOML file.
        /// </summary>
        internal static void ApplyAllStructureConfigs()
        {
            ApplyConfigs(
                filePath: ConfigPaths.Structures,
                registry: StructurePropertyRegistry.Instance,
                nonModifiableEntities: Systems.StatsStructures.NonModableStructures,
                entityTypeName: "structure",
                parseEntity: TryParseStructure
            );
        }

        /// <summary>
        /// Helper method to parse unit enum from string key.
        /// </summary>
        private static eChimps? TryParseUnit(string key)
        {
            if (Enum.TryParse<eChimps>(key, out var unit))
                return unit;
            return (eChimps?)null;
        }

        /// <summary>
        /// Helper method to parse structure enum from string key.
        /// </summary>
        private static eStructs? TryParseStructure(string key)
        {
            if (Enum.TryParse<eStructs>(key, out var structure))
                return structure;
            return (eStructs?)null;
        }

        /// <summary>
        /// Generic method to load and apply configuration files for any entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type (eChimps or eStructs)</typeparam>
        /// <param name="filePath">Path to the TOML config file</param>
        /// <param name="registry">Property registry for this entity type</param>
        /// <param name="nonModifiableEntities">Entities that should be skipped</param>
        /// <param name="entityTypeName">Name of the entity type for logging (e.g., "unit", "structure")</param>
        /// <param name="parseEntity">Function to parse entity name from string, returns null if invalid</param>
        private static void ApplyConfigs<TEntity>(
            string filePath,
            PropertyRegistry<TEntity> registry,
            TEntity[] nonModifiableEntities,
            string entityTypeName,
            Func<string, TEntity?> parseEntity)
            where TEntity : struct
        {
            if (!ConfigFileHelper.ConfigFileExists(filePath)) return;

            try
            {
                int processedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                var tomlString = ConfigFileHelper.ReadConfigFile(filePath);
                var tomlModel = Toml.ToModel(tomlString);

                foreach (var kvp in tomlModel)
                {
                    // Parse entity name
                    var entity = parseEntity(kvp.Key);
                    if (entity == null)
                    {
                        ConfigHelpers.ErrorLogging.LogUnknownEntity(entityTypeName, kvp.Key);
                        continue;
                    }

                    // Skip non-modifiable entities
                    if (nonModifiableEntities.Contains(entity.Value))
                    {
                        skippedCount++;
                        continue;
                    }

                    // Process this entity's properties
                    if (kvp.Value is TomlTable entityTable)
                    {
                        foreach (var propertyKvp in entityTable)
                        {
                            string propertyName = propertyKvp.Key;
                            object propertyValue = propertyKvp.Value;

                            // Get the appropriate property handler
                            var handler = registry.GetByName(propertyName);

                            if (handler == null)
                            {
                                ConfigHelpers.ErrorLogging.LogUnknownProperty(entityTypeName, entity, propertyName);
                                continue;
                            }

                            // Try to apply the value
                            try
                            {
                                if (!handler.TryLoadFromObject(entity.Value, propertyValue))
                                {
                                    ConfigHelpers.ErrorLogging.LogPropertySkipped(propertyName, entity);
                                }
                            }
                            catch (Exception ex)
                            {
                                ConfigHelpers.ErrorLogging.LogPropertyLoadException(propertyName, entity, ex);
                                errorCount++;
                            }
                        }

                        processedCount++;
                    }
                }

                Plugin.Logger.LogInfo($"Applied {entityTypeName} configs: Processed={processedCount}, Skipped={skippedCount}, Errors={errorCount}");
            }
            catch (Exception ex)
            {
                ConfigHelpers.ErrorLogging.LogConfigLoadException($"{entityTypeName} configs", ex);
            }
        }
    }
}