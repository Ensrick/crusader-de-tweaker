using System;
using System.IO;
using System.Linq;
using System.Text;
using CrusaderDETweaker.Config.ConfigToml;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Units;
using CrusaderDETweaker.Config.Toml.Structures;
using SHCDESE.Interop;

namespace CrusaderDETweaker
{
    /// <summary>
    /// Generates default TOML configuration files using the property handler system.
    /// </summary>
    internal static class ConfigGenerator
    {
        /// <summary>
        /// Generate default unit configuration file.
        /// </summary>
        internal static void GenerateDefaultConfigUnits()
        {
            GenerateDefaultConfig(
                filePath: ConfigPaths.Units,
                registry: UnitPropertyRegistry.Instance,
                allEntities: Enum.GetValues(typeof(eChimps)).Cast<eChimps>().ToArray(),
                nonModifiableEntities: Systems.StatsUnits.NonModableUnits,
                entityTypeName: "unit"
            );
        }

        /// <summary>
        /// Generate default structure configuration file.
        /// </summary>
        internal static void GenerateDefaultConfigStructures()
        {
            GenerateDefaultConfig(
                filePath: ConfigPaths.Structures,
                registry: StructurePropertyRegistry.Instance,
                allEntities: Enum.GetValues(typeof(eStructs)).Cast<eStructs>().ToArray(),
                nonModifiableEntities: Systems.StatsStructures.NonModableStructures,
                entityTypeName: "structure"
            );
        }

        /// <summary>
        /// Generic method to generate default configuration files for any entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type (eChimps or eStructs)</typeparam>
        /// <param name="filePath">Path where the config file should be written</param>
        /// <param name="registry">Property registry for this entity type</param>
        /// <param name="allEntities">All entities of this type to process</param>
        /// <param name="nonModifiableEntities">Entities that should be skipped</param>
        /// <param name="entityTypeName">Name of the entity type for logging (e.g., "unit", "structure")</param>
        private static void GenerateDefaultConfig<TEntity>(
            string filePath,
            PropertyRegistry<TEntity> registry,
            TEntity[] allEntities,
            TEntity[] nonModifiableEntities,
            string entityTypeName)
        {
            if (ConfigFileHelper.ConfigFileExists(filePath)) return;

            try
            {
                var sb = new StringBuilder();
                int processedCount = 0;
                int skippedCount = 0;

                foreach (var entity in allEntities)
                {
                    // Skip non-modifiable entities
                    if (nonModifiableEntities.Contains(entity))
                    {
                        skippedCount++;
                        continue;
                    }

                    sb.AppendLine($"[{entity}]");

                    // Get all applicable property handlers for this entity
                    var handlers = registry.GetApplicable(entity);
                    bool hasAnyProperty = false;

                    foreach (var handler in handlers)
                    {
                        hasAnyProperty |= handler.TryGenerate(entity, sb);
                    }

                    if (!hasAnyProperty)
                        sb.AppendLine("# No modifiable properties");

                    sb.AppendLine();
                    processedCount++;
                }

                ConfigFileHelper.WriteConfigFile(filePath, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default {entityTypeName} config: Processed={processedCount}, Skipped={skippedCount}");
            }
            catch (Exception ex)
            {
                ConfigHelpers.ErrorLogging.LogConfigGenerationException($"{entityTypeName} config", ex);
            }
        }
    }
}