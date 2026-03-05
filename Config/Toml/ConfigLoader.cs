using System;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.Toml;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Projectiles;
using CrusaderDETweaker.Config.Toml.Units;
using CrusaderDETweaker.Config.Toml.Structures;
using CrusaderDETweaker.Data;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using Tomlyn;
using Tomlyn.Model;
using System.Collections.Generic;

namespace CrusaderDETweaker.Config.Toml
{
    /// <summary>
    /// Loads and applies TOML configuration files using the property handler system.
    /// 
    /// This class is responsible for:
    /// 1. Parsing TOML files into structured data
    /// 2. Matching entity names to game enums (eChimps, eStructs)
    /// 3. Applying property values using registered property handlers
    /// 4. Handling errors gracefully (invalid entities, missing properties, etc.)
    /// 
    /// The loader uses a generic ApplyConfigs method that works for both units and structures,
    /// reducing code duplication. Property handlers are obtained from the appropriate registry
    /// (UnitPropertyRegistry or StructurePropertyRegistry).
    /// 
    /// Error handling:
    /// - Invalid entity names are logged and skipped
    /// - Non-modifiable entities are skipped (e.g., special game units)
    /// - Property load errors are logged but don't stop the loading process
    /// - Missing config files are handled gracefully (no error, just no modifications)
    /// </summary>
    internal static class ConfigLoader
    {
        /// <summary>
        /// Load and apply unit configurations from TOML file.
        /// Also loads projectile configurations from the same file.
        /// </summary>
        internal static void ApplyAllUnitConfigs()
        {
            if (!ConfigFileHelper.ConfigFileExists(ConfigPaths.Units)) return;

            try
            {
                // DEACTIVATED: Tag system is too complex for automated management within context limits
                // Units.Properties.TagsProperty.RegisterDefaultTags();

                // DEACTIVATED: Mismatch tracking for armor formulas
                // Units.Properties.MeleeArmorMultiplierProperty.ResetMismatchTracking();
                // Units.Properties.RangedArmorMultiplierProperty.ResetMismatchTracking();

                var tomlString = ConfigFileHelper.ReadConfigFile(ConfigPaths.Units);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);

                // Load projectile settings BEFORE processing units (affects RangedArmorMultiplier calculations)
                LoadProjectileSettings(tomlModel);

                // Filter unit sections vs projectile sections
                var unitModel = new TomlTable();
                var projectileModel = new TomlTable();
                var projectileTypeNames = new HashSet<string>(Enum.GetNames(typeof(ProjectileType)));
                var ignoredSections = new HashSet<string> { "ProjectileSettings" };

                foreach (var section in tomlModel)
                {
                    if (ignoredSections.Contains(section.Key)) continue;

                    if (projectileTypeNames.Contains(section.Key))
                    {
                        projectileModel[section.Key] = section.Value;
                    }
                    else
                    {
                        unitModel[section.Key] = section.Value;
                    }
                }

                // Apply unit configs from the filtered unit model
                var (unitProcessed, unitSkipped, unitErrors) = EntityProcessor.ProcessEntities(
                    unitModel,
                    UnitPropertyRegistry.Instance,
                    Data.UnitCategories.NonModable,
                    "unit",
                    TryParseUnit
                );

                // Apply projectile configs from the filtered projectile model
                var (projProcessed, projSkipped, projErrors) = EntityProcessor.ProcessEntities(
                    projectileModel,
                    ProjectilePropertyRegistry.Instance,
                    new ProjectileType[0], // No projectiles are non-modifiable
                    "projectile",
                    TryParseProjectile
                );

                Plugin.Logger.LogInfo($"Applied unit configs: Units={unitProcessed}, Projectiles={projProcessed}, Errors={unitErrors + projErrors}");

                // DEACTIVATED: Mismatch summaries for armor formulas
                // Units.Properties.MeleeArmorMultiplierProperty.LogMismatchSummary();
                // Units.Properties.RangedArmorMultiplierProperty.LogMismatchSummary();
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigLoadException("unit configs", ex);
            }
        }

        /// <summary>
        /// Load projectile settings from the [ProjectileSettings] section.
        /// These settings affect how ranged damage is calculated.
        /// </summary>
        private static void LoadProjectileSettings(TomlTable tomlModel)
        {
            if (!tomlModel.TryGetValue("ProjectileSettings", out var settingsObj))
                return;

            if (!(settingsObj is TomlTable settingsTable))
                return;

            // RangedDamageCap - maximum damage for ranged attacks
            if (settingsTable.TryGetValue("RangedDamageCap", out var capValue))
            {
                if (capValue is long capLong)
                {
                    int cap = (int)capLong;
                    if (cap >= 100 && cap <= 100000)
                    {
                        Units.Properties.RangedArmorMultiplierProperty.RangedDamageCap = cap;
                        Plugin.Logger.LogDebug($"Set RangedDamageCap = {cap}");
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Invalid RangedDamageCap value: {cap} (must be 100-100000)");
                    }
                }
            }
        }

        private static bool _sessionHooksRegistered = false;

        /// <summary>
        /// Register an OnStartMap hook to re-apply session-specific global settings.
        ///
        /// SetNoKnockdownWalls and SetAutoTrade are per-session: the game resets them when
        /// a new session starts, so they must be re-applied after each map load completes.
        /// Calling ApplyAllGlobalConfigs() here is idempotent and safe.
        /// </summary>
        internal static void RegisterSessionHooks()
        {
            if (_sessionHooksRegistered) return;
            _sessionHooksRegistered = true;

            MapLoaderR3EventHooks.OnStartMap.Observable
                .Subscribe(_ =>
                {
                    Plugin.Logger.LogInfo("[GlobalConfig] OnStartMap fired — re-applying session-specific global settings...");
                    ApplyAllGlobalConfigs();
                });

            // SetAutoTrade requires the marketplace to be active. Subscribe to building spawns so
            // auto-trade is applied the moment the local player's market is built (or re-spawned
            // from a save). Skips AI players via PlayerId check.
            BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post
                            && args.Building == eStructs.STRUCT_TRADEPOST
                            && args.PlayerId == (Plugin.PlayerApi?.GetLocalPlayerId() ?? -1))
                .Subscribe(args =>
                {
                    Plugin.Logger.LogInfo("[GlobalConfig] Local player marketplace spawned — applying auto-trade settings...");
                    ApplyAllGlobalConfigs();
                });

            // When a unit with RequiresHorse = true (per config) spawns for the local player,
            // link it to an available stable slot so the stable accurately tracks the unit.
            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args =>
                {
                    try
                    {
                        eChimps unitType = args.UnitType;
                        if (!Units.Properties.RequiresHorseProperty.HorseRequiringUnits.Contains(unitType))
                            return;

                        int unitId = (int)args.ReturnValue;
                        int localPlayerId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                        if (Plugin.UnitApi?.GetOwner(unitId) != localPlayerId)
                            return;

                        // Find the local player's stable building
                        var stableList = new List<int>();
                        Plugin.BuildingApi?.GetAllBuildings(stableList, null, eStructs.STRUCT_STABLES);
                        int stableId = -1;
                        foreach (int bid in stableList)
                        {
                            if (Plugin.BuildingApi.GetOwner(bid) == localPlayerId)
                            {
                                stableId = bid;
                                break;
                            }
                        }
                        if (stableId < 0) return;

                        // Find the first empty stable slot (global ID <= 0 means empty)
                        int emptySlot = -1;
                        for (int slot = 0; slot < 4; slot++)
                        {
                            if (Plugin.BuildingApi.GetStablesUnitGlobalIdLink(stableId, slot) <= 0)
                            {
                                emptySlot = slot;
                                break;
                            }
                        }
                        if (emptySlot < 0) return;

                        int unitGlobalId = Plugin.UnitApi?.GetGlobalId(unitId) ?? -1;
                        if (unitGlobalId < 0) return;

                        Plugin.BuildingApi?.SetStablesUnitIdLink(stableId, emptySlot, unitId, unitGlobalId);
                        Plugin.Logger.LogDebug($"[StableTracking] Linked {unitType} (id={unitId}) to stable {stableId} slot {emptySlot}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"[StableTracking] Error linking unit to stable: {ex.Message}");
                    }
                });

            Plugin.Logger.LogInfo("[GlobalConfig] Registered OnStartMap + OnBuildingSpawn + OnUnitCreate hooks.");
        }

        /// <summary>
        /// Load global settings from the [GameGlobals] and [PlayerOptions] sections.
        /// </summary>
        internal static void ApplyAllGlobalConfigs()
        {
            if (!ConfigFileHelper.ConfigFileExists(ConfigPaths.Globals)) return;

            try
            {
                var tomlString = ConfigFileHelper.ReadConfigFile(ConfigPaths.Globals);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);
                
                LoadGameGlobals(tomlModel);
                LoadPlayerOptions(tomlModel);
                LoadTradePrices(tomlModel);
                LoadAutoTrade(tomlModel);
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigLoadException("global configs", ex);
            }
        }

        private static void LoadGameGlobals(TomlTable tomlModel)
        {
            if (tomlModel.TryGetValue("Siege Engines", out var siegeObj) && siegeObj is TomlTable siegeTable)
            {
                if (siegeTable.TryGetValue("SiegeEngineRestockStoneAmount", out var craValue) && craValue is long cra)
                    Plugin.GlobalsApi?.CatapultRestockStoneAmount?.SetValue((ushort)cra);

                if (siegeTable.TryGetValue("SiegeEngineRestockStoneCost", out var crcValue) && crcValue is long crc)
                    Plugin.GlobalsApi?.CatapultRestockStoneCost?.SetValue((ushort)crc);
            }

            if (tomlModel.TryGetValue("Stealth", out var stealthObj) && stealthObj is TomlTable stealthTable)
            {
                if (stealthTable.TryGetValue("StealthDetectionRange", out var adrValue) && adrValue is long adr)
                    Plugin.GlobalsApi?.AssassinDetectionRange?.SetValue((ushort)adr);

                if (stealthTable.TryGetValue("StealthTransparencyThreshold", out var attValue) && attValue is long att)
                    Plugin.GlobalsApi?.AssassinTransparencyThreshold?.SetValue((ushort)att);
            }

            if (tomlModel.TryGetValue("Stables", out var stablesObj) && stablesObj is TomlTable stablesTable)
            {
                if (stablesTable.TryGetValue("StablesHorseRegenTickTarget", out var srtValue) && srtValue is long srt)
                    Plugin.GlobalsApi?.StablesHorseRegenTickTarget?.SetValue((short)srt);

                if (stablesTable.TryGetValue("StablesHorsesCap", out var shcValue) && shcValue is long shc)
                    Plugin.GlobalsApi?.StablesHorsesCap?.SetValue((sbyte)shc);
            }

            if (tomlModel.TryGetValue("Gatehouse", out var gateObj) && gateObj is TomlTable gateTable)
            {
                if (gateTable.TryGetValue("GateHouseCloseDistance", out var ghcValue) && ghcValue is long ghc)
                    Plugin.GlobalsApi?.GateHouseCloseDistance?.SetValue((ushort)ghc);

                if (gateTable.TryGetValue("GateHouseReOpenDistance", out var ghrValue) && ghrValue is long ghr)
                    Plugin.GlobalsApi?.GateHouseReOpenDistance?.SetValue((ushort)ghr);
            }

            if (tomlModel.TryGetValue("Disease", out var diseaseObj) && diseaseObj is TomlTable diseaseTable)
            {
                if (diseaseTable.TryGetValue("DiseaseDamage1", out var dd1Value) && dd1Value is long dd1)
                    Plugin.GlobalsApi?.DiseaseDamage1?.SetValue((int)dd1);

                if (diseaseTable.TryGetValue("DiseaseDamage2", out var dd2Value) && dd2Value is long dd2)
                    Plugin.GlobalsApi?.DiseaseDamage2?.SetValue((int)dd2);

                if (diseaseTable.TryGetValue("DiseaseDamage3", out var dd3Value) && dd3Value is long dd3)
                    Plugin.GlobalsApi?.DiseaseDamage3?.SetValue((int)dd3);
            }
        }

        private static void LoadPlayerOptions(TomlTable tomlModel)
        {
            if (!tomlModel.TryGetValue("Gameplay Options", out var settingsObj) || !(settingsObj is TomlTable settingsTable))
                return;

            bool ParseBool(string key, out bool val)
            {
                if (settingsTable.TryGetValue(key, out var raw) && raw is bool parsedBool)
                {
                    val = parsedBool;
                    return true;
                }
                val = false;
                return false;
            }

            // All gameplay options only override when true.
            // false = "don't override" (leave game default as-is), not "explicitly disable".
            if (ParseBool("BetterHealers", out var bh) && bh)
                Plugin.PlayerApi?.SetBetterHealers(true);
            if (ParseBool("FasterPeasants", out var fp) && fp)
                Plugin.PlayerApi?.SetFasterPeasants(true);
            if (ParseBool("ImprovedArabSwordsman", out var ias) && ias)
                Plugin.PlayerApi?.SetImprovedArabSwordsman(true);
            if (ParseBool("ImprovedFletchers", out var inf) && inf)
                Plugin.PlayerApi?.SetImprovedFletchers(true);
            if (ParseBool("ImprovedLadderman", out var il) && il)
                Plugin.PlayerApi?.SetImprovedLadderman(true);
            if (ParseBool("ImprovedSpearman", out var isp) && isp)
                Plugin.PlayerApi?.SetImprovedSpearman(true);
            if (ParseBool("NerfEunuchs", out var ne) && ne)
                Plugin.PlayerApi?.SetNerfEunuchs(true);
            if (ParseBool("NoKnockdownWalls", out var nkw) && nkw)
                Plugin.PlayerApi?.SetNoKnockdownWalls(true);
            if (ParseBool("RebalancedHorseArchers", out var rha) && rha)
                Plugin.PlayerApi?.SetRebalancedHorseArchers(true);
            if (ParseBool("UncappedPeasants", out var ucp) && ucp)
                Plugin.PlayerApi?.SetUncappedPeasants(true);
            if (ParseBool("AllBuildingsAvailable", out var aba) && aba)
                Plugin.PlayerApi?.SetAllBuildingAvailability(true);
            if (ParseBool("AllUnitsAllowed", out var aua) && aua)
                Plugin.PlayerApi?.SetAllUnitsAllowed(true);
            if (ParseBool("AllTradeGoodsAllowed", out var atga) && atga)
                Plugin.PlayerApi?.SetAllTradeGoodsAllowed(true);
            if (ParseBool("AllProductionGoodsAllowed", out var apga) && apga)
                Plugin.PlayerApi?.SetAllProductionGoodAllowed(true);
        }

        private static void LoadAutoTrade(TomlTable tomlModel)
        {
            if (!tomlModel.TryGetValue("Auto Trade", out var atObj) || !(atObj is TomlTable atTable))
                return;

            int applied = 0;

            foreach (var kvp in atTable)
            {
                if (!(kvp.Value is TomlTable goodTable))
                    continue;

                if (!Enum.TryParse<eGoods>(kvp.Key, out var good))
                {
                    Plugin.Logger.LogWarning($"[Auto Trade] Unknown goods type: '{kvp.Key}'");
                    continue;
                }

                bool enabled = false;
                ushort buyLevel = 0;
                ushort sellLevel = 0;

                if (goodTable.TryGetValue("Enabled", out var enabledVal) && enabledVal is bool enabledBool)
                    enabled = enabledBool;

                if (goodTable.TryGetValue("BuyLevel", out var buyVal) && buyVal is long buyLong)
                    buyLevel = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (long)buyLong));

                if (goodTable.TryGetValue("SellLevel", out var sellVal) && sellVal is long sellLong)
                    sellLevel = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (long)sellLong));

                Plugin.PlayerApi?.SetAutoTrade(good, enabled, buyLevel, sellLevel);
                applied++;
            }

            if (applied > 0)
                Plugin.Logger.LogInfo($"[Auto Trade] Applied {applied} auto-trade settings");
        }

        private static void LoadTradePrices(TomlTable tomlModel)
        {
            if (!tomlModel.TryGetValue("Trade Prices", out var tpObj) || !(tpObj is TomlTable tpTable))
                return;

            int applied = 0;

            foreach (var kvp in tpTable)
            {
                if (!(kvp.Value is TomlTable goodTable))
                    continue;

                if (!Enum.TryParse<eGoods>(kvp.Key, out var good))
                {
                    Plugin.Logger.LogWarning($"[Trade Prices] Unknown good: '{kvp.Key}'");
                    continue;
                }

                // Get current prices — preserves whichever price is not configured.
                var currentPriceOpt = Plugin.PlayerApi?.GetTradeBasePrice(good);
                if (!currentPriceOpt.HasValue) continue;

                var currentPrice = currentPriceOpt.Value;
                bool changed = false;

                if (goodTable.TryGetValue("BuyPrice", out var buyVal) && buyVal is long buyLong && buyLong > 0)
                {
                    if ((int)buyLong != currentPrice.BuyPrice)
                    {
                        currentPrice.BuyPrice = (int)buyLong;
                        changed = true;
                    }
                }

                if (goodTable.TryGetValue("SellPrice", out var sellVal) && sellVal is long sellLong && sellLong > 0)
                {
                    if ((int)sellLong != currentPrice.SellPrice)
                    {
                        currentPrice.SellPrice = (int)sellLong;
                        changed = true;
                    }
                }

                if (changed)
                {
                    Plugin.PlayerApi?.SetTradeBasePrice(good, currentPrice);
                    applied++;
                }
            }

            if (applied > 0)
                Plugin.Logger.LogInfo($"[Trade Prices] Applied {applied} trade price overrides");
        }

        /// <summary>
        /// Helper method to parse projectile enum from string key.
        /// </summary>
        private static ProjectileType? TryParseProjectile(string key)
        {
            if (Enum.TryParse<ProjectileType>(key, out var projectile))
                return projectile;
            return (ProjectileType?)null;
        }

        /// <summary>
        /// Load and apply structure configurations from TOML file.
        /// </summary>
        internal static void ApplyAllStructureConfigs()
        {
            ApplyConfigs(
                filePath: ConfigPaths.Structures,
                registry: StructurePropertyRegistry.Instance,
                nonModifiableEntities: Data.StructureCategories.NonModable,
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
                var tomlString = ConfigFileHelper.ReadConfigFile(filePath);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);

                var (processedCount, skippedCount, errorCount) = EntityProcessor.ProcessEntities(
                    tomlModel,
                    registry,
                    nonModifiableEntities,
                    entityTypeName,
                    parseEntity
                );

                Plugin.Logger.LogInfo($"Applied {entityTypeName} configs: Processed={processedCount}, Skipped={skippedCount}, Errors={errorCount}");
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigLoadException($"{entityTypeName} configs", ex);
            }
        }
    }
}