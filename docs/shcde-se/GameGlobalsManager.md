# GameGlobalsManager

**Namespace:** `SHCDESE.GameGlobals`
**Assembly:** `SHCDESE.dll`
**Version documented:** 1.23.1

Provides access to global game constants and session-wide settings. Modifications here typically affect all relevant units/buildings immediately or upon their next spawn.

```csharp
public sealed class GameGlobalsManager
```

## Remarks

The `GameGlobalsManager` is the preferred way to modify unit/building defaults that are shared across the entire game session. Unlike the per-instance APIs in `GameUnitManagerAPI`, changes made here do not require scanning for live units or hooking spawn events.

---

## Properties

### `Instance`

Gets the singleton instance.

```csharp
public static GameGlobalsManager Instance { get; }
```

---

## Methods

### `GetManagedAssemblyStateManager`

```csharp
public ManagedAssemblyStateManager GetManagedAssemblyStateManager()
```

**Returns:** `ManagedAssemblyStateManager`

---

## Fields — Damage Tables (Raw RVAs)

| Field | Type |
|-------|------|
| `MeleeDamageTableRVA` | `ulong` |
| `MeleeEunuchAOEDamageTableRVA` | `ulong` |
| `RangedArrowDamageTableRVA` | `ulong` |
| `RangedBoltDamageTableRVA` | `ulong` |
| `RangedJavelinDamageTableRVA` | `ulong` |
| `RangedSlingerDamageTableRVA` | `ulong` |

---

## Fields — Unit & Building Tables (Raw RVAs)

| Field | Type |
|-------|------|
| `UnitHealthTableRVA` | `ulong` |
| `BuildingHealthTableRVA` | `ulong` |
| `BuildingHousingPopulatonSpaceTableRVA` | `ulong` |
| `SpeedTableRVA` | `ulong` |
| `UnitEUGoldCostTableRVA` | `ulong` |
| `UnitEUGoodTypeCostsTableRVA` | `ulong` |

---

## Fields — Building Costs (Raw RVAs/VAs)

| Field | Type |
|-------|------|
| `BuildingDefaultCostsTableVA` | `ulong` |
| `BuildingDefaultCostsTableEndVA` | `ulong` |
| `BuildingGoldCostsTableRVA` | `ulong` |
| `BuildingIronIngotsCostsTableRVA` | `ulong` |
| `BuildingRawPitchCostsTableRVA` | `ulong` |
| `BuildingStoneCostsTableRVA` | `ulong` |
| `BuildingWoodCostsTableRVA` | `ulong` |
| `BuildingAvailabilityManager` | `ulong` |

---

## Fields — Shield Health Constants

### `BedouinDemolisherShieldHealth`

Controls the default shield health for Bedouin Demolisher units. `ManagedAssemblyImmediate<ushort>` directly patches game memory.

```csharp
public ManagedAssemblyImmediate<ushort>? BedouinDemolisherShieldHealth
```

- **Default:** 40000
- `GetValue()` — Returns the current global shield health.
- `SetValue(ushort value)` — Updates the global shield health for all units.

**Example:**
```csharp
GameGlobalsManager.Instance.BedouinDemolisherShieldHealth.SetValue(50000);
```

---

## Fields — AI

| Field | Type |
|-------|------|
| `AIAttackForceMaxUnits` | `ManagedAssemblyMultiImmediate<ushort>?` |
| `AIBoolPlayerListVA` | `ulong` |
| `AILineUpVA` | `ulong` |
| `AILordManagerRVA` | `ulong` |

---

## Fields — Unit Speed Bonuses

| Field | Type |
|-------|------|
| `ArabHorsemanRunSpeedBonus` | `ManagedAssemblyImmediate<ushort>?` |
| `BedouinCamelLancerRunSpeedBonus` | `ManagedAssemblyImmediate<ushort>?` |
| `BedouinHeavyCamelRunSpeedBonus` | `ManagedAssemblyImmediate<ushort>?` |
| `KnightRunSpeedBonus` | `ManagedAssemblyImmediate<ushort>?` |

---

## Fields — Assassin

| Field | Type |
|-------|------|
| `AssassinDetectionRange` | `ManagedAssemblyImmediate<ushort>?` |
| `AssassinTransparencyThreshold` | `ManagedAssemblyImmediate<ushort>?` |

---

## Fields — Ballista Damage

| Field | Type |
|-------|------|
| `BallistaDamageDefault` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToArabBallista` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToBallista` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToBatteringRamAndSiegeTower` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToCatapult` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToMangonel` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToPortableShields` | `ManagedAssemblyImmediate<ushort>?` |
| `BallistaDamageToTrebutchet` | `ManagedAssemblyImmediate<ushort>?` |

---

## Fields — Building Repair

| Field | Type |
|-------|------|
| `BuildingRepairProximityCheckRange` | `ManagedAssemblyImmediate<ushort>?` |
| `BuildingRepairProximityCheckExRange` | `ManagedAssemblyImmediate<ushort>?` |

---

## Fields — Building Limits

| Field | Type |
|-------|------|
| `MaxArmories` | `ManagedAssemblyImmediate<ushort>?` |
| `MaxGoodYards` | `ManagedAssemblyImmediate<ushort>?` |
| `MaxGranaries` | `ManagedAssemblyImmediate<ushort>?` |
| `MaxRandomLions` | `ManagedAssemblyImmediate<uint>?` |

---

## Fields — Catapult

| Field | Type | Notes |
|-------|------|-------|
| `CatapultInitialStoneAmount` | `ManagedAssemblyImmediate<byte>?` | Stone loaded when a catapult first spawns |
| `CatapultRestockStoneAmount` | `ManagedAssemblyImmediate<ushort>?` | Stone received per restock |
| `CatapultRestockStoneCost` | `ManagedAssemblyImmediate<ushort>?` | Stone consumed from stockpile per restock |

---

## Fields — Disease Damage

Damage values used in `c_game_unit_takedamage_projectile`. Three tiers applied depending on disease severity.

| Field | Type | Default |
|-------|------|---------|
| `DiseaseDamage1` | `ManagedAssemblyImmediate<int>?` | 150 |
| `DiseaseDamage2` | `ManagedAssemblyImmediate<int>?` | 200 |
| `DiseaseDamage3` | `ManagedAssemblyImmediate<int>?` | 400 |

---

## Fields — Food Consumption

| Field | Type | Notes |
|-------|------|-------|
| `FoodConsumptionTickThreshold` | `ManagedAssemblyMultiImmediate<int>?` | How often the food tick fires. Higher = slower eating. |

---

## Fields — Stables

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `StablesHorseRegenTickTarget` | `ManagedAssemblyImmediate<short>?` | 550 | Ticks before a horse charge regenerates. Lower = faster. Referenced in `c_game_horse_stable_update`. |
| `StablesHorsesCap` | `ManagedAssemblyImmediate<sbyte>?` | 4 | Maximum horses tracked by the stable. **WARNING: values above 4 break horse-link tracking** — used horses will re-charge instead of being properly tracked. |

---

## Fields — Gatehouse

| Field | Type |
|-------|------|
| `GateHouseCloseDistance` | `ManagedAssemblyImmediate<ushort>?` |
| `GateHouseReOpenDistance` | `ManagedAssemblyImmediate<ushort>?` |
| `GateHouseUnkDistance` | `ManagedAssemblyImmediate<ushort>?` |
| `GameGatehouseFunctionsVA` | `ulong` |
| `GameGatehouseFunctionsVTable` | `NativePointer<GatehouseFunctionsVTable>` |
| `GameGatehouseManagerRVA` | `ulong` |

---

## Fields — Gold Costs (Unit Recruitment)

| Field | Type |
|-------|------|
| `GoldCostDefault` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostDefaultUnknown` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostEngineer` | `ManagedAssemblyImmediate<short>?` |
| `GoldCostLadderman` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostMonk` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostTunneler` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabAssassin` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabBow` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabGrenadier` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabSlave` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabSlinger` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostArabhorsemanOrSwordsmanOrDemolisher` | `ManagedAssemblyImmediate<short>?` |
| `GoldCostBedouinAmbusher` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinCamelLancer` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinEunuch` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinHealer` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinHeavyCamel` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinSapper` | `ManagedAssemblyDisplacement<short>?` |
| `GoldCostBedouinSkirmisher` | `ManagedAssemblyDisplacement<short>?` |

---

## Fields — Date/Time

| Field | Type |
|-------|------|
| `DateTimeCurrentDayRVA` | `ManagedAssemblyDisplacement<uint>?` |
| `DateTimeCurrentMonthRVA` | `ManagedAssemblyDisplacement<uint>?` |
| `DateTimeCurrentYearRVA` | `ManagedAssemblyDisplacement<uint>?` |
| `DateTimeDaysInMonthRVA` | `ManagedAssemblyImmediate<short>?` |
| `DateTimeMonthsInYearRVA` | `ManagedAssemblyImmediate<short>?` |

---

## Fields — Ox / Stone Economy

| Field | Type |
|-------|------|
| `OxStoneReceiveAmount` | `ManagedAssemblyDisplacement<ushort>?` |
| `OxStoneRequiredAmount` | `ManagedAssemblyMultiImmediate<ushort>?` |

---

## Fields — Peasant Spawning

| Field | Type | Notes |
|-------|------|---------|
| `PeasantRespawnTickTargetValue` | `ManagedAssemblyMultiImmediate<ushort>?` | Ticks before a new peasant spawns. Lower = faster. **Configurable via `[Peasant Spawning]` in Globals TOML.** |
| `PeasantRespawnTickResetValue` | `ManagedAssemblyMultiImmediate<ushort>?` | Tick counter reset value after a spawn. **Configurable via `[Peasant Spawning]` in Globals TOML.** |
| `PeasantSpawnRateIncrementsDefaultsRVA` | `ulong` | Raw table — default population bracket (RVA, not directly settable) |
| `PeasantSpawnRateIncrementsHighPopRVA` | `ulong` | Raw table — high population bracket (RVA, not directly settable) |
| `PeasantSpawnRateIncrementsLowPopRVA` | `ulong` | Raw table — low population bracket (RVA, not directly settable) |

---

## Fields — Pathfinding

| Field | Type |
|-------|------|
| `PathfindingMaxTilesConstraint` | `ManagedAssemblyImmediate<ushort>?` |

---

## Fields — Rabbit

| Field | Type |
|-------|------|
| `RabbitDespawnTickTime` | `ManagedAssemblyImmediate<short>?` |

---

## Fields — Walls & Proximity

| Field | Type |
|-------|------|
| `FriendlyWallToEnemyWallProximityDistAllowance` | `ManagedAssemblyImmediate<uint>?` |
| `NoKnockdownWallsVA` | `ulong` |

---

## Fields — Player & Skirmish

| Field | Type |
|-------|------|
| `LocalPlayerIdVA` | `ulong` |
| `LocalPlayerArmyCountsVA` | `ulong` |
| `PlayerDefaultSkirmishResourcesVA` | `ulong` |
| `PlayerDefaultSkirmishSettingsTableRVA` | `ulong` |
| `PlayerDefaultSkirmishSpawnGoldTable` | `ulong` |
| `PlayerExtremePowersEnabledVA` | `ulong` |
| `PlayerKeepIsEnclosedVA` | `ulong` |

---

## Fields — Game Managers (Virtual Addresses)

| Field | Type |
|-------|------|
| `ChoreManagerVA` | `ulong` |
| `GameBuildingManagerVA` | `ulong` |
| `GameCursorManagerVA` | `ulong` |
| `GamePlayerManagerVA` | `ulong` |
| `GameProjectilesManagerVA` | `ulong` |
| `GameSoundManagerVA` | `ulong` |
| `GameTileManagerVA` | `ulong` |
| `GameTribeManagerVA` | `ulong` |
| `GameUnitManagerVA` | `ulong` |
| `GameVegetationManagerVA` | `ulong` |

---

## Fields — Map & Rules

| Field | Type |
|-------|------|
| `CurrentMapNameVA` | `ulong` |
| `CurrentMapNameLengthVA` | `ulong` |
| `CurrentMapSizeVA` | `ulong` |
| `MapColumnLookupTableRVA` | `ulong` |
| `MapRowLookupTableRVA` | `ulong` |
| `MapRulesInfo_AllowedArabUnitsVA` | `ulong` |
| `MapRulesInfo_AllowedBedouinUnitsVA` | `ulong` |
| `MapRulesInfo_AllowedEuroUnitsVA` | `ulong` |
| `MapRulesInfo_AllowedProductionGoodsVA` | `ulong` |
| `MapRulesInfo_AllowedTradingGoodsRVA` | `ulong` |

---

## Fields — Speed & Unit Tables

| Field | Type |
|-------|------|
| `SpeedTableRVA` | `ulong` |
| `UnitEUGoldCostTableRVA` | `ulong` |
| `UnitEUGoodTypeCostsTableRVA` | `ulong` |
| `UnitHealthTableRVA` | `ulong` |

---

## Fields — Miscellaneous

| Field | Type | Notes |
|-------|------|-------|
| `CurrentContextAttackingUnitIdVA` | `ulong` | VA pointing to the attacking unit ID in damage hooks |
| `CurrentContextMapperValueVA` | `ulong` | |
| `CurrentContextUnitValueVA` | `ulong` | Points to the currently processed Unit ID |
| `CurrentlySelectedBuildingIdVA` | `ulong` | |
| `EngineerAvailableVA` | `ulong` | |
| `GamePausedVA` | `ulong` | |
| `GlobalAdvancedOptionsVA` | `ulong` | |
| `GlobalIdsUsedVA` | `ulong` | |
| `GlobalImprovedSiegingBehaviourVA` | `ulong` | |
| `IsInMapEditorVA` | `ulong` | |
| `LaddermanAvailableVA` | `ulong` | |
| `MonkAvailableVA` | `ulong` | |
| `p_PathfindingContextVA` | `ulong` | |
| `TeamsListRVA` | `ulong` | |
| `TreeGrowthProgressionTableRVA` | `ulong` | |
| `TreeProximityAreaLevelTableRVA` | `ulong` | |

---

## Fields — Static

### `LastMessageFromCharacterCached`

Contains the player ID that sent the last in-game "message". Mostly used for AI-related lookups.

```csharp
public static byte LastMessageFromCharacterCached
```
