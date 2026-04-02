// Config/Toml/ConfigGenerator.cs
//
// PURPOSE: Generates default TOML config files on first run.
//
// WARNING: GenerateDefault* methods run during LibraryLoaded init — NO game session exists.
// If a config file is missing, GenerateDefaultConfig calls TryGetFromAPI for every entity,
// which writes/reads native game memory and causes a native ACCESS_VIOLATION crash.
// Fix: Each Generate method returns early if the file already exists.
// If you add a new GenerateDefault* method, you MUST add the same early-return guard:
//   if (ConfigFileHelper.ConfigFileExists(ConfigPaths.YourPath)) return;
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tomlyn.Model;
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
        /// Only generates if the file does not already exist — avoids calling game API during init.
        /// </summary>
        internal static void GenerateDefaultConfigUnits()
        {
            if (ConfigFileHelper.ConfigFileExists(ConfigPaths.Units)) return;

            try
            {
                var sb = new StringBuilder();
                ConfigFileHeaderWriter.WriteHeader(sb, "unit");

                int processedCount = 0;
                int skippedCount = 0;

                var nonModifiableSet = new HashSet<eChimps>(UnitCategories.NonModable);
                var allUnits = Enum.GetValues(typeof(eChimps)).Cast<eChimps>().ToArray();

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
                Plugin.Logger.LogInfo($"Generated unit config: Units={processedCount}, Skipped={skippedCount}, Projectiles={projectileCount}");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigGenerationException("unit config", ex);
            }
        }

        /// <summary>
        /// Generate default structure configuration file.
        /// Only generates if the file does not already exist — avoids calling game API during init.
        /// </summary>
        internal static void GenerateDefaultConfigStructures()
        {
            if (ConfigFileHelper.ConfigFileExists(ConfigPaths.Structures)) return;

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
        /// If the file already exists, migrates it: preserves user values, adds new entries, removes deprecated ones.
        /// </summary>
        internal static void GenerateDefaultConfigGlobals()
        {
            // Load existing values for migration if file already exists
            TomlTable existingToml = null;
            if (ConfigFileHelper.ConfigFileExists(ConfigPaths.Globals))
            {
                try
                {
                    existingToml = Tomlyn.Toml.ToModel(ConfigFileHelper.ReadConfigFile(ConfigPaths.Globals));
                    Plugin.Logger.LogInfo($"Migrating existing global config: {ConfigPaths.Globals}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Could not parse existing global config for migration, leaving unchanged: {ex.Message}");
                    return;
                }
            }

            try
            {
                var sb = new StringBuilder();

                // True game defaults — hardcoded because the API already reflects user settings by the
                // time the generator runs (config was already loaded before generation). Never read these
                // from GetValue() here, as that would show the user's value in the # default comment.
                const int DefaultRestockStoneAmount = 20;
                const int DefaultRestockStoneCost = 10;
                const int DefaultInitialStoneAmount = 20;
                const int DefaultStealthDetectionRange = 160;
                const int DefaultStealthTransparencyThreshold = 120;
                const int DefaultStablesHorseRegenTickTarget = 550;
                const int DefaultStablesHorsesCap = 4;
                const int DefaultGateHouseCloseDistance = 200;
                const int DefaultGateHouseReOpenDistance = 1200;
                const int DefaultDiseaseDamage1 = 150;
                const int DefaultDiseaseDamage2 = 200;
                const int DefaultDiseaseDamage3 = 400;
                const int DefaultPathfindingMaxTilesConstraint = 2000;
                const int DefaultFoodConsumptionTickThreshold = 15000;
                const int DefaultFoodConsumptionRate = 3;
                const int DefaultPeasantRespawnTickTargetValue = 4000;
                const int DefaultPeasantRespawnTickResetValue = 2000;

                var restockAmount = ExistingOrDefault(existingToml, "Siege Engines", "SiegeEngineRestockStoneAmount", DefaultRestockStoneAmount);
                var restockCost = ExistingOrDefault(existingToml, "Siege Engines", "SiegeEngineRestockStoneCost", DefaultRestockStoneCost);
                var initialStone = ExistingOrDefault(existingToml, "Siege Engines", "SiegeEngineInitialStoneAmount", DefaultInitialStoneAmount);
                var pathfindingMaxTiles = ExistingOrDefault(existingToml, "Pathfinding", "PathfindingMaxTilesConstraint", DefaultPathfindingMaxTilesConstraint);
                var fccThreshold = ExistingOrDefault(existingToml, "Food Consumption", "FoodConsumptionTickThreshold", DefaultFoodConsumptionTickThreshold);
                var fccRate = ExistingOrDefault(existingToml, "Food Consumption", "FoodConsumptionRate", DefaultFoodConsumptionRate);
                var stealthDetection = ExistingOrDefault(existingToml, "Stealth", "StealthDetectionRange", DefaultStealthDetectionRange);
                var stealthTransparency = ExistingOrDefault(existingToml, "Stealth", "StealthTransparencyThreshold", DefaultStealthTransparencyThreshold);
                var stablesRegenTick = ExistingOrDefault(existingToml, "Stables", "StablesHorseRegenTickTarget", DefaultStablesHorseRegenTickTarget);
                var stablesHorsesCap = ExistingOrDefault(existingToml, "Stables", "StablesHorsesCap", DefaultStablesHorsesCap);
                var gateClose = ExistingOrDefault(existingToml, "Gatehouse", "GateHouseCloseDistance", DefaultGateHouseCloseDistance);
                var gateReOpen = ExistingOrDefault(existingToml, "Gatehouse", "GateHouseReOpenDistance", DefaultGateHouseReOpenDistance);
                var diseaseDamage1 = ExistingOrDefault(existingToml, "Disease", "DiseaseDamage1", DefaultDiseaseDamage1);
                var diseaseDamage2 = ExistingOrDefault(existingToml, "Disease", "DiseaseDamage2", DefaultDiseaseDamage2);
                var diseaseDamage3 = ExistingOrDefault(existingToml, "Disease", "DiseaseDamage3", DefaultDiseaseDamage3);

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Globals Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine();

                sb.AppendLine("[\"Siege Engines\"]");
                sb.AppendLine($"SiegeEngineRestockStoneAmount = {restockAmount}  # default: {DefaultRestockStoneAmount}");
                sb.AppendLine($"SiegeEngineRestockStoneCost = {restockCost}  # default: {DefaultRestockStoneCost}");
                sb.AppendLine($"SiegeEngineInitialStoneAmount = {initialStone}  # default: {DefaultInitialStoneAmount}");
                sb.AppendLine();

                sb.AppendLine("[Stealth]");
                sb.AppendLine($"StealthDetectionRange = {stealthDetection}  # default: {DefaultStealthDetectionRange}");
                sb.AppendLine($"StealthTransparencyThreshold = {stealthTransparency}  # default: {DefaultStealthTransparencyThreshold}");
                sb.AppendLine();

                sb.AppendLine("[Stables]");
                sb.AppendLine($"StablesHorseRegenTickTarget = {stablesRegenTick}  # default: {DefaultStablesHorseRegenTickTarget} — ticks before a horse charge regenerates");
                sb.AppendLine($"StablesHorsesCap = {stablesHorsesCap}  # default: {DefaultStablesHorsesCap} — WARNING: values above 4 break stable horse-link tracking");
                sb.AppendLine();

                sb.AppendLine("[Gatehouse]");
                sb.AppendLine($"GateHouseCloseDistance = {gateClose}  # default: {DefaultGateHouseCloseDistance}");
                sb.AppendLine($"GateHouseReOpenDistance = {gateReOpen}  # default: {DefaultGateHouseReOpenDistance}");
                sb.AppendLine();

                sb.AppendLine("[Disease]");
                sb.AppendLine($"DiseaseDamage1 = {diseaseDamage1}  # default: {DefaultDiseaseDamage1} — damage tier 1 (from c_game_unit_takedamage_projectile)");
                sb.AppendLine($"DiseaseDamage2 = {diseaseDamage2}  # default: {DefaultDiseaseDamage2} — damage tier 2");
                sb.AppendLine($"DiseaseDamage3 = {diseaseDamage3}  # default: {DefaultDiseaseDamage3} — damage tier 3");
                sb.AppendLine();

                sb.AppendLine("[Pathfinding]");
                sb.AppendLine($"PathfindingMaxTilesConstraint = {pathfindingMaxTiles}  # default: {DefaultPathfindingMaxTilesConstraint}");
                sb.AppendLine();

                sb.AppendLine("[\"Food Consumption\"]");
                sb.AppendLine("# Modifies how often the food consumption evaluation ticks. Higher values delay the ticks (slower eating).");
                sb.AppendLine($"FoodConsumptionTickThreshold = {fccThreshold}  # default: {DefaultFoodConsumptionTickThreshold}");
                sb.AppendLine("# Base rate (%) multiplier for food consumption during a tick calculation.");
                sb.AppendLine($"FoodConsumptionRate = {fccRate}  # default: {DefaultFoodConsumptionRate}");
                sb.AppendLine();

                var peasantTickTarget = ExistingOrDefault(existingToml, "Peasant Spawning", "PeasantRespawnTickTargetValue", DefaultPeasantRespawnTickTargetValue);
                var peasantTickReset = ExistingOrDefault(existingToml, "Peasant Spawning", "PeasantRespawnTickResetValue", DefaultPeasantRespawnTickResetValue);

                sb.AppendLine("[\"Peasant Spawning\"]");
                sb.AppendLine($"# How many ticks must elapse before a new peasant spawns. Lower = faster spawning.");
                sb.AppendLine($"PeasantRespawnTickTargetValue = {peasantTickTarget}  # default: {DefaultPeasantRespawnTickTargetValue}");
                sb.AppendLine($"# Tick counter reset value after a spawn. Lower = fewer ticks wasted on reset.");
                sb.AppendLine($"PeasantRespawnTickResetValue = {peasantTickReset}  # default: {DefaultPeasantRespawnTickResetValue}");
                sb.AppendLine();

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Player Options Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine();

                sb.AppendLine("[\"Gameplay Options\"]");
                sb.AppendLine($"BetterHealers = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "BetterHealers", false))}");
                sb.AppendLine($"FasterPeasants = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "FasterPeasants", false))}");
                sb.AppendLine($"ImprovedArabSwordsman = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "ImprovedArabSwordsman", false))}");
                sb.AppendLine($"ImprovedFletchers = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "ImprovedFletchers", false))}");
                sb.AppendLine($"ImprovedLadderman = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "ImprovedLadderman", false))}");
                sb.AppendLine($"ImprovedSpearman = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "ImprovedSpearman", false))}");
                sb.AppendLine($"NerfEunuchs = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "NerfEunuchs", false))}");
                sb.AppendLine($"NoKnockdownWalls = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "NoKnockdownWalls", false))}");
                sb.AppendLine($"RebalancedHorseArchers = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "RebalancedHorseArchers", false))}");
                sb.AppendLine($"UncappedPeasants = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "UncappedPeasants", false))}");
                sb.AppendLine("# Override map restrictions — set true to unlock everything regardless of map settings");
                sb.AppendLine($"AllBuildingsAvailable = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "AllBuildingsAvailable", false))}");
                sb.AppendLine($"AllUnitsAllowed = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "AllUnitsAllowed", false))}");
                sb.AppendLine($"AllTradeGoodsAllowed = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "AllTradeGoodsAllowed", false))}");
                sb.AppendLine($"AllProductionGoodsAllowed = {FormatBool(ExistingOrDefault(existingToml, "Gameplay Options", "AllProductionGoodsAllowed", false))}");
                sb.AppendLine();

                var nonTradeableGoods = new System.Collections.Generic.HashSet<string>
                {
                    "STORED_NULL",        // null/empty good sentinel — not a real good
                    "STORED_WOOD_LOGS",   // raw wood — not sold at market
                    "STORED_COW_HIDES",   // raw hides — not sold at market
                    "STORED_PITCH_RAW",   // unrefined pitch — not sold at market
                    "STORED_GOLD",        // starting gold — map/difficulty setting, not a trade price
                };

                sb.AppendLine("# ========================================");
                sb.AppendLine("# Crusader DE Tweaker - Trade Prices Configuration");
                sb.AppendLine("# ========================================");
                sb.AppendLine("# BuyPrice:  gold paid per unit when buying at the market. 0 = use game default (no override).");
                sb.AppendLine("# SellPrice: gold received per unit when selling at the market. 0 = use game default (no override).");
                sb.AppendLine("# Requires a Trade Post. Re-applied on each map load.");
                sb.AppendLine();

                // Hardcoded defaults captured from live game session via LogTradePriceDefaults().
                var tradePriceDefaults = new System.Collections.Generic.Dictionary<string, (int Buy, int Sell)>
                {
                    { "STORED_WOOD_PLANKS",    (Buy:  20, Sell:   5) },
                    { "STORED_RAW_HOPS",       (Buy:  75, Sell:  40) },
                    { "STORED_STONE_BLOCKS",   (Buy:  70, Sell:  35) },
                    { "STORED_IRON_INGOTS",    (Buy: 225, Sell: 115) },
                    { "STORED_PITCH_REFINED",  (Buy: 100, Sell:  50) },
                    { "STORED_RAW_WHEAT",      (Buy: 115, Sell:  40) },
                    { "STORED_FOOD_BREAD",     (Buy:  40, Sell:  20) },
                    { "STORED_FOOD_CHEESE",    (Buy:  40, Sell:  20) },
                    { "STORED_FOOD_MEAT",      (Buy:  40, Sell:  20) },
                    { "STORED_FOOD_FRUIT",     (Buy:  40, Sell:  20) },
                    { "STORED_FOOD_ALE",       (Buy: 100, Sell:  50) },
                    { "STORED_FLOUR",          (Buy: 160, Sell:  50) },
                    { "STORED_BOWS",           (Buy: 155, Sell:  75) },
                    { "STORED_CROSSBOWS",      (Buy: 290, Sell: 150) },
                    { "STORED_SPEARS",         (Buy: 100, Sell:  50) },
                    { "STORED_PIKES",          (Buy: 180, Sell:  90) },
                    { "STORED_MACES",          (Buy: 290, Sell: 150) },
                    { "STORED_SWORDS",         (Buy: 290, Sell: 150) },
                    { "STORED_LEATHER_ARMOUR", (Buy: 125, Sell:  60) },
                    { "STORED_METAL_ARMOUR",   (Buy: 290, Sell: 150) },
                };

                foreach (eGoods good in Enum.GetValues(typeof(eGoods)))
                {
                    string goodName = good.ToString();
                    if (!goodName.StartsWith("STORED_")) continue;
                    if (nonTradeableGoods.Contains(goodName)) continue;

                    tradePriceDefaults.TryGetValue(goodName, out var defaults);

                    var buyPrice = ExistingOrDefaultNested(existingToml, "Trade Prices", goodName, "BuyPrice", 0);
                    var sellPrice = ExistingOrDefaultNested(existingToml, "Trade Prices", goodName, "SellPrice", 0);

                    sb.AppendLine($"[\"Trade Prices\".{goodName}]");
                    sb.AppendLine($"BuyPrice = {buyPrice}  # default: {defaults.Buy}");
                    sb.AppendLine($"SellPrice = {sellPrice}  # default: {defaults.Sell}");
                    sb.AppendLine();
                }

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
                    if (!goodName.StartsWith("STORED_")) continue;
                    if (nonTradeableGoods.Contains(goodName)) continue;

                    var atEnabled = ExistingOrDefaultNested(existingToml, "Auto Trade", goodName, "Enabled", false);
                    var atBuyLevel = ExistingOrDefaultNested(existingToml, "Auto Trade", goodName, "BuyLevel", 0);
                    var atSellLevel = ExistingOrDefaultNested(existingToml, "Auto Trade", goodName, "SellLevel", 0);

                    sb.AppendLine($"[\"Auto Trade\".{goodName}]");
                    sb.AppendLine($"Enabled = {FormatBool(atEnabled)}");
                    sb.AppendLine($"BuyLevel = {atBuyLevel}");
                    sb.AppendLine($"SellLevel = {atSellLevel}");
                    sb.AppendLine();
                }

                ConfigFileHelper.WriteConfigFile(ConfigPaths.Globals, sb.ToString());
                string globalsAction = existingToml != null ? "Migrated" : "Generated";
                Plugin.Logger.LogInfo($"{globalsAction} global config");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigGenerationException("global config", ex);
            }
        }

        /// <summary>
        /// Generic method to generate default configuration files for any entity type.
        /// If the file already exists, migrates it: preserves user values, adds new entries, removes deprecated ones.
        /// </summary>
        private static void GenerateDefaultConfig<TEntity>(
            string filePath,
            PropertyRegistry<TEntity> registry,
            TEntity[] allEntities,
            TEntity[] nonModifiableEntities,
            string entityTypeName)
        {
            // Load existing values for migration if file already exists
            TomlTable existingToml = null;
            if (ConfigFileHelper.ConfigFileExists(filePath))
            {
                try
                {
                    existingToml = Tomlyn.Toml.ToModel(ConfigFileHelper.ReadConfigFile(filePath));
                    Plugin.Logger.LogInfo($"Migrating existing {entityTypeName} config: {filePath}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Could not parse existing {entityTypeName} config for migration, leaving unchanged: {ex.Message}");
                    return;
                }
            }

            try
            {
                var sb = new StringBuilder();
                ConfigFileHeaderWriter.WriteHeader(sb, entityTypeName);

                int processedCount = 0;
                int skippedCount = 0;

                var nonModifiableSet = new HashSet<TEntity>(nonModifiableEntities);

                foreach (var entity in allEntities)
                {
                    if (nonModifiableSet.Contains(entity))
                    {
                        skippedCount++;
                        continue;
                    }

                    sb.AppendLine($"[{entity}]");

                    var handlers = registry.GetApplicable(entity);
                    bool hasAnyProperty = false;

                    IDictionary<string, object> existingSection = null;
                    if (existingToml != null && existingToml.TryGetValue(entity.ToString(), out var rawSection))
                        existingSection = rawSection as IDictionary<string, object>;

                    foreach (var handler in handlers)
                    {
                        hasAnyProperty |= existingSection != null
                            ? handler.TryGenerateWithOverride(entity, sb, existingSection)
                            : handler.TryGenerate(entity, sb);
                    }

                    if (!hasAnyProperty)
                        sb.AppendLine("# No modifiable properties");

                    sb.AppendLine();
                    processedCount++;
                }

                ConfigFileHelper.WriteConfigFile(filePath, sb.ToString());
                string action = existingToml != null ? "Migrated" : "Generated";
                Plugin.Logger.LogInfo($"{action} {entityTypeName} config: Processed={processedCount}, Skipped={skippedCount}");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigGenerationException($"{entityTypeName} config", ex);
            }
        }

        /// <summary>
        /// Formats a bool as TOML-required lowercase "true"/"false".
        /// </summary>
        private static string FormatBool(bool value) => value ? "true" : "false";

        /// <summary>
        /// Returns the user's existing value from a parsed globals TOML if present,
        /// otherwise returns the provided game default.
        /// </summary>
        private static T ExistingOrDefault<T>(TomlTable existing, string section, string key, T gameDefault)
        {
            if (existing == null) return gameDefault;
            if (!existing.TryGetValue(section, out var sectionObj) || !(sectionObj is TomlTable sectionTable))
                return gameDefault;
            if (!sectionTable.TryGetValue(key, out var raw))
                return gameDefault;
            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return gameDefault; }
        }

        /// <summary>
        /// Returns the user's existing value from a two-level nested section (e.g. ["Trade Prices".STORED_X])
        /// if present, otherwise returns the provided game default.
        /// </summary>
        private static T ExistingOrDefaultNested<T>(TomlTable existing, string section, string subsection, string key, T gameDefault)
        {
            if (existing == null) return gameDefault;
            if (!existing.TryGetValue(section, out var sectionObj) || !(sectionObj is TomlTable sectionTable))
                return gameDefault;
            if (!sectionTable.TryGetValue(subsection, out var subObj) || !(subObj is TomlTable subTable))
                return gameDefault;
            if (!subTable.TryGetValue(key, out var raw))
                return gameDefault;
            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return gameDefault; }
        }
    }
}