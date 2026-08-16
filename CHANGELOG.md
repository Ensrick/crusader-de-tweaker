Unreleased (2.7.0) - Multiplayer config sync (in development, NOT shipped):

       - **New: automatic host config sync in multiplayer.** The mod registers a "Unit Stat Editor" tab in SHCDE-SE's lobby Mod Settings hub (SE 1.41.0+). When the host has "Sync my configs to all players" enabled (default on), every joining player automatically receives and applies the host's unit stats, structure stats and all 7 damage matrices for that session - no more manual config file sharing. Built on SE's [SyncHostOnly] lobby settings sync (verified sender identity, automatic push to late joiners); the ~130 KB config set travels as one gzipped ~15-20 KB blob, well under the transport's 512 KB ceiling. Clients re-apply their own configs when the session ends; values the host overrode that a client's files leave at the -1 sentinel keep the host's values until the game restarts (logged as a warning). NOT synced in v1: the real-time multipliers cfg (`CrusaderDETweaker_GlobalMultipliers.cfg`) - clients keep their own.
       - New files: `Config/Sync/ConfigSyncManager.cs`, `Config/Sync/ConfigSyncLobbySettings.cs`, `Override/ScriptExtenderUI/CDTLobbySettings.xaml`; `ConfigPaths`/`MatrixPaths` gained a session override directory that redirects all config reads to the host's synced files. UNTESTED in a real multiplayer session as of 2026-08-13.

2.6.1 - Rebuilt for game patch 2.8.0.1 / SHCDE-SE 1.41.0, gatehouse write disabled (2026-08-15):

       - **Fix: crash when starting a game after the 2026-08-11 game update.** Two independent causes, both addressed:
         1. The game patched to 2.8.0.1, which broke Script Extender 1.40.0's native bindings. This build is compiled against SHCDE-SE 1.41.0 (built for 2.8.0.1) and declares `SupportedGameVersions: 2.8.0.1`. **Requires Script Extender 1.41.0** - update it before updating this mod (currently available from the SE GitLab releases page; Nexus/Workshop copies of SE may lag behind).
         2. Even on the matched SE 1.41.0 stack, applying the `[Gatehouse]` globals at session start crashed the game (per-write trace pinpointed `GateHouseCloseDistance.SetValue`; it is an SE assembly-immediate patch into `c_game_gatehouse_handler`, which 2.8.0.1 recompiled). Since generated configs bake a value for every `[Gatehouse]` key, **every user crashed at every skirmish start**. The two gatehouse writes are now skipped; a user-authored override logs a warning that it is ignored until the Script Extender ships a fix. All other globals (siege, stealth, stables, pathfinding, food, disease) still apply.
       - Session-start global writes now log each write by name (debug logging on), so any future patch-break names its culprit in the log.
       - SE 1.41.0's `SetStablesUnitIdLink` is now bidirectional upstream; the mod's single call site recompiles cleanly against the new signature.

2.6.0 - Health multiplier + stable-linking reach recruited units, structure cost 0-override preserved, dead-code overhaul (2026-08-11):

       - **Fix: `UnitHealthMultiplier` now applies to recruited and transitioned units.** The multiplier was applied only from `OnUnitCreate`, which does not fire for units recruited at the Barracks / Mercenary Post, assigned as workers, or disbanded back to peasants — those paths raise `OnUnitTransition` (SHCDE-SE 1.35+) instead. A new one-frame deferral bridge (`Config/Core/UnitTransitionDispatcher.cs`) re-raises each transition from the plugin's Unity `Update` loop, after the native transformation has completed and the unit already carries its new type's template health, and the handler scales `SetMaxHealth`/`SetCurrentHealth` once. No compounding: the game resets a unit's health from the template during the transformation, so the deferred write scales the fresh template value exactly once. Incidentally this makes the multiplier reach AI troops recruited mid-match, which the spawn-only path never could.
       - **Fix: `RequiresHorse` units recruited mid-match now link to a stable slot.** The stable-linking hook in `ConfigLoader.RegisterSessionHooks` was also `OnUnitCreate`-only, so a horse-requiring unit recruited from the Barracks / Mercenary Post was never linked to the local player's stable. The link logic is now factored into `TryLinkUnitToStable` and driven from both `OnUnitCreate` (spawns) and the deferred `OnUnitTransition` path (recruits).
       - **Fix: a structure cost explicitly set to `0` is no longer silently deleted on migration.** `StructureCostPropertyBase.ShouldOmitFromGeneration` returned `true` for `value == 0`, so a user's intentional `GoldCost = 0` (a free building) was stripped from the file on the next config regeneration. Only the `-1` sentinel means "leave the game value unchanged"; `0` is a legitimate override and is now preserved. The now-unused `ShouldOmitFromGeneration` override point was removed from `PropertyHandler`. (Auto-generation still omits properties whose game default is 0 via the `value > 0` gate in `TryGetFromAPI`; only a user-authored 0 is affected.)
       - **Internal: professional-standards overhaul.** ~6,370 lines of dead / unreachable code removed across six strata (the old unit-tag damage system, the original-defaults capture subsystem, the deactivated formula-damage and armor-multiplier handlers, the empty projectile TOML leg, and the ConfigHelpers/TomlFormatter/ValueValidator shim), 126 → 92 source files, DLL ~241 KB → ~161 KB, build clean with 0 warnings. Damage remains CSV-matrix only (rows = defenders, columns = attackers; `-1` = leave unchanged). Version is single-sourced in `PluginInfo.cs`. Rebuilt against SHCDE-SE 1.40.0 (game v2.8).

2.5.0 - Recruit-time unit caps, building-cap count fix, EnemyHealthModifier removed, DiseaseDamageMultiplier fix (2026-06-16):

       - **Fix: building `MaxCount` no longer locks out placement over time.** The cap's count of existing buildings was querying *every* `AliveState` (`GetAllBuildings(..., null, ...)`), so buildings pending deletion (bulldozed / destroyed / replaced — cleared by the engine only "shortly" later) and not-yet-initialized ones kept being counted. Over a match these dead entries piled up and pushed the count permanently past the cap, e.g. a woodcutter capped at 3 became unplaceable after ~1 minute of normal play. The count now filters to `AliveState.IsAlive` and does the owner filter inside the query (`PlayerRelationship.Self`), which also drops the old per-id `GetOwner` resolve. (This was *not* AI buildings being counted — AI structures carry a different owner and were already excluded.)
       - **Fix: `DiseaseDamageMultiplier` now actually takes effect.** It was applied once at plugin init, then immediately overwritten by the per-map-load `[Disease]` TOML write, so it never changed in-game disease damage. It now applies inside `LoadGameGlobals` on top of the `[Disease]` base on every map load. This also removes a latent init-time game-API write (an `ACCESS_VIOLATION` hazard) that ran whenever the multiplier was set to a non-default value.
       - **Fix: unit `MaxCount` is now enforced for recruited units.** Previously the unit cap relied solely on `OnUnitCreate`, which does **not** fire when a unit is recruited from the Barracks / Mercenary Post (recruiting transforms a peasant into a soldier rather than spawning a fresh unit), so recruited troops ignored the cap entirely. A new MonoMod detour on `EngineInterface.GameAction(MakeTroop)` now refuses an over-cap recruit before it is queued (no gold spent) and trims a batch/"recruit max" request down to the remaining room. `MaxCount = 0` now means a unit genuinely cannot be recruited. The existing `OnUnitCreate` removal path is kept as a fallback for the spawns that do fire it. Counting is live; rapid recruit clicks are guarded by short-lived reservations so they cannot momentarily overshoot the cap. (Requires the game assembly + MonoMod references, both already present at runtime.)
       - **Removed: `EnemyHealthModifier` gameplay option (added in 2.4.0).** It wrote the game's "Enemy Health" value into the skirmish **setup/lobby** block (`ChoreManagerOptions.AdvOpt_EnemyHPS`), which the engine consumes once when it *builds* the session. The mod applied it at `OnStartMap`, i.e. after the session was already built, so it never affected live AI unit health — and its read-back "confirmation" was a false positive (it read back the same field it had just written). It is removed rather than reworked because the only reliable replacement (per-unit HP scaling on spawn) cannot reach AI units recruited mid-match, which is exactly the case it was meant to cover. The key is dropped from generated configs; an orphan `EnemyHealthModifier = ...` line in an existing config is harmless and is removed on the next config regeneration.

2.4.1 - Silence MaxCount warning spam, plugin-only release zip (2026-06-12):

       - **Fix: `Unknown property 'MaxCount'` no longer warned for every unit and structure on launch.** `MaxCount` is read by the dedicated cap loaders (`ConfigLoader.LoadUnitCaps` / `LoadBuildingCaps`), not by the per-property registry, so the generic property loop flagged it as unknown — ~161 warning lines per launch with a full config. Caps themselves always worked; the warnings were cosmetic. The generic loop now recognizes externally-handled TOML keys and logs them at Debug level instead.
       - **Change: the release zip no longer includes config files.** Previous zips carried freshly-generated pristine configs, so upgrading by extracting the zip over an existing install silently overwrote personalized configs with defaults. Configs are generated on first launch and migrated in place on every update, so the zip now ships only `BepInEx/plugins/CrusaderDETweaker/`. Release pipeline simplified accordingly (`release.ps1` is now build → backup → package; the reset/launch/restore steps are retired).

2.4.0 - AI siege behaviour toggles, Enemy Health option, advanced-options master flags (2026-06-11):

       - **New: `GlobalImprovedSiegeBehaviour` / `GlobalMoreAggressiveSiegeBehaviour`** — the game's AI siege behaviour toggles are now available in `["Gameplay Options"]`. Same semantics as every other gameplay option: only `true` overrides, `false` leaves the in-game setting untouched. Applied on each map load with read-back confirmation. (These write dedicated native globals via SHCDE-SE's `SetGlobalImprovedSiegeBehaviour` / `SetGlobalMoreAggressiveSiegeBehaviour` — separate plumbing from the `ChoreManagerOptions`-backed options.)
       - **New: `EnemyHealthModifier`** — the game's "Enemy Health" advanced option as a config value: `-1` = don't override (default), `0` = Weak (66%), `1` = Normal (100%), `2` = Strong (125%), `3` = Very Strong (150%). This is the vanilla lever for scaling enemy (AI) troop health *only* — unlike the per-unit `Health` properties, which apply to a unit type on every side. First enum-valued gameplay option (the rest are booleans); out-of-range values are logged and ignored.
       - **New: `AdvancedOptionsEnabled` / `AdvancedSkirmishOptionsEnabled`** — the master switches behind the game's advanced-options panels are now exposed, and both are **auto-raised whenever any advanced sub-option is overridden** (`BetterHealers`, `FasterPeasants`, `ImprovedArabSwordsman`, `ImprovedFletchers`, `ImprovedLadderman`, `ImprovedSpearman`, `NerfEunuchs`, `RebalancedHorseArchers`, `UncappedPeasants`, `EnemyHealthModifier`). Whether the game honors a sub-option while its master is off is undocumented, so the masters are raised as insurance; setting them to `true` explicitly also works on its own.

2.3.1 - Building caps block placement instead of demolishing after build (2026-06-09):

       - **Change: building count caps (`MaxCount`) are enforced at placement now, before the building exists.** Previously a capped/disabled building was placed, paid for, then removed on the next frame (`OnBuildingSpawn` + `DeleteBuildingSafe`) — you lost the resources and saw the building flicker in and out. The cap now hooks `OnPlacementValidation` (Pre) and refuses the placement up front, exactly like the game's own footprint/terrain rules: no resources spent, no flicker. `MaxCount = 0` (disabled) blocks every placement of that type; `MaxCount = N` blocks the (N+1)th. The placement event's `eMappers` is mapped to `eStructs` via SHCDE-SE's `ConvertToEStructs`.
       - **Side effect (improvement):** loading a save that has more of a building than the current cap no longer bulldozes the extras — placement-blocking only prevents building *new* ones, it never removes what already stands. (The old remove-on-spawn path could cull buildings as a save reloaded them.)
       - **Units are unchanged:** SHCDE-SE exposes no pre-recruit validation hook (only `OnPlacementValidation`, which is buildings-only), so unit `MaxCount` still removes the unit the moment it spawns — a disabled/over-cap unit still costs gold for the attempt. A true unit-level block needs an upstream SHCDE-SE hook (or the map-rules menu-disable API).

2.3.0 - Stat properties default to "use game default" + old-config MaxCount migration fix (2026-06-07):

       - **Fix: old configs no longer disable every unit/building.** Configs generated by 2.2.0 used `MaxCount = 0` to mean "no limit". 2.2.1 changed `0` to mean "disabled" (removed on spawn) and added a migration — but that migration only recognized files containing the literal `(no cap)` marker. Real 2.2.0 configs (which use `# Default: 0` and a `0 = no limit` header, with no `(no cap)` string) were skipped, so on first launch under 2.2.1+ every `MaxCount = 0` was read as "disabled" and **every unit and building of the local player was deleted on spawn**. Detection is now robust: any config lacking the current `-1 = unlimited` marker is treated as old-scheme and its `MaxCount = 0` entries migrate to `-1` (unlimited). An intentional `0 = disabled` set in a new-format file is preserved (that file has the marker).
       - **Change: unit & structure stat properties now default to `-1` = "use the game default".** Previously every property was generated with the game value baked in (e.g. `Health = 2500 # Default: 2500`), so the mod re-applied that value to every stat on load — even ones you never changed. Now an untouched property reads `Health = -1 # Default: 2500`: `-1` means "leave it alone", so the mod only writes the stats you actually override. Set any real number to override; the live game value is still shown in the `# Default:` comment, refreshed every launch. This stops the mod from silently overriding values you didn't intend and improves compatibility with other mods that adjust the same stats.
       - **Migration:** existing `Units` / `Structures` configs convert automatically on launch — a value equal to its `# Default:` becomes `-1`, while any genuine override is preserved exactly. A pre-change backup is kept in `BepInEx/config/CrusaderDETweaker/Backups/pre-noop-sentinel/`.
       - **Scope:** applies to Health, Speed, GoldCost, ShieldHealth, armor multipliers, structure health/costs, housing space and run-speed bonuses. `WeaponType` / `ArmorType` / `RequiresHorse` are written directly as before. `MaxCount` keeps its own convention (`-1` = unlimited). Trade Prices already used `0` = no override. The Globals file (siege/stealth/stables/etc.) is unchanged for now.
       - **Internal:** the `-1` sentinel is matched on the raw TOML value before the typed cast (so it works for `uint`/`ushort` stats too); skip-on-sentinel lives in `PropertyHandlerWrapper.TryLoadFromObject`; migration + sentinel generation in `PropertyHandler.TryGenerateCore`. Added migration unit tests; the float (armor-multiplier) comparison uses an epsilon so display-vs-API rounding never leaves a phantom override.

2.2.1 - MaxCount enforcement fix + disable support (2026-06-05):

       - **Fix: MaxCount count caps were never enforced** — The unit/building cap subscriptions were registered during `GlobalConfigSystem.Load()`, which runs *before* the Unit/Structure systems populate the cap dictionaries. With the dictionaries still empty, both `UnitCapHandler.Subscribe` and `BuildingCapHandler.Subscribe` hit their `if (caps.Count == 0) return;` guard and never subscribed to `OnUnitCreate` / `OnBuildingSpawn`, so caps did nothing. Dropped the empty-count short-circuit: the subscriptions now always register and read the (by-reference, mutated-in-place) dictionaries live at spawn time. Cap values from `2.2.0` configs now actually apply.
       - **Change: MaxCount semantics** — `-1` = unlimited (the new default), `0` = disabled (the type is removed the moment it spawns/places, so it cannot be fielded), `>0` = max alive/placed at once. Previously `0` meant "no cap." Existing configs migrate automatically on launch: any `MaxCount = 0` written by `2.2.0` (detected via the old `# Default: 0 (no cap)` comment) is rewritten to `-1`, so nothing is silently disabled. After migration, `0` is honored as "disabled." Set `MaxCount = 0` on any unit or building to disable it in every game mode.
       - **Note:** "Disable" uses the existing remove-on-spawn mechanism, so a disabled unit still appears recruitable and costs gold for the attempt — it just never survives. (A true menu-level disable via the map-rules API is a possible future enhancement.)

2.2.0 - Unit & building count caps, CampPeasantsCap, SHCDE-SE API fixes (2026-04-24):

       - **New: Per-unit-type count caps** — `MaxCount` property added to each unit section in `CrusaderDETweaker_Units.toml`. Set the maximum number of a given unit type the local player can have alive at once (e.g., `MaxCount = 20` under `[CHIMP_TYPE_KNIGHT]`). When the cap is exceeded, newly spawned units are immediately removed. Set to 0 to disable (default). Requires game restart.
       - **New: Per-building-type count caps** — `MaxCount` property added to each building section in `CrusaderDETweaker_Structures.toml`. Same behavior as unit caps: limits how many of each building type the local player can place. Set to 0 to disable (default). Requires game restart.
       - **New: CampPeasantsCap** — Added to `[Peasant Spawning]` section in `CrusaderDETweaker_GameplaySettings.toml`. Controls the maximum number of peasants that can wait at the campfire (default: 24). Set to 0 for no cap.
       - **Fix: SHCDE-SE v1.28.0 API compatibility** — Updated `LibraryLoaded` delegate subscription to match the new two-parameter signature `(IntPtr, ReadOnlySpan<byte>)`. Updated `GetCurrentContextAttackingUnitId()` call to renamed `GetCurrentContextUnitId()`.

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

1.3.2 - Code quality improvements and documentation enhancements:
       - **Fixed all critical code issues** - ArmorCategory tag removal, exception handling, TODO comments
       - **Enhanced README** - Added "What is This?", "Quick Start", and "Configuration Methods" sections
       - **Added system status documentation** - Created SYSTEMS_STATUS.md to clarify active vs. development systems
       - **Improved code documentation** - Added clear documentation to development systems (AutomatedDamageSystem, ExceptionDetector, DamageMatrixSolver)
       - **Enhanced tag system documentation** - Improved UnitDamageData.cs with tag category explanations
       - **Better error handling** - All empty catch blocks now log exceptions at Debug level
       - **Cleaned up code** - Removed commented-out debug code, improved obsolete code documentation
       - **Project clarity improvements** - Better documentation for users and contributors

1.3.1 - Fixed verification system to work correctly with modified TOML configs:
       - **CRITICAL FIX: Verification System** - Verification now uses original game defaults instead of modified TOML values
       - Added original registry snapshot mechanism: Captures hardcoded defaults before TOML loads
       - Added CalculateMeleeDamageWithOriginalData(): Uses original defaults for verification calculations
       - Verification now correctly validates calculation logic against original game defaults, regardless of user TOML modifications
       - This ensures verification always passes at 100% when calculation logic is correct, even if TOML files are modified

1.3.0 - Major update with new multipliers and improvements:
       - Added UnitRangedDamageTakenMultiplier: Global multiplier for all ranged damage (Arrow, Bolt, Slinger, Javelin)
       - Fixed WallDamageTakenMultiplier: Now properly detects and applies to walls using tile property flags
       - Replaced WoodenStructureDamageTakenMultiplier with CivilStructureDamageTakenMultiplier
       - Added wall cost multipliers: LowWallCostMultiplier and HighWallCostMultiplier (moved from TOML to BepInEx config)
       - Refactored BepInEx config system into modular structure (UnitMultipliersConfig, StructureMultipliersConfig, WallCostConfig)
       - **MAJOR REFACTORING: Damage Calculator** - Completely refactored using Strategy pattern
       - Improved structure categorization and detection
       - Better error handling and validation for all multipliers

1.2.1 - Added wall cost modification support:
       - Walls (stone, crenel, wood) can now have their costs modified via TOML
       - Walls are cost-only structures (health and other properties cannot be modified)
       - Added CostOnlyStructures list to handle structures with limited modifiable properties

1.2.0 - Added additional BepInEx multipliers:
       - WallDamageTakenMultiplier: Multiplier for damage to walls
       - TowerDamageTakenMultiplier: Multiplier for damage to towers
       - WoodenStructureDamageTakenMultiplier: Multiplier for damage to wooden structures
       - Improved error handling and validation for all multipliers
       - Added structure type helper methods (IsWall, IsTower, IsWoodenStructure)

1.1.1 - Bugfix for structure cost.