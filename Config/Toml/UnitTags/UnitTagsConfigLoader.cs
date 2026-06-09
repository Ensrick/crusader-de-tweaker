// AI Dev Comment: Loads CrusaderDETweaker_UnitTags.toml and populates UnitTagRegistry with tag interactions.
// Tags themselves are now defined as properties in CrusaderDETweaker_Units.toml (via TagsProperty).
// This loader only handles tag interactions (damage modifiers between tagged units).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Data;
using Tomlyn;
using Tomlyn.Model;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.UnitTags
{
    /// <summary>
    /// Loads unit tags configuration from TOML file.
    /// </summary>
    internal static class UnitTagsConfigLoader
    {
        /// <summary>
        /// Load unit tags and interactions from TOML file.
        /// </summary>
        public static bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Plugin.Logger.LogWarning($"Unit tags config not found: {filePath}");
                    return false;
                }

                Plugin.Logger.LogDebug($"Loading unit tags config: {filePath}");

                // NOTE: Tag registry is NOT cleared here - tags are populated by TagsProperty
                // when loading CrusaderDETweaker_Units.toml. We only clear interactions.
                UnitTagRegistry.ClearInteractions();

                // Parse TOML
                string tomlText = File.ReadAllText(filePath);
                var tomlModel = Tomlyn.Toml.ToModel(tomlText);

                // Load tag definitions
                int tagsLoaded = 0;
                if (tomlModel.ContainsKey("Tags"))
                {
                    TomlTable tagsTable = (TomlTable)tomlModel["Tags"];
                    foreach (var tagEntry in tagsTable)
                    {
                        string tagName = tagEntry.Key;
                        if (tagEntry.Value is TomlArray unitsArray)
                        {
                            foreach (var unitObj in unitsArray)
                            {
                                string unitName = unitObj.ToString();
                                if (Enum.TryParse<eChimps>(unitName, out var unit))
                                {
                                    UnitTagRegistry.RegisterUnitTag(unit, tagName);
                                    tagsLoaded++;
                                }
                            }
                        }
                    }
                }
                Plugin.Logger.LogInfo($"Loaded {tagsLoaded} unit tag mappings from {filePath}");

                // Load interactions
                int interactionsLoaded = 0;
                if (tomlModel.ContainsKey("Interactions"))
                {
                    TomlTable interactionsTable = (TomlTable)tomlModel["Interactions"];
                    foreach (var interactionEntry in interactionsTable)
                    {
                        string interactionId = interactionEntry.Key;
                        if (interactionEntry.Value is TomlTable interactionTable)
                        {
                            try
                            {
                                    float delta = 0.0f;
                                    if (interactionTable.ContainsKey("Delta"))
                                    {
                                        delta = Convert.ToSingle(interactionTable["Delta"]);
                                    }
                                    else if (interactionTable.ContainsKey("Multiplier"))
                                    {
                                        delta = Convert.ToSingle(interactionTable["Multiplier"]) - 1.0f;
                                    }

                                    var interaction = new UnitTagInteraction
                                    {
                                         InteractionId = interactionId,
                                         Delta = delta,
                                         FlatBonus = interactionTable.ContainsKey("FlatBonus")
                                             ? Convert.ToInt32(interactionTable["FlatBonus"])
                                             : 0
                                     };

                                     // Handle AttackerTag (singular) or AttackerTags (list)
                                     if (interactionTable.ContainsKey("AttackerTags") && interactionTable["AttackerTags"] is TomlArray attackerArray)
                                     {
                                         interaction.AttackerTags.AddRange(attackerArray.Select(t => t.ToString()));
                                     }
                                     else if (interactionTable.ContainsKey("AttackerTag"))
                                     {
                                         interaction.AttackerTags.Add(interactionTable["AttackerTag"].ToString());
                                     }

                                     // Handle DefenderTag (singular) or DefenderTags (list)
                                     if (interactionTable.ContainsKey("DefenderTags") && interactionTable["DefenderTags"] is TomlArray defenderArray)
                                     {
                                         interaction.DefenderTags.AddRange(defenderArray.Select(t => t.ToString()));
                                     }
                                     else if (interactionTable.ContainsKey("DefenderTag"))
                                     {
                                         interaction.DefenderTags.Add(interactionTable["DefenderTag"].ToString());
                                     }

                                     UnitTagRegistry.RegisterInteraction(interaction);
                                     interactionsLoaded++;
                                     
                                     string attackerStr = string.Join(",", interaction.AttackerTags);
                                     string defenderStr = string.Join(",", interaction.DefenderTags);
                                     Plugin.Logger.LogDebug($"  Loaded interaction '{interactionId}': {attackerStr} vs {defenderStr} (Δ{interaction.Delta}, +{interaction.FlatBonus})");
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger.LogError($"Failed to parse interaction '{interactionId}': {ex.Message}");
                            }
                        }
                    }
                }

                Plugin.Logger.LogInfo($"Loaded unit tag interactions: {interactionsLoaded} interactions");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load unit tags config: {ex.Message}");
                return false;
            }
        }
    }
}
