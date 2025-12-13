using System;
using System.IO;
using System.Text;
using CrusaderDETweaker.Config.ConfigToml;
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
            if (File.Exists(ConfigPaths.Units)) return;

            try
            {
                var sb = new StringBuilder();
                int processedCount = 0;
                int skippedCount = 0;

                foreach (eChimps unit in Enum.GetValues(typeof(eChimps)))
                {
                    // Skip completely non-modifiable units
                    if (Systems.StatsUnits.NonModableUnits.Contains(unit))
                    {
                        skippedCount++;
                        continue;
                    }

                    sb.AppendLine($"[{unit}]");

                    // Get all applicable property handlers for this unit
                    var handlers = UnitPropertyRegistry.Instance.GetApplicable(unit);
                    bool hasAnyProperty = false;

                    foreach (var handler in handlers)
                    {
                        hasAnyProperty |= handler.TryGenerate(unit, sb);
                    }

                    if (!hasAnyProperty)
                        sb.AppendLine("# No modifiable properties");

                    sb.AppendLine();
                    processedCount++;
                }

                File.WriteAllText(ConfigPaths.Units, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default unit config: Processed={processedCount}, Skipped={skippedCount}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to generate default unit config: {ex}");
            }
        }

        /// <summary>
        /// Generate default structure configuration file.
        /// </summary>
        internal static void GenerateDefaultConfigStructures()
        {
            if (File.Exists(ConfigPaths.Structures)) return;

            try
            {
                var sb = new StringBuilder();
                int processedCount = 0;
                int skippedCount = 0;

                foreach (eStructs structure in Enum.GetValues(typeof(eStructs)))
                {
                    if (Systems.StatsStructures.NonModableStructures.Contains(structure))
                    {
                        skippedCount++;
                        continue;
                    }

                    sb.AppendLine($"[{structure}]");

                    // Get all applicable property handlers for this structure
                    var handlers = StructurePropertyRegistry.Instance.GetApplicable(structure);
                    bool hasAnyProperty = false;

                    foreach (var handler in handlers)
                    {
                        hasAnyProperty |= handler.TryGenerate(structure, sb);
                    }

                    if (!hasAnyProperty)
                        sb.AppendLine("# No modifiable properties");

                    sb.AppendLine();
                    processedCount++;
                }

                File.WriteAllText(ConfigPaths.Structures, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default structure config: Processed={processedCount}, Skipped={skippedCount}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to generate default structure config: {ex}");
            }
        }
    }
}