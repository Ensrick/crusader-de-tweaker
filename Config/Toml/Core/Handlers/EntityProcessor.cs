// Config/Toml/Core/EntityProcessor.cs
//
// PURPOSE: Generic loop for processing entities and their properties from TOML files.
//
// USAGE:
// - ProcessEntities(): Called by ConfigLoader to process all entities in a TOML file
// - Iterates through TOML sections, parses entity names, skips non-modifiable entities
// - Processes each entity's properties using PropertyRegistry
//
// IMPORTANT FOR AI AGENTS:
// - This is a generic helper extracted from ConfigLoader to reduce method complexity
// - Works for both units (eChimps) and structures (eStructs)
// - Skips non-modifiable entities automatically
// - Returns counts of processed/skipped/errors for logging
// - All property loading logic is delegated to PropertyRegistry
//
using System;
using System.Collections.Generic;
using System.Linq;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Processes entities from TOML configuration files.
    /// 
    /// Extracted from ConfigLoader to reduce method size and improve readability.
    /// 
    /// Provides a generic loop that works for both units and structures:
    /// 1. Parse entity name from TOML section
    /// 2. Skip non-modifiable entities
    /// 3. Process entity properties using PropertyRegistry
    /// 4. Track counts for logging
    /// </summary>
    internal static class EntityProcessor
    {
        /// <summary>
        /// Process all entities from a TOML model, applying their properties.
        /// </summary>
        public static (int processed, int skipped, int errors) ProcessEntities<TEntity>(
            TomlTable tomlModel,
            PropertyRegistry<TEntity> registry,
            TEntity[] nonModifiableEntities,
            string entityTypeName,
            Func<string, TEntity?> parseEntity)
            where TEntity : struct
        {
            int processedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            // Convert array to HashSet for O(1) lookup performance
            var nonModifiableSet = new HashSet<TEntity>(nonModifiableEntities);

            foreach (var kvp in tomlModel)
            {
                // Parse entity name
                var entity = parseEntity(kvp.Key);
                if (entity == null)
                {
                    ErrorLogging.LogUnknownEntity(entityTypeName, kvp.Key);
                    continue;
                }

                // Skip non-modifiable entities (O(1) lookup)
                if (nonModifiableSet.Contains(entity.Value))
                {
                    skippedCount++;
                    continue;
                }

                // Process this entity's properties
                if (kvp.Value is TomlTable entityTable)
                {
                    errorCount += ProcessEntityProperties(entity.Value, entityTable, registry, entityTypeName);
                    processedCount++;
                }
            }

            return (processedCount, skippedCount, errorCount);
        }

        private static int ProcessEntityProperties<TEntity>(
            TEntity entity,
            TomlTable entityTable,
            PropertyRegistry<TEntity> registry,
            string entityTypeName)
            where TEntity : struct
        {
            int errorCount = 0;

            foreach (var propertyKvp in entityTable)
            {
                string propertyName = propertyKvp.Key;
                object propertyValue = propertyKvp.Value;

                // Get the appropriate property handler
                var handler = registry.GetByName(propertyName);

                if (handler == null)
                {
                    ErrorLogging.LogUnknownProperty(entityTypeName, entity, propertyName);
                    continue;
                }

                // Try to apply the value
                try
                {
                    if (!handler.TryLoadFromObject(entity, propertyValue))
                    {
                        ErrorLogging.LogPropertySkipped(propertyName, entity);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogPropertyLoadException(propertyName, entity, ex);
                    errorCount++;
                }
            }

            return errorCount;
        }
    }
}

