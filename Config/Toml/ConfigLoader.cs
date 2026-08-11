using System;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.BepInEx.Systems.Handlers;
using CrusaderDETweaker.Config.Toml;
using CrusaderDETweaker.Config.Toml.Core;
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
    /// Units and structures are each applied by a dedicated entry point (ApplyAllUnitConfigs /
    /// ApplyAllStructureConfigs) that shares the EntityProcessor pipeline. Property handlers are
    /// obtained from the appropriate registry (UnitPropertyRegistry or StructurePropertyRegistry).
    /// 
    /// Error handling:
    /// - Invalid entity names are logged and skipped
    /// - Non-modifiable entities are skipped (e.g., special game units)
    /// - Property load errors are logged but don't stop the loading process
    /// - Missing config files are handled gracefully (no error, just no modifications)
    /// </summary>
    internal static class ConfigLoader
    {
        private static Dictionary<eChimps, int> _unitCaps = new Dictionary<eChimps, int>();
        private static Dictionary<eStructs, int> _buildingCaps = new Dictionary<eStructs, int>();

        /// <summary>
        /// Load and apply unit configurations from the Units TOML file.
        /// </summary>
        internal static void ApplyAllUnitConfigs()
        {
            if (!ConfigFileHelper.ConfigFileExists(ConfigPaths.Units)) return;

            try
            {
                var tomlString = ConfigFileHelper.ReadConfigFile(ConfigPaths.Units);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);

                var (unitProcessed, unitSkipped, unitErrors) = EntityProcessor.ProcessEntities(
                    tomlModel,
                    UnitPropertyRegistry.Instance,
                    Data.UnitCategories.NonModable,
                    "unit",
                    TryParseUnit
                );

                Plugin.Logger.LogInfo($"Applied unit configs: Units={unitProcessed}, Skipped={unitSkipped}, Errors={unitErrors}");

                LoadUnitCaps(tomlModel);
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigLoadException("unit configs", ex);
            }
        }

        private static bool _sessionHooksRegistered = false;
        private static List<int> _reusableStableList = new List<int>();

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
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ =>
                {
                    Plugin.Logger.LogInfo("[GlobalConfig] OnStartMap (Post) fired — re-applying session-specific global settings...");
                    ApplyAllGlobalConfigs();
                });

            // For trail/campaign missions, DLL_LoadMapToPlay fires AFTER OnStartMap and applies
            // map-specific unit/building restrictions that override our settings. Re-apply on Post.
            MapLoaderR3EventHooks.OnLoadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ =>
                {
                    Plugin.Logger.LogInfo("[GlobalConfig] OnLoadMap (Post) fired — re-applying gameplay options after map rules...");
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

            // When a unit with RequiresHorse = true (per config) appears for the local player,
            // link it to an available stable slot so the stable accurately tracks the unit.
            // Two arrival paths (see MakeTroopRecruitHook.cs / UnitTransitionDispatcher.cs):
            //   - spawned units fire OnUnitCreate (Post);
            //   - RECRUITED units never do — they arrive via OnUnitTransition, re-raised by the
            //     dispatcher after the transformation finished so GetGlobalId/owner reads are valid.
            UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => TryLinkUnitToStable(args.UnitType, (int)args.ReturnValue));

            Config.Core.UnitTransitionDispatcher.TransitionSettled +=
                (unitId, newType) => TryLinkUnitToStable(newType, unitId);

            UnitCapHandler.Subscribe(_unitCaps);
            BuildingCapHandler.Subscribe(_buildingCaps);

            Plugin.Logger.LogInfo("[GlobalConfig] Registered OnStartMap + OnLoadMap + OnBuildingSpawn + OnUnitCreate + OnUnitTransition hooks.");
        }

        /// <summary>
        /// Link one horse-requiring unit of the local player to the first empty slot of their
        /// first stable. No-op for unit types without RequiresHorse = true, non-local owners,
        /// or when no stable/slot is available.
        /// </summary>
        private static void TryLinkUnitToStable(eChimps unitType, int unitId)
        {
            try
            {
                if (!Units.Properties.RequiresHorseProperty.HorseRequiringUnits.Contains(unitType))
                    return;

                int localPlayerId = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
                if (Plugin.UnitApi?.GetOwner(unitId) != localPlayerId)
                    return;

                // Reuse static list to prevent garbage collection allocation on every unit spawn
                if (_reusableStableList == null) _reusableStableList = new List<int>();
                _reusableStableList.Clear();

                Plugin.BuildingApi?.GetAllBuildings(_reusableStableList, null, eStructs.STRUCT_STABLES);
                int stableId = -1;
                foreach (int bid in _reusableStableList)
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
        }

        /// <summary>
        /// Load global settings from the [GameGlobals] and [PlayerOptions] sections.
        /// </summary>
        internal static void ApplyAllGlobalConfigs()
        {
            if (!ConfigFileHelper.ConfigFileExists(ConfigPaths.Globals)) return;

            bool dbg = BepInExConfigManager.DebugLogging?.Value ?? false;

            try
            {
                var tomlString = ConfigFileHelper.ReadConfigFile(ConfigPaths.Globals);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);

                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] Applying: LoadGameGlobals...");
                LoadGameGlobals(tomlModel);
                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] Applying: LoadPeasantSpawning...");
                LoadPeasantSpawning(tomlModel);
                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] Applying: LoadPlayerOptions...");
                LoadPlayerOptions(tomlModel, dbg);
                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] Applying: LoadTradePrices...");
                LoadTradePrices(tomlModel);
                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] Applying: LoadAutoTrade...");
                LoadAutoTrade(tomlModel);
                if (dbg) Plugin.Logger.LogInfo("[GlobalConfig] All sections applied.");
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
                    Plugin.GlobalsApi?.CatapultRestockStoneAmount?.SetValue((ushort)ClampInteger("Siege Engines", "SiegeEngineRestockStoneAmount", cra, ushort.MinValue, ushort.MaxValue));

                if (siegeTable.TryGetValue("SiegeEngineRestockStoneCost", out var crcValue) && crcValue is long crc)
                    Plugin.GlobalsApi?.CatapultRestockStoneCost?.SetValue((ushort)ClampInteger("Siege Engines", "SiegeEngineRestockStoneCost", crc, ushort.MinValue, ushort.MaxValue));

                if (siegeTable.TryGetValue("SiegeEngineInitialStoneAmount", out var cisValue) && cisValue is long cis)
                    Plugin.GlobalsApi?.CatapultInitialStoneAmount?.SetValue((byte)ClampInteger("Siege Engines", "SiegeEngineInitialStoneAmount", cis, byte.MinValue, byte.MaxValue));
            }

            if (tomlModel.TryGetValue("Stealth", out var stealthObj) && stealthObj is TomlTable stealthTable)
            {
                if (stealthTable.TryGetValue("StealthDetectionRange", out var adrValue) && adrValue is long adr)
                    Plugin.GlobalsApi?.AssassinDetectionRange?.SetValue((ushort)ClampInteger("Stealth", "StealthDetectionRange", adr, ushort.MinValue, ushort.MaxValue));

                if (stealthTable.TryGetValue("StealthTransparencyThreshold", out var attValue) && attValue is long att)
                    Plugin.GlobalsApi?.AssassinTransparencyThreshold?.SetValue((ushort)ClampInteger("Stealth", "StealthTransparencyThreshold", att, ushort.MinValue, ushort.MaxValue));
            }

            if (tomlModel.TryGetValue("Stables", out var stablesObj) && stablesObj is TomlTable stablesTable)
            {
                if (stablesTable.TryGetValue("StablesHorseRegenTickTarget", out var srtValue) && srtValue is long srt)
                    Plugin.GlobalsApi?.StablesHorseRegenTickTarget?.SetValue((short)ClampInteger("Stables", "StablesHorseRegenTickTarget", srt, short.MinValue, short.MaxValue));

                if (stablesTable.TryGetValue("StablesHorsesCap", out var shcValue) && shcValue is long shc)
                    Plugin.GlobalsApi?.StablesHorsesCap?.SetValue((sbyte)ClampInteger("Stables", "StablesHorsesCap", shc, sbyte.MinValue, sbyte.MaxValue));
            }

            if (tomlModel.TryGetValue("Gatehouse", out var gateObj) && gateObj is TomlTable gateTable)
            {
                if (gateTable.TryGetValue("GateHouseCloseDistance", out var ghcValue) && ghcValue is long ghc)
                    Plugin.GlobalsApi?.GateHouseCloseDistance?.SetValue((ushort)ClampInteger("Gatehouse", "GateHouseCloseDistance", ghc, ushort.MinValue, ushort.MaxValue));

                if (gateTable.TryGetValue("GateHouseReOpenDistance", out var ghrValue) && ghrValue is long ghr)
                    Plugin.GlobalsApi?.GateHouseReOpenDistance?.SetValue((ushort)ClampInteger("Gatehouse", "GateHouseReOpenDistance", ghr, ushort.MinValue, ushort.MaxValue));
            }

            if (tomlModel.TryGetValue("Pathfinding", out var pfObj) && pfObj is TomlTable pfTable)
            {
                if (pfTable.TryGetValue("PathfindingMaxTilesConstraint", out var pfValue) && pfValue is long pfConstraint)
                    Plugin.GlobalsApi?.PathfindingMaxTilesConstraint?.SetValue((ushort)ClampInteger("Pathfinding", "PathfindingMaxTilesConstraint", pfConstraint, ushort.MinValue, ushort.MaxValue));
            }

            if (tomlModel.TryGetValue("Food Consumption", out var fcObj) && fcObj is TomlTable fcTable)
            {
                if (fcTable.TryGetValue("FoodConsumptionTickThreshold", out var fctValue) && fctValue is long fct)
                    Plugin.GlobalsApi?.FoodConsumptionTickThreshold?.SetValue((int)ClampInteger("Food Consumption", "FoodConsumptionTickThreshold", fct, int.MinValue, int.MaxValue));

                if (fcTable.TryGetValue("FoodConsumptionRate", out var fcrValue) && fcrValue is long fcr)
                    Plugin.PlayerApi?.FoodConsumptionRate?.SetValue((int)ClampInteger("Food Consumption", "FoodConsumptionRate", fcr, int.MinValue, int.MaxValue));
            }

            if (tomlModel.TryGetValue("Disease", out var diseaseObj) && diseaseObj is TomlTable diseaseTable)
            {
                // The [Multipliers] DiseaseDamageMultiplier (BepInEx cfg) scales on top of the TOML
                // base, applied HERE (every map load) rather than once at init. This loader is the
                // last writer of the disease-tier globals on each map start, so applying the
                // multiplier at init was overwritten and had no effect; doing it here is also safe
                // (map-load hook, not the LibraryLoaded init path). Floor of 1 prevents 0 damage.
                float diseaseMult = BepInExConfigManager.FireAndHeal?.DiseaseDamageMultiplier?.Value ?? 1.0f;

                if (diseaseTable.TryGetValue("DiseaseDamage1", out var dd1Value) && dd1Value is long dd1)
                    Plugin.GlobalsApi?.DiseaseDamage1?.SetValue(ScalePositiveInt32("Disease", "DiseaseDamage1", dd1, diseaseMult));

                if (diseaseTable.TryGetValue("DiseaseDamage2", out var dd2Value) && dd2Value is long dd2)
                    Plugin.GlobalsApi?.DiseaseDamage2?.SetValue(ScalePositiveInt32("Disease", "DiseaseDamage2", dd2, diseaseMult));

                if (diseaseTable.TryGetValue("DiseaseDamage3", out var dd3Value) && dd3Value is long dd3)
                    Plugin.GlobalsApi?.DiseaseDamage3?.SetValue(ScalePositiveInt32("Disease", "DiseaseDamage3", dd3, diseaseMult));
            }
        }

        private static void LoadPeasantSpawning(TomlTable tomlModel)
        {
            if (!tomlModel.TryGetValue("Peasant Spawning", out var psObj) || !(psObj is TomlTable psTable))
                return;

            if (psTable.TryGetValue("PeasantRespawnTickTargetValue", out var targetVal) && targetVal is long target)
                Plugin.GlobalsApi?.PeasantRespawnTickTargetValue?.SetValue((ushort)ClampInteger("Peasant Spawning", "PeasantRespawnTickTargetValue", target, ushort.MinValue, ushort.MaxValue));

            if (psTable.TryGetValue("PeasantRespawnTickResetValue", out var resetVal) && resetVal is long reset)
                Plugin.GlobalsApi?.PeasantRespawnTickResetValue?.SetValue((ushort)ClampInteger("Peasant Spawning", "PeasantRespawnTickResetValue", reset, ushort.MinValue, ushort.MaxValue));

            if (psTable.TryGetValue("CampPeasantsCap", out var capVal) && capVal is long cap)
                Plugin.GlobalsApi?.CampPeasantsCap?.SetValue((ushort)ClampInteger("Peasant Spawning", "CampPeasantsCap", cap, ushort.MinValue, ushort.MaxValue));
        }

        private static void LoadPlayerOptions(TomlTable tomlModel, bool dbg = false)
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
            // Each call is individually guarded — some options rely on map-load-time structures.
            // Getter is optional: if provided, the result is read back to confirm the setting took effect.
            //
            // Advanced sub-options (ChoreManagerOptions-backed) are tracked so the game's master
            // flags can be raised below: whether the game honors a sub-option while AdvancedOptions /
            // AdvancedSkirmishOptions is off is undocumented, so the masters are set whenever any
            // sub-option is in use. The read-back only confirms the memory write, not the gate.
            bool advancedOptionUsed = false;
            advancedOptionUsed |= TryApply("BetterHealers",             () => Plugin.PlayerApi?.SetBetterHealers(true),            () => Plugin.PlayerApi?.IsBetterHealers());
            advancedOptionUsed |= TryApply("FasterPeasants",            () => Plugin.PlayerApi?.SetFasterPeasants(true),           () => Plugin.PlayerApi?.IsFasterPeasants());
            advancedOptionUsed |= TryApply("ImprovedArabSwordsman",     () => Plugin.PlayerApi?.SetImprovedArabSwordsman(true),    () => Plugin.PlayerApi?.IsImprovedArabSwordsman());
            advancedOptionUsed |= TryApply("ImprovedFletchers",         () => Plugin.PlayerApi?.SetImprovedFletchers(true),        () => Plugin.PlayerApi?.IsImprovedFletchers());
            advancedOptionUsed |= TryApply("ImprovedLadderman",         () => Plugin.PlayerApi?.SetImprovedLadderman(true),        () => Plugin.PlayerApi?.IsImprovedLadderman());
            advancedOptionUsed |= TryApply("ImprovedSpearman",          () => Plugin.PlayerApi?.SetImprovedSpearman(true),         () => Plugin.PlayerApi?.IsImprovedSpearman());
            advancedOptionUsed |= TryApply("NerfEunuchs",               () => Plugin.PlayerApi?.SetNerfEunuchs(true),              () => Plugin.PlayerApi?.IsNerfEunuchs());
            advancedOptionUsed |= TryApply("RebalancedHorseArchers",    () => Plugin.PlayerApi?.SetRebalancedHorseArchers(true),   () => Plugin.PlayerApi?.IsRebalancedHorseArchers());
            advancedOptionUsed |= TryApply("UncappedPeasants",          () => Plugin.PlayerApi?.SetUncappedPeasants(true),         () => Plugin.PlayerApi?.IsUncappedPeasants());
            // Native-global-pointer options (not ChoreManagerOptions-backed): no master flag involved
            TryApply("NoKnockdownWalls",                   () => Plugin.PlayerApi?.SetNoKnockdownWalls(true),                   () => Plugin.PlayerApi?.IsNoKnockdownWalls());
            TryApply("GlobalImprovedSiegeBehaviour",       () => Plugin.PlayerApi?.SetGlobalImprovedSiegeBehaviour(true),       () => Plugin.PlayerApi?.IsGlobalImprovedSiegeBehaviour());
            TryApply("GlobalMoreAggressiveSiegeBehaviour", () => Plugin.PlayerApi?.SetGlobalMoreAggressiveSiegeBehaviour(true), () => Plugin.PlayerApi?.IsGlobalMoreAggressiveSiegeBehaviour());
            // Map-rules options: no single getter for "all", so no read-back — failure is logged via exception
            TryApply("AllBuildingsAvailable",     () => Plugin.PlayerApi?.SetAllBuildingAvailability(true),  null);
            TryApply("AllUnitsAllowed",           () => Plugin.PlayerApi?.SetAllUnitsAllowed(true),          null);
            TryApply("AllTradeGoodsAllowed",      () => Plugin.PlayerApi?.SetAllTradeGoodsAllowed(true),     null);
            TryApply("AllProductionGoodsAllowed", () => Plugin.PlayerApi?.SetAllProductionGoodAllowed(true), null);
            // Master switches for the game's advanced options: explicit config values apply like any
            // other option, and both are auto-enabled when any advanced sub-option above was overridden.
            TryApply("AdvancedOptionsEnabled",         () => Plugin.PlayerApi?.SetAdvancedOptionsEnabled(true),         () => Plugin.PlayerApi?.IsAdvancedOptionsEnabled());
            TryApply("AdvancedSkirmishOptionsEnabled", () => Plugin.PlayerApi?.SetAdvancedSkirmishOptionsEnabled(true), () => Plugin.PlayerApi?.IsAdvancedSkirmishOptionsEnabled());
            if (advancedOptionUsed)
            {
                try
                {
                    Plugin.PlayerApi?.SetAdvancedOptionsEnabled(true);
                    Plugin.PlayerApi?.SetAdvancedSkirmishOptionsEnabled(true);
                    if (dbg) Plugin.Logger.LogInfo("[GameplayOptions] Advanced sub-option overrides active — raised AdvancedOptions/AdvancedSkirmishOptions master flags");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameplayOptions] Failed to raise advanced-options master flags: {ex.Message}");
                }
            }

            bool TryApply(string key, Action action, Func<bool?> getter)
            {
                if (!ParseBool(key, out var val) || !val) return false;
                try
                {
                    action();

                    if (getter != null)
                    {
                        bool? actual = getter();
                        if (actual == true)
                        {
                            if (dbg) Plugin.Logger.LogInfo($"[GameplayOptions] {key} = true (confirmed)");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"[GameplayOptions] {key} was set but read back as {actual?.ToString() ?? "null"} — may not have taken effect");
                        }
                    }
                    else
                    {
                        if (dbg) Plugin.Logger.LogInfo($"[GameplayOptions] {key} = true (applied, no read-back available)");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[GameplayOptions] {key} failed: {ex.Message}");
                }
                return true;
            }
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
                    buyLevel = (ushort)ClampInteger("Auto Trade", $"{kvp.Key}.BuyLevel", buyLong, ushort.MinValue, ushort.MaxValue);

                if (goodTable.TryGetValue("SellLevel", out var sellVal) && sellVal is long sellLong)
                    sellLevel = (ushort)ClampInteger("Auto Trade", $"{kvp.Key}.SellLevel", sellLong, ushort.MinValue, ushort.MaxValue);

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
                    int buyPrice = (int)ClampInteger("Trade Prices", $"{kvp.Key}.BuyPrice", buyLong, 1, int.MaxValue);
                    if (buyPrice != currentPrice.BuyPrice)
                    {
                        currentPrice.BuyPrice = buyPrice;
                        changed = true;
                    }
                }

                if (goodTable.TryGetValue("SellPrice", out var sellVal) && sellVal is long sellLong && sellLong > 0)
                {
                    int sellPrice = (int)ClampInteger("Trade Prices", $"{kvp.Key}.SellPrice", sellLong, 1, int.MaxValue);
                    if (sellPrice != currentPrice.SellPrice)
                    {
                        currentPrice.SellPrice = sellPrice;
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

        private static void LoadUnitCaps(TomlTable unitModel)
        {
            _unitCaps.Clear();
            foreach (var kvp in unitModel)
            {
                if (!(kvp.Value is TomlTable section)) continue;
                if (!section.TryGetValue("MaxCount", out var mcVal) || !(mcVal is long mc)) continue;
                // Any negative value means unlimited. Check before narrowing so long.MinValue cannot wrap.
                if (mc < 0) continue;
                if (mc > int.MaxValue)
                {
                    Plugin.Logger.LogWarning($"[UnitCaps] {kvp.Key}.MaxCount={mc} exceeds int.MaxValue; clamping to {int.MaxValue}.");
                    mc = int.MaxValue;
                }
                int cap = (int)mc;
                if (!Enum.TryParse<eChimps>(kvp.Key, out var unit)) continue;
                _unitCaps[unit] = cap;
            }
            if (_unitCaps.Count > 0)
                Plugin.Logger.LogInfo($"[UnitCaps] Loaded {_unitCaps.Count} unit cap(s) from TOML.");
        }

        private static void LoadBuildingCaps(TomlTable structModel)
        {
            _buildingCaps.Clear();
            foreach (var kvp in structModel)
            {
                if (!(kvp.Value is TomlTable section)) continue;
                if (!section.TryGetValue("MaxCount", out var mcVal) || !(mcVal is long mc)) continue;
                // Any negative value means unlimited. Check before narrowing so long.MinValue cannot wrap.
                if (mc < 0) continue;
                if (mc > int.MaxValue)
                {
                    Plugin.Logger.LogWarning($"[BuildingCaps] {kvp.Key}.MaxCount={mc} exceeds int.MaxValue; clamping to {int.MaxValue}.");
                    mc = int.MaxValue;
                }
                int cap = (int)mc;
                if (!Enum.TryParse<eStructs>(kvp.Key, out var structure)) continue;
                _buildingCaps[structure] = cap;
            }
            if (_buildingCaps.Count > 0)
                Plugin.Logger.LogInfo($"[BuildingCaps] Loaded {_buildingCaps.Count} building cap(s) from TOML.");
        }

        private static long ClampInteger(string section, string key, long value, long minimum, long maximum)
        {
            long clamped = Math.Max(minimum, Math.Min(maximum, value));
            if (clamped != value)
            {
                Plugin.Logger.LogWarning($"[{section}] {key}={value} is outside [{minimum}, {maximum}]; clamping to {clamped}.");
            }

            return clamped;
        }

        private static int ScalePositiveInt32(string section, string key, long value, float multiplier)
        {
            double scaled = value * (double)multiplier;
            if (double.IsNaN(scaled))
            {
                Plugin.Logger.LogWarning($"[{section}] {key} produced NaN after applying multiplier {multiplier}; using 1.");
                return 1;
            }

            if (scaled > int.MaxValue)
            {
                Plugin.Logger.LogWarning($"[{section}] {key}={value} exceeds int.MaxValue after applying multiplier {multiplier}; clamping to {int.MaxValue}.");
                return int.MaxValue;
            }

            return scaled < 1 ? 1 : (int)scaled;
        }

        /// <summary>
        /// Load and apply structure configurations from TOML file.
        /// </summary>
        internal static void ApplyAllStructureConfigs()
        {
            if (!ConfigFileHelper.ConfigFileExists(ConfigPaths.Structures)) return;

            try
            {
                var tomlString = ConfigFileHelper.ReadConfigFile(ConfigPaths.Structures);
                var tomlModel = Tomlyn.Toml.ToModel(tomlString);

                var (processedCount, skippedCount, errorCount) = EntityProcessor.ProcessEntities(
                    tomlModel,
                    StructurePropertyRegistry.Instance,
                    Data.StructureCategories.NonModable,
                    "structure",
                    TryParseStructure
                );

                Plugin.Logger.LogInfo($"Applied structure configs: Processed={processedCount}, Skipped={skippedCount}, Errors={errorCount}");

                LoadBuildingCaps(tomlModel);
            }
            catch (Exception ex)
            {
                Core.ErrorLogging.LogConfigLoadException("structure configs", ex);
            }
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
    }
}
