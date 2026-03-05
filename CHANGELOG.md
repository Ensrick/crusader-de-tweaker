2.1.5 - TOML generation improvements, Killing Pit damage property (2026-03-05):

       - **New: KillingPit Damage property** — `[KillingPit]` section added to `CrusaderDETweaker_Structures.toml`. The `Damage` property controls the fire damage per tick applied to units standing in a killing pit (default: 18000). Value is validated to stay within the game's 0–32767 range.
       - **Improved: TOML generation skips zero-cost entries** — Cost properties (WoodCost, StoneCost, IronIngotCost, RawPitchCost, GoldCost) are no longer generated for buildings that have a base cost of 0. Reduces TOML noise significantly; users can add overrides manually for any skipped entry.
       - **Improved: TOML generation skips zero-housing entries** — `PopulationSpace` is no longer generated for structures with a base housing value of 0. Same principle as cost entries.
       - **Improved: `# Default: X` comments on all TOML properties** — Every generated property entry now includes a comment showing the game's original default value (e.g., `# Default: 1200`). Makes it easy to see what you're changing and revert to vanilla without restarting.

2.1.4 - Structure multiplier fix, debug logging system (2026-03-03):

       - **Fix: Wall/Tower/Gatehouse/Civil structure multipliers now apply correctly** — `StructureDamageMultiplierHandler` was casting `args.TileId` to `ushort` before passing it to `GetTileBuildingId()` and `GetTilePropertyFlag()`. On an 800×800 map, tile IDs can reach 640,000 — far above the ushort max of 65,535. The silent truncation corrupted ~90% of grid lookups, causing `buildingId` to always resolve to 0, structure type detection to always fail, and `WallDamageTakenMultiplier`, `TowerDamageTakenMultiplier`, and `CivilStructureDamageTakenMultiplier` to be silently skipped for every hit. Only the global `StructureDamageTakenMultiplier` and sapper/demolisher multipliers (which don't require type detection) were applying. Fixed by passing `args.TileId` directly as `int`, which is already a valid `StructureGrid` index.
       - **Fix: StructureTypeDetector tile ID parameter widened from `ushort` to `int`** — Same root cause: `Detect()` accepted a `ushort tileId`, silently truncating the value before any grid lookups inside the method. Parameter changed to `int`.
       - **New: Toggleable debug logging system** — `[Debug]` section added to `CrusaderDETweaker_GlobalMultipliers.cfg` with a `DebugLogging` flag (default: `false`). When enabled, all four event hooks (`OnBuildingTileTakeDamage`, `OnUnitTakeMeleeDamage`, `OnUnitTakeProjectileDamageEx`, `OnUnitCreate`) emit step-by-step log entries showing intermediate values at each processing stage. Also adds RAW unfiltered subscriptions to each hook for confirming the hook fires at all. Changes apply in real-time — no restart needed.

2.1.3 - GoldCost fix, Sapper/Demolisher logging, release tooling (2026-03-02):

       - **Fix: GoldCost now applies to Barracks units** — `GoldCostProperty` was writing to the managed `_unitGoldCostsDict` cache via reflection instead of calling `SetUnitGoldCost`. The cache is what the GUI tooltip and mercenary (Mercenary Post) recruitment read from, so Arab/Bedouin costs appeared to work. However, Barracks recruitment reads from a separate native cost table that only `SetUnitGoldCost` updates — so Swordsman, Pikeman, Maceman, Crossbowman, Knight, Archer, and Spearman gold costs were silently ignored in-game. Fixed by replacing the reflection approach with a direct `SetUnitGoldCost` call.
       - **Debug: Sapper/Demolisher hit logging** — `StructureDamageMultiplierHandler` now logs `[Info]` messages when a Demolisher or Sapper hits a structure, showing raw damage before the hook, the multiplier applied, and the final damage value. Only fires for these two unit types; no spam for normal hits.
       - **New script: `backup_configs.ps1`** — copies all config and damage matrix files to `{GameDir}\BepInEx\config\CrusaderDETweaker\Backups\<name>`. Defaults to a timestamp name; pass `-Name "label"` for a named backup.
       - **New script: `restore_configs.ps1`** — run without arguments to list available backups; pass `-Name "label"` to restore.
       - **New script: `package_release.ps1`** — copies the live plugin and config folders into the staging directory (`D:\Game Mods\Stronghold\Crusader DE Tweaker\BepInEx\`), excludes the `Backups` subfolder, and repacks `Crusader DE Tweaker.zip`.

2.1.2 - Trade prices re-enabled, Demolisher/Sapper unit filtering (2026-02-28):

       - **Fix: Trade Prices re-enabled** — SHCDE-SE 1.20.0 fixed the `GetTradeBasePrice` API. The getter now returns a `PackedGoodPrice` struct with separate `BuyPrice` and `SellPrice` fields. The setter is now correctly named `SetTradeBasePrice`. The `[Trade Prices]` section is re-enabled in the generated config; each good has `BuyPrice` and `SellPrice` sub-keys (0 = use game default, no override).
       - **Fix: Demolisher/Sapper filtering by unit type** — `OnBuildingTileTakeDamage` exposes `AttackingUnitId`. The handler now calls `GameUnitManagerAPI.GetType(attackingUnitId)` and filters for `eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER` / `CHIMP_TYPE_BEDOUIN_SAPPER` instead of matching known base damage values.
       - **Updated docs**: `GamePlayerManagerAPI.md` — updated Trade Prices section for SHCDE-SE 1.20.0 API.
       - **Updated docs**: `CONFIGURATION_GUIDE.md` — Trade Prices section re-enabled; added `TowerDamageTakenMultiplier`, `DemolisherBuildingDamageMultiplier`, `SapperBuildingDamageMultiplier` to cfg reference.

2.1.1 - Trade price bugfixes, gameplay option fix (2026-02-27):

       - **Fix: Trade Prices disabled** — `[Trade Prices]` section removed from generated config pending a Script Extender fix. The `GetTradeBasePrice` getter truncates its return value to 32 bits, making it impossible to read back the sell price; calling the setter would silently zero the sell price for any changed good. The loader code is retained and will be re-enabled once the API is corrected.
       - **Fix: Gameplay options** — All `[Gameplay Options]` flags (`BetterHealers`, `FasterPeasants`, `NerfEunuchs`, etc.) now only call the API when set to `true`. Previously `false` was actively applied, which could override in-game settings with the wrong value.

2.1.0 - Stables, Disease, Shield Health API (2026-02-27):

       - **New: Stables config** - `[Stables]` section in `CrusaderDETweaker_GameplaySettings.toml` exposes `StablesHorseRegenTickTarget` (default: 550) and `StablesHorsesCap` (default: 4, max safe value). Warning: cap above 4 breaks horse-link tracking.
       - **New: Disease Damage config** - `[Disease]` section exposes `DiseaseDamage1/2/3` (defaults: 150/200/400) sourced from `c_game_unit_takedamage_projectile`
       - **New: DiseaseDamageMultiplier** - Added `DiseaseDamageMultiplier` to `CrusaderDETweaker_GlobalMultipliers.cfg`; scales all three disease damage tiers at startup. Stacks with `[Disease]` TOML values.
       - **Fix: ShieldHealth property** - `ShieldHealthProperty` now uses `GameUnitManagerAPI.GetDefaultShieldHealth()` / `SetDefaultShieldHealth()` instead of accessing `GameGlobalsManager.BedouinDemolisherShieldHealth` directly
       - **Updated docs**: `GameUnitManagerAPI.md` — added `GetDefaultShieldHealth` and `SetDefaultShieldHealth`
       - **Updated docs**: `GameGlobalsManager.md` — added `StablesHorseRegenTickTarget`, `StablesHorsesCap`, `DiseaseDamage1/2/3`

2.0.0 - Session Hooks, Trade Prices, Fire/Heal Matrices, Stable Tracking (2026-02-26):

       - **SHCDE-SE 2.7 Compatibility** - Updated plugin to work with SHCDE-SE 2.7 (new modded game warning popup, updated API)

       - **New: Unit Fire Damage CSV** - `CrusaderDETweaker_UnitFireDamage.csv` controls per-unit fire damage values
       - **New: Bedouin Heal CSV** - `CrusaderDETweaker_BedouinHeal.csv` controls per-unit heal amounts from Bedouin Healer
       - **New: Building Fire Damage CSV** - `CrusaderDETweaker_BuildingFireDamage.csv` controls per-building fire damage
       - **New: Ballista Damage CSV** - `CrusaderDETweaker_BallistaDamage.csv` for ballista damage matrix
       - **New: BepInEx Fire/Heal Multipliers** - `FireAndHealMultipliersConfig` adds global multipliers for fire and heal values

       - **New: Trade Prices** - `[Trade Prices]` section in `CrusaderDETweaker_Globals.toml` allows overriding base buy/sell prices for all 20 tradeable goods using `GamePlayerManagerAPI.GetTradeBasePrice(eGoods, long)` (the setter overload — a typo in SHCDE-SE source, both get and set share the same method name)
       - **New: Globals session hooks** - `SetNoKnockdownWalls` and `SetAutoTrade` now correctly re-applied on every `OnStartMap` event; both are per-session API calls that the game resets on each new map load

       - **Fix: NoKnockdownWalls / Strong Walls** - Was previously called at library load time (before any session existed) and had no effect. Now applied via `MapLoaderR3EventHooks.OnStartMap` subscription
       - **Fix: Auto Trade** - Was called at library load time and had no effect. Now applied via `BuildingR3EventHooks.OnBuildingSpawn` when the local player's `STRUCT_TRADEPOST` spawns; skips AI players
       - **Fix: Map restriction flags** (`AllBuildingsAvailable`, `AllUnitsAllowed`, `AllTradeGoodsAllowed`, `AllProductionGoodsAllowed`) — Previously calling the API with `false` was actively disabling all map-defined restrictions. These flags are now no-ops when set to `false`; the API is only called when the value is `true`

       - **New: Stable tracking for RequiresHorse** - When a unit type has `RequiresHorse = true` in the TOML config, the plugin now hooks `UnitR3EventHooks.OnUnitCreate` to automatically link newly spawned units of that type to an available horse stable slot via `GameBuildingManagerAPI.SetStablesUnitIdLink`. This ensures the stable accurately tracks all configured cavalry units. Only applies to the local player; AI players are unaffected

       - **Config File Renames**: `CrusaderDETweaker.cfg` → `CrusaderDETweaker_GlobalMultipliers.cfg`; `CrusaderDETweaker_Globals.toml` → `CrusaderDETweaker_GameplaySettings.toml`; all 7 damage CSV files moved to `DamageMatrices\` subfolder (auto-created on first launch)

       - **New docs**: `docs/shcde-se/BuildingSpawnEventArgs.md` — documents `BuildingSpawnEventArgs` (`SHCDESE.EventAPI.Buildings`)
       - **Updated docs**: `GamePlayerManagerAPI.md` — added `GetTradeBasePrice(eGoods)` getter and `GetTradeBasePrice(eGoods, long)` setter (typo overload)
       - **Updated docs**: `GameBuildingManagerAPI.md` — added `SetStablesUnitIdLink` and `GetStablesUnitGlobalIdLink` methods

1.8.0 - SHCDE-SE Platform Update (2026-02-14):
       - **Compatibility** - Updated project to support SHCDE-SE v1.15.0
       - **API Update** - Fixed breaking change from removal of SHCDESE.Core.Interop namespace
       - **Stability** - Built against latest SDK for improved game global address discovery
       - **Performance** - Project now compatible with latest extender performance improvements

1.7.0 - Tag System Reconstruction (Semantic Compression):
       - **Semantic Tags** - Replaced unit-enum-based tags with semantic category tags (e.g., "Civilian", "Lord")
       - **Mathematical Compression** - Implemented equivalence class detection to group units with identical damage profiles
       - **Reduced Redundancy** - Semantic rules apply to entire groups, significantly reducing TOML file size
       - **Precision Alignment** - Aligned armor calculation logic between discovery engine and property handlers
       - **Initialization Order** - Fixed race condition by ensuring semantic tags are registered before discovery runs
       - **100% Accuracy Renewed** - Fixed 250+ mismatches caused by previous namespace disconnect
       - **Modular Architecture** - Refactored config systems into focused single-responsibility classes
1.1.1 - Bugfix for structure cost.
1.2.0 - Added additional BepInEx multipliers:
       - WallDamageTakenMultiplier: Multiplier for damage to walls
       - TowerDamageTakenMultiplier: Multiplier for damage to towers
       - WoodenStructureDamageTakenMultiplier: Multiplier for damage to wooden structures
       - Improved error handling and validation for all multipliers
       - Added structure type helper methods (IsWall, IsTower, IsWoodenStructure)
1.2.1 - Added wall cost modification support:
       - Walls (stone, crenel, wood) can now have their costs modified via TOML
       - Walls are cost-only structures (health and other properties cannot be modified)
       - Added CostOnlyStructures list to handle structures with limited modifiable properties
1.3.0 - Major update with new multipliers and improvements:
       - Added UnitRangedDamageTakenMultiplier: Global multiplier for all ranged damage (Arrow, Bolt, Slinger, Javelin)
       - Fixed WallDamageTakenMultiplier: Now properly detects and applies to walls using tile property flags
       - Replaced WoodenStructureDamageTakenMultiplier with CivilStructureDamageTakenMultiplier
       - Added wall cost multipliers: LowWallCostMultiplier and HighWallCostMultiplier (moved from TOML to BepInEx config)
       - Refactored BepInEx config system into modular structure (UnitMultipliersConfig, StructureMultipliersConfig, WallCostConfig)
       - **MAJOR REFACTORING: Damage Calculator** - Completely refactored using Strategy pattern:
         * Reduced main CalculateMeleeDamage method from ~500 lines to ~100 lines
         * Created 9 weapon-specific calculator classes (Sword, Mace, Lance, Axe, Dagger, Ranged, Unarmed, Beast, Polearm)
         * Extracted all constants to DamageConstants class
         * Added helper classes: WeaponVsArmorLookupTable, TagVsTagModifierHelper, SiegeDefenseHandler
         * Much easier to maintain, test, and extend
         * Maintained 100% test pass rate (all existing functionality preserved)
       - Improved structure categorization and detection
       - Better error handling and validation for all multipliers
       - Fixed Speed property: Now writes to config for all units (some units have API limitations - see known issues)
       - Known Issue: Some units (animals, special units) cannot have Speed modified due to SHCDE-SE API limitation
1.3.1 - Fixed verification system to work correctly with modified TOML configs:
       - **CRITICAL FIX: Verification System** - Verification now uses original game defaults instead of modified TOML values
       - Added original registry snapshot mechanism: Captures hardcoded defaults before TOML loads
       - Added CalculateMeleeDamageWithOriginalData(): Uses original defaults for verification calculations
       - Added CsvMatrixReader.CaptureOriginalDefaults(): Captures original damage values from game API before TOML loads
       - Verification now correctly validates calculation logic against original game defaults, regardless of user TOML modifications
       - This ensures verification always passes at 100% when calculation logic is correct, even if TOML files are modified

1.3.2 - Code quality improvements and documentation enhancements:
       - **Fixed all critical code issues** - ArmorCategory tag removal, exception handling, TODO comments
       - **Enhanced README** - Added "What is This?", "Quick Start", and "Configuration Methods" sections
       - **Added system status documentation** - Created SYSTEMS_STATUS.md to clarify active vs. development systems
       - **Improved code documentation** - Added clear documentation to development systems (AutomatedDamageSystem, ExceptionDetector, DamageMatrixSolver)
       - **Enhanced tag system documentation** - Improved UnitDamageData.cs with tag category explanations
       - **Better error handling** - All empty catch blocks now log exceptions at Debug level
       - **Cleaned up code** - Removed commented-out debug code, improved obsolete code documentation
       - **Project clarity improvements** - Better documentation for users and contributors

1.5.0 - Major Code Cleanup and Optimization:
       - **REMOVED: Tag System** - Tag-based damage modifiers completely removed
       - **REMOVED: Weapon/Armor Detection** - Category-based damage calculation removed
       - **REMOVED: Special Modifiers** - Unit-specific damage modifiers removed (unused dead code)
       - **REMOVED: Damage Calculator** - All damage calculation logic removed (CSV-only system)
       - **CSV-Only Damage System** - Damage values are now exclusively loaded from CSV matrices
       - **Performance Optimizations** - Optimized NonModable lookups (HashSet for O(1) performance)
       - **Code Cleanup** - Removed ~400+ lines of dead code, unused methods, and obsolete systems
       - **Documentation Updates** - Updated all documentation to reflect simplified architecture
       - **DLL Size Reduction** - Reduced from ~95KB to ~91KB through code removal
       - **Simplified Architecture** - Damage system is now purely CSV-based with no calculation logic

1.6.0 - Major Refactoring: Modular Architecture:
       - **Damage Matrix Refactoring** - Split large classes into focused components:
         * `OriginalDefaultsCapture.cs` - Orchestrates capturing all original game defaults
         * `MeleeDefaultsCapture.cs` - Specialized melee defaults capture
         * `RangedDefaultsCapture.cs` - Specialized ranged defaults capture
         * `EunuchAoeDefaultsCapture.cs` - Specialized AOE defaults capture
         * `CsvMatrixFallback.cs` - Fallback CSV loading if API fails
         * `MatrixApplicationLoop.cs` - Generic matrix application loop (extracted from MatrixLoader)
       - **TOML Config Refactoring** - Split large classes into focused components:
         * `TomlFormatter.cs` - TOML value formatting utilities
         * `TypeConverter.cs` - Type conversion utilities
         * `ValueValidator.cs` - Value validation helpers
         * `ErrorLogging.cs` - Standardized error logging (extracted from ConfigHelpers)
         * `ConfigFileHeaderWriter.cs` - File header generation (extracted from ConfigGenerator)
         * `EntityProcessor.cs` - Generic entity processing loop (extracted from ConfigLoader)
       - **Data/Systems Refactoring** - Split large classes into focused components:
         * `RegistrySnapshot.cs` - Registry snapshot management (extracted from UnitDamageRegistry)
         * `UnitRegistrationHelper.cs` - Shared unit registration logic
         * Specialized initializers in `Systems/Initializers/`:
           - `HeavyArmorUnitInitializer.cs`
           - `MediumArmorUnitInitializer.cs`
           - `LightArmorUnitInitializer.cs`
           - `UnarmoredUnitInitializer.cs`
           - `NoArmorUnitInitializer.cs`
           - `SiegeUnitInitializer.cs`
           - `BeastUnitInitializer.cs`
           - `EliteUnitInitializer.cs`
         * `StructureTypeHelpers.cs` - Structure type query helpers (extracted from StructureCategories)
       - **BepInEx Config Refactoring** - Split large classes into focused components:
         * `ConfigSystemInitializer.cs` - BepInEx config initialization (extracted from BepInExConfigManager)
         * Event handlers in `Config/BepInEx/Systems/Handlers/`:
           - `MeleeDamageMultiplierHandler.cs` - Melee damage multiplier events
           - `RangedDamageMultiplierHandler.cs` - Ranged damage multiplier events
           - `HealthMultiplierHandler.cs` - Health multiplier events
           - `StructureDamageMultiplierHandler.cs` - Structure damage multiplier events
           - `StructureTypeDetector.cs` - Structure type detection
       - **Code Quality Improvements**:
         * Reduced class sizes significantly (e.g., ConfigHelpers from 311 to 47 lines)
         * Improved single responsibility principle adherence
         * Better modularity and maintainability
         * Cleaner log output (removed excessive debug logging)
       - **Documentation Updates** - Updated all documentation to reflect new modular architecture