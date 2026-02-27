using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrusaderDETweaker.Config.Toml;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Units;
using CrusaderDETweaker.Config.Toml.Structures;
using CrusaderDETweaker.Config.Toml.Projectiles;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml
{
    /// <summary>
    /// Generates default TOML configuration files using the property handler system.
    /// 
    /// This class creates template TOML files that users can edit to customize game behavior.
    /// The generator:
    /// 1. Iterates through all entities (units or structures)
    /// 2. Uses property handlers to read current values from the game API
    /// 3. Writes TOML-formatted configuration entries
    /// 4. Skips non-modifiable entities (special game units/structures)
    /// 
    /// The generated files serve as:
    /// - Documentation of current game values
    /// - Templates for user customization
    /// - Defaults that are loaded if user files don't exist
    /// 
    /// Files are only generated if they don't already exist, preventing overwriting
    /// user customizations. The generator uses the same property handler system
    /// as the loader, ensuring consistency between generation and loading.
    /// </summary>
    internal static class ConfigGenerator
    {
        /// <summary>
        /// Generate default unit configuration file.
        /// Includes projectiles at the bottom of the file.
        /// </summary>
        internal static void GenerateDefaultConfigUnits()
        {
            if (ConfigFileHelper.ConfigFileExists(ConfigPaths.Units)) return;

            try
            {
                var sb = new StringBuilder();
                
                // Add file header
                ConfigFileHeaderWriter.WriteHeader(sb, "unit");
                
                int processedCount = 0;
                int skippedCount = 0;

                var nonModifiableSet = new HashSet<eChimps>(UnitCategories.NonModable);
                var allUnits = Enum.GetValues(typeof(eChimps)).Cast<eChimps>().ToArray();

                // Generate unit sections
                foreach (var unit in allUnits)
                {
                    if (nonModifiableSet.Contains(unit))
                    {
                        skippedCount++;
                        continue;
                    }

                    sb.AppendLine($"[{unit}]");

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

                // Projectile configuration is DEACTIVATED in TOML.
                // Damage is now exclusively handled via CSV matrices.
                int projectileCount = 0;

                ConfigFileHelper.WriteConfigFile(ConfigPaths.Units, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default unit config: Units={processedCount}, Skipped={skippedCount}, Projectiles={projectileCount}");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigGenerationException("unit config", ex);
            }
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
                nonModifiableEntities: Data.StructureCategories.NonModable,
                entityTypeName: "structure"
            );
        }

        /// <summary>
        /// Generate default global configuration file.
        /// </summary>
        internal static void GenerateDefaultConfigGlobals()
        {
            if (ConfigFileHelper.ConfigFileExists(ConfigPaths.Globals)) return;

            try
            {
                var sb = new StringBuilder();

                var restockAmount = Plugin.GlobalsApi?.CatapultRestockStoneAmount?.GetValue() ?? 20;
                var restockCost = Plugin.GlobalsApi?.CatapultRestockStoneCost?.GetValue() ?? 10;
                var stealthDetection = Plugin.GlobalsApi?.AssassinDetectionRange?.GetValue() ?? 160;
                var stealthTransparency = Plugin.GlobalsApi?.AssassinTransparencyThreshold?.GetValue() ?? 120;
                var stablesRegenTick = Plugin.GlobalsApi?.StablesHorseRegenTickTarget?.GetValue() ?? 550;
                var stablesHorsesCap = Plugin.GlobalsApi?.StablesHorsesCap?.GetValue() ?? 4;
                var gateClose = Plugin.GlobalsApi?.GateHouseCloseDistance?.GetValue() ?? 200;
                var gateReOpen = Plugin.GlobalsApi?.GateHouseReOpenDistance?.GetValue() ?? 1200;
                var diseaseDamage1 = Plugin.GlobalsApi?.DiseaseDamage1?.GetValue() ?? 150;
                var diseaseDamage2 = Plugin.GlobalsApi?.DiseaseDamage2?.GetValue() ?? 200;
                var diseaseDamage3 = Plugin.GlobalsApi?.DiseaseDamage3?.GetValue() ?? 400;

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Globals Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine();

                sb.AppendLine("[\"Siege Engines\"]");
                sb.AppendLine($"SiegeEngineRestockStoneAmount = {restockAmount}  # default: {restockAmount}");
                sb.AppendLine($"SiegeEngineRestockStoneCost = {restockCost}  # default: {restockCost}");
                sb.AppendLine();

                sb.AppendLine("[Stealth]");
                sb.AppendLine($"StealthDetectionRange = {stealthDetection}  # default: {stealthDetection}");
                sb.AppendLine($"StealthTransparencyThreshold = {stealthTransparency}  # default: {stealthTransparency}");
                sb.AppendLine();

                sb.AppendLine("[Stables]");
                sb.AppendLine($"StablesHorseRegenTickTarget = {stablesRegenTick}  # default: {stablesRegenTick} — ticks before a horse charge regenerates");
                sb.AppendLine($"StablesHorsesCap = {stablesHorsesCap}  # default: {stablesHorsesCap} — WARNING: values above 4 break stable horse-link tracking");
                sb.AppendLine();

                sb.AppendLine("[Gatehouse]");
                sb.AppendLine($"GateHouseCloseDistance = {gateClose}  # default: {gateClose}");
                sb.AppendLine($"GateHouseReOpenDistance = {gateReOpen}  # default: {gateReOpen}");
                sb.AppendLine();

                sb.AppendLine("[Disease]");
                sb.AppendLine($"DiseaseDamage1 = {diseaseDamage1}  # default: {diseaseDamage1} — damage tier 1 (from c_game_unit_takedamage_projectile)");
                sb.AppendLine($"DiseaseDamage2 = {diseaseDamage2}  # default: {diseaseDamage2} — damage tier 2");
                sb.AppendLine($"DiseaseDamage3 = {diseaseDamage3}  # default: {diseaseDamage3} — damage tier 3");
                sb.AppendLine();

                sb.AppendLine("[\"Peasant Spawning\"]");
                sb.AppendLine("# PeasantRespawnTickResetValue = 0  # ManagedAssemblyMultiImmediate<ushort> - not yet implemented");
                sb.AppendLine("# PeasantRespawnTickTargetValue = 0  # ManagedAssemblyMultiImmediate<ushort> - not yet implemented");
                sb.AppendLine("# PeasantSpawnRateIncrementsDefaultsRVA - RVA-based, not yet implemented");
                sb.AppendLine("# PeasantSpawnRateIncrementsHighPopRVA - RVA-based, not yet implemented");
                sb.AppendLine("# PeasantSpawnRateIncrementsLowPopRVA - RVA-based, not yet implemented");
                sb.AppendLine();

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Player Options Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine();

                sb.AppendLine("[\"Gameplay Options\"]");
                sb.AppendLine("BetterHealers = false");
                sb.AppendLine("FasterPeasants = false");
                sb.AppendLine("ImprovedArabSwordsman = false");
                sb.AppendLine("ImprovedFletchers = false");
                sb.AppendLine("ImprovedLadderman = false");
                sb.AppendLine("ImprovedSpearman = false");
                sb.AppendLine("NerfEunuchs = false");
                sb.AppendLine("NoKnockdownWalls = false");
                sb.AppendLine("RebalancedHorseArchers = false");
                sb.AppendLine("UncappedPeasants = false");
                sb.AppendLine("# Override map restrictions — set true to unlock everything regardless of map settings");
                sb.AppendLine("AllBuildingsAvailable = false");
                sb.AppendLine("AllUnitsAllowed = false");
                sb.AppendLine("AllTradeGoodsAllowed = false");
                sb.AppendLine("AllProductionGoodsAllowed = false");
                sb.AppendLine();

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Trade Prices Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine("# Base prices for goods at the market.");
                sb.AppendLine("# Values are the game defaults read at startup. Edit to override.");
                sb.AppendLine();

                // Known default prices captured from API at session start (OnStartMap).
                // Goods not in this table will show 0 (unknown default).
                var tradePriceDefaults = new System.Collections.Generic.Dictionary<string, long>
                {
                    { "STORED_WOOD_PLANKS",    20  },
                    { "STORED_STONE_BLOCKS",   70  },
                    { "STORED_IRON_INGOTS",    225 },
                    { "STORED_RAW_HOPS",       75  },
                    { "STORED_PITCH_REFINED",  100 },
                    { "STORED_RAW_WHEAT",      115 },
                    { "STORED_FLOUR",          160 },
                    { "STORED_FOOD_BREAD",     40  },
                    { "STORED_FOOD_CHEESE",    40  },
                    { "STORED_FOOD_MEAT",      40  },
                    { "STORED_FOOD_FRUIT",     40  },
                    { "STORED_FOOD_ALE",       100 },
                    { "STORED_SWORDS",         290 },
                    { "STORED_BOWS",           155 },
                    { "STORED_CROSSBOWS",      290 },
                    { "STORED_SPEARS",         100 },
                    { "STORED_PIKES",          180 },
                    { "STORED_MACES",          290 },
                    { "STORED_LEATHER_ARMOUR", 125 },
                    { "STORED_METAL_ARMOUR",   290 },
                };

                // Goods that exist in the enum but are not player-tradeable at the market.
                // Gold is map/difficulty-dependent; raw materials are intermediate goods.
                var nonTradeableGoods = new System.Collections.Generic.HashSet<string>
                {
                    "STORED_NULL",        // null/empty good sentinel — not a real good
                    "STORED_WOOD_LOGS",   // raw wood — not sold at market
                    "STORED_COW_HIDES",   // raw hides — not sold at market
                    "STORED_PITCH_RAW",   // unrefined pitch — not sold at market
                    "STORED_GOLD",        // starting gold — map/difficulty setting, not a trade price
                };

                sb.AppendLine("# Override the game's base buy/sell price for each good.");
                sb.AppendLine("# Set to 0 to leave the game's built-in default unchanged.");
                sb.AppendLine("[\"Trade Prices\"]");
                foreach (eGoods good in Enum.GetValues(typeof(eGoods)))
                {
                    string goodName = good.ToString();
                    if (!goodName.StartsWith("STORED_")) continue; // skip STORED_NULL, _SE_* specials, Count sentinel
                    if (nonTradeableGoods.Contains(goodName)) continue;
                    long def = tradePriceDefaults.TryGetValue(goodName, out var d) ? d : 0;
                    sb.AppendLine($"{goodName} = {def}  # default: {def}");
                }
                sb.AppendLine();

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Auto Trade Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine("# Applied at match start via the market's auto-trade feature.");
                sb.AppendLine("# BuyLevel:  auto-buy when stock falls BELOW this amount (0 = no buying)");
                sb.AppendLine("# SellLevel: auto-sell when stock rises ABOVE this amount (0 = no selling)");
                sb.AppendLine();

                foreach (eGoods good in Enum.GetValues(typeof(eGoods)))
                {
                    string goodName = good.ToString();
                    if (!goodName.StartsWith("STORED_")) continue; // skip STORED_NULL, _SE_* specials, Count sentinel
                    if (nonTradeableGoods.Contains(goodName)) continue;
                    sb.AppendLine($"[\"Auto Trade\".{goodName}]");
                    sb.AppendLine("Enabled = false");
                    sb.AppendLine("BuyLevel = 0");
                    sb.AppendLine("SellLevel = 0");
                    sb.AppendLine();
                }

                ConfigFileHelper.WriteConfigFile(ConfigPaths.Globals, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default global config");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigGenerationException("global config", ex);
            }
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
                
                // Add file header with explanation
                ConfigFileHeaderWriter.WriteHeader(sb, entityTypeName);
                
                int processedCount = 0;
                int skippedCount = 0;

                // Convert array to HashSet for O(1) lookup performance
                var nonModifiableSet = new HashSet<TEntity>(nonModifiableEntities);

                foreach (var entity in allEntities)
                {
                    // Skip non-modifiable entities (O(1) lookup)
                    if (nonModifiableSet.Contains(entity))
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
                Core.ErrorLogging.LogConfigGenerationException($"{entityTypeName} config", ex);
            }
        }

    }
}