# GameBuildingManagerAPI

**Namespace:** `SHCDESE.API`
**Assembly:** `SHCDESE.dll`

Provides a high-level API for interacting with game buildings.

```csharp
public sealed class GameBuildingManagerAPI
```

## Remarks

This class is a singleton that serves as the primary entry point for querying and modifying buildings in the game world. It provides direct access to building properties, cost tables, and includes a high-performance query system for spatial and property-based searches.

---

## Properties

### `Instance`

Gets the singleton instance.

```csharp
public static GameBuildingManagerAPI Instance { get; }
```

---

## Methods — Building Creation & Deletion

### `Create`

Spawns a building structure in a raw manner.

```csharp
[LuaApiExport("Building_Create")]
public long Create(int playerId, int tileX, int tileY, short heightElevation, eStructs building, int buildingScale, int visualPlayerid, int spriteVariationIndex)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `playerId` | `int` | The ID of the player who will own the building. |
| `tileX` | `int` | The top-left tile X-coordinate for placement. |
| `tileY` | `int` | The top-left tile Y-coordinate for placement. |
| `heightElevation` | `short` | The height elevation for the building. |
| `building` | `eStructs` | The specific type of building to create. |
| `buildingScale` | `int` | The size scale parameter (e.g., woodcutter = 3 for 3x3). |
| `visualPlayerid` | `int` | The player ID that determines the building's color scheme. |
| `spriteVariationIndex` | `int` | The index for the building's sprite variation. |

**Returns:** `long` — The unique ID of the newly created building object.

> **Warning:** This is a low-level function that spawns only the core building object. It performs no placement checks, does not deduct resources, and does not create associated sub-components. Prefer `CreatePrefab` for normal use.

---

### `CreatePrefab`

Creates a complete building as if placed by a player, including all associated components and resource costs.

```csharp
[LuaApiExport("Building_CreatePrefab")]
public long CreatePrefab(int playerId, int tileX, int tileY, eMappers mv, int buildingScale, int a7, bool bIsFree, bool bypassPlacementRules = false)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `playerId` | `int` | The ID of the player placing the building. |
| `tileX` | `int` | The top-left tile X-coordinate for placement. |
| `tileY` | `int` | The top-left tile Y-coordinate for placement. |
| `mv` | `eMappers` | The building type from the editor's object palette enumeration. |
| `buildingScale` | `int` | The size scale parameter (e.g., woodcutter = 3 for 3x3). |
| `a7` | `int` | An unknown integer parameter passed to the game's internal function. |
| `bIsFree` | `bool` | If `true`, the player will not be charged the resource cost. |
| `bypassPlacementRules` | `bool` | If `true`, no placement validation will be applied. |

**Returns:** `long` — A non-zero value typically indicates success.

> **Recommended.** Correctly handles resource deduction, placement validation, and creation of all required sub-structures (e.g., farm plots). To get the ID of a created object, set up a listener hook for `BuildingSpawnEventArgs`.

---

### `CreateWall`

Builds a wall for a player from begin coordinates to end coordinates.

```csharp
[LuaApiExport("Building_CreateWall")]
public void CreateWall(int playerId, int tileXBegin, int tileYBegin, int tileXEnd, int tileYEnd, eMappers mv, int a7 = 100)
```

---

### `Delete`

Immediately deletes a building object from the game.

```csharp
[LuaApiExport("Building_Delete")]
public void Delete(int buildingId)
```

> Use with caution. Prefer `DeleteBuildingSafe` where possible.

---

### `DeleteBuildingSafe`

Safely marks a building object for deletion by the game engine.

```csharp
[LuaApiExport("Building_DeleteSafe")]
public bool DeleteBuildingSafe(int buildingId)
```

**Returns:** `true` if the building was found and marked for deletion; otherwise `false`.

> **Recommended.** Changes the building's state, allowing the game engine to clean it up gracefully on a subsequent frame.

---

### `Kill`

Instantly kills a building by setting its health to zero.

```csharp
[LuaApiExport("Building_Kill")]
public void Kill(int buildingId, int playerIdSource = 0)
```

---

## Methods — Health

### `GetDefaultHealth` / `SetDefaultHealth`

Gets or sets the default (maximum) health for a specific building type.

```csharp
[LuaApiExport("Building_GetDefaultHealth")]
public int GetDefaultHealth(eStructs building)

[LuaApiExport("Building_SetDefaultHealth")]
public void SetDefaultHealth(eStructs building, uint value)
```

---

### `GetHealth` / `SetHealth`

Gets or sets the current health of a building instance.

```csharp
[LuaApiExport("Building_GetCurrentHealth")]
public int GetHealth(int buildingId)

[LuaApiExport("Building_SetCurrentHealth")]
public void SetHealth(int buildingId, short health)
```

**Returns:** The current health value, or `-1` if the building is not found.

---

### `GetMaxHealth` / `SetMaxHealth`

Gets or sets the max health of a building instance.

```csharp
[LuaApiExport("Building_GetMaxHealth")]
public int GetMaxHealth(int buildingId)

[LuaApiExport("Building_SetMaxHealth")]
public void SetMaxHealth(int buildingId, ushort health)
```

**Returns:** The max health value, or `-1` if the building is not found.

---

### `Damage`

Reduces a building's health by a raw value.

```csharp
[LuaApiExport("Building_Damage")]
public void Damage(int buildingId, short damage, int playerIdSource = 0)
```

---

### `Repair`

Repairs a given building.

```csharp
[LuaApiExport("Building_Repair")]
public void Repair(int playerId, int buildingId, int woodCost, int stoneCost, int buildingGlobalId)
```

---

## Methods — Default Costs

### `GetDefaultCost` / `SetDefaultCost`

Gets or sets the default construction costs for a building type from the game's core data.

```csharp
[LuaApiExport("Building_GetDefaultCost")]
public BuildingCost GetDefaultCost(eStructs building)

public void SetDefaultCost(eStructs building, BuildingCost newCost, bool updateUnityEngineSite = true)
```

> **Warning:** This modifies global base data. Not suitable for per-map cost changes during gameplay. Use the individual cost setters below for in-map changes.

---

### Individual Cost Getters/Setters

```csharp
[LuaApiExport("Building_GetGoldCost")]
public int GetGoldCost(eStructs building)
[LuaApiExport("Building_SetGoldCost")]
public void SetGoldCost(eStructs building, int value)

[LuaApiExport("Building_GetWoodCost")]
public int GetWoodCost(eStructs building)
[LuaApiExport("Building_SetWoodCost")]
public void SetWoodCost(eStructs building, int value)

[LuaApiExport("Building_GetStoneCost")]
public int GetStoneCost(eStructs building)
[LuaApiExport("Building_SetStoneCost")]
public void SetStoneCost(eStructs building, int value)

[LuaApiExport("Building_GetIronCost")]
public int GetIronIngotCost(eStructs building)
[LuaApiExport("Building_SetIronCost")]
public void SetIronIngotCost(eStructs building, int value)

[LuaApiExport("Building_GetPitchCost")]
public int GetRawPitchCost(eStructs building)
[LuaApiExport("Building_SetPitchCost")]
public void SetRawPitchCost(eStructs building, int value)
```

---

### Wall Cost Multipliers

```csharp
[LuaApiExport("Building_GetLowWallCostMultiplier")]
public float GetLowWallCostMultiplier()

[LuaApiExport("Building_SetLowWallCostMultiplier")]
public void SetLowWallCostMultiplier(float mult)

[LuaApiExport("Building_GetHighWallCostMultiplier")]
public float GetHighWallCostMultiplier()

[LuaApiExport("Building_SetHighWallCostMultiplier")]
public void SetHighWallCostMultiplier(float mult)
```

Defaults: Low wall = `0.25`, High wall = `0.5`.

---

## Methods — Housing

### `GetDefaultHousingPopulationSpace` / `SetDefaultHousingPopulationSpace`

Gets or sets the default population space provided by a housing building.

```csharp
[LuaApiExport("Building_GetDefaultHousingPopulationSpace")]
public int GetDefaultHousingPopulationSpace(eStructs building)

[LuaApiExport("Building_SetDefaultHousingPopulationSpace")]
public void SetDefaultHousingPopulationSpace(eStructs building, ushort value)
```

---

## Methods — Fire

### `GetBuildingFireDamage` / `SetBuildingFireDamage`

Gets or sets the building fire damage. Default is 1 for almost all buildings.

```csharp
[LuaApiExport("Building_GetFireDamage")]
public short GetBuildingFireDamage(eStructs building)

public void SetBuildingFireDamage(eStructs building, short damage)
```

---

### `GetOnFireTicks` / `SetOnFireTicks`

Gets or sets the number of game ticks a building has been on fire. Any value greater than 0 causes the building to catch fire.

```csharp
[LuaApiExport("Building_GetOnFireTicks")]
public ushort GetOnFireTicks(int buildingId)

[LuaApiExport("Building_SetOnFireTicks")]
public void SetOnFireTicks(int buildingId, ushort onFireTicks)
```

---

## Methods — Stable Tracking

### `SetStablesUnitIdLink`

Links a live unit instance to a specific horse slot in a stable building.

```csharp
[LuaApiExport("Building_SetStablesUnitIdLink")]
public void SetStablesUnitIdLink(int buildingId, int slot, int unitId, int unitGlobalId)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `buildingId` | `int` | The stable building's instance ID. |
| `slot` | `int` | The zero-based slot index (0–3). The stable supports up to 4 horse slots (`gStablesHorsesCap = 4`). |
| `unitId` | `int` | The instance ID of the unit to link. Must be a valid live unit (checked via `TryGetUnitById`). |
| `unitGlobalId` | `int` | The global ID of the unit to link (from `GameUnitManagerAPI.GetGlobalId`). |

> Use `GetStablesUnitGlobalIdLink` to find empty slots before calling this (a return value ≤ 0 means the slot is free).

---

### `GetStablesUnitGlobalIdLink`

Returns the global ID of the unit occupying a specific horse slot in a stable building.

```csharp
[LuaApiExport("Building_GetStablesUnitGlobalIdLink")]
public int GetStablesUnitGlobalIdLink(int buildingId, int slot)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `buildingId` | `int` | The stable building's instance ID. |
| `slot` | `int` | The zero-based slot index (0–3). |

**Returns:** The global ID of the unit in this slot, or `-1` if the building is not found. A value ≤ 0 indicates the slot is empty.

> Used by CrusaderDETweaker's `OnUnitCreate` hook to find free slots before calling `SetStablesUnitIdLink`.

---

## Methods — Building Information

### `GetType`

Gets the current type of a building.

```csharp
[LuaApiExport("Building_GetType")]
public eStructs GetType(int buildingId)
```

**Returns:** The building type, or null if not found.

---

### `GetOwner` / `SetOwner`

Gets or sets the current owner of a building.

```csharp
[LuaApiExport("Building_GetOwner")]
public int GetOwner(int buildingId)

[LuaApiExport("Building_SetOwner")]
public void SetOwner(int buildingId, int playerId)
```

---

### `GetGlobalId`

Gets the global ID of a building.

```csharp
[LuaApiExport("Building_GetGlobalId")]
public int GetGlobalId(int buildingId)
```

---

### `GetRequiredWorkers` / `SetRequiredWorkers`

Gets or sets the required workers of a building.

```csharp
[LuaApiExport("Building_GetRequiredWorkers")]
public int GetRequiredWorkers(int buildingId)

[LuaApiExport("Building_SetRequiredWorkers")]
public void SetRequiredWorkers(int buildingId, ushort totalWorkers)
```

---

### `IsSleeping` / `SetSleeping`

Checks or sets whether a building is in a "sleeping" (inactive) state.

```csharp
[LuaApiExport("Building_IsSleeping")]
public bool IsSleeping(int buildingId)

[LuaApiExport("Building_SetSleeping")]
public void SetSleeping(int buildingId, bool isSleeping)
```

---

## Methods — Position & Tiles

### `GetBeginPosition` / `GetEndPosition`

Gets the tile begin/end position of a building.

```csharp
[LuaApiExport("Building_GetBeginPosition")]
public UnmanagedVector2<ushort> GetBeginPosition(int buildingId)

[LuaApiExport("Building_GetEndPosition")]
public UnmanagedVector2<ushort> GetEndPosition(int buildingId)
```

---

### `GetBeginTileId`

Gets the beginning tile ID of a building.

```csharp
[LuaApiExport("Building_GetBeginTileId")]
public int GetBeginTileId(int buildingId)
```

---

### `GetOccupiedTileIds`

Gets all occupied tile IDs of a building.

```csharp
[LuaApiExport("Building_GetOccupiedTileIds")]
public int[] GetOccupiedTileIds(int buildingId)
```

---

### `GetOccupyTileGridSize`

Gets the occupying tile grid size of a building (e.g., woodcutter = 3 for 3x3).

```csharp
[LuaApiExport("Building_GetBuildingOccupyTileGridSize")]
public int GetOccupyTileGridSize(int buildingId)
```

---

## Methods — Production & Storage

### `GetCurrentlyProducedGood` / `SetCurrentlyProducedGood`

Gets or sets the good that this building is supposed to produce.

```csharp
[LuaApiExport("Building_GetProductionGood")]
public eGoods GetCurrentlyProducedGood(int buildingId)

[LuaApiExport("Building_SetProductionGood")]
public void SetCurrentlyProducedGood(int buildingId, eGoods good)
```

---

### `GetCurrentGoodStackAmount` / `SetCurrentGoodStackAmount`

Gets or sets the current number of goods in the primary production/storage stack.

```csharp
[LuaApiExport("Building_GetCurrentStackAmount")]
public int GetCurrentGoodStackAmount(int buildingId)

[LuaApiExport("Building_SetCurrentStackAmount")]
public void SetCurrentGoodStackAmount(int buildingId, int amount)
```

**Returns:** The current stack amount, or `-1` if not found.

---

### `GetMaxGoodStackAmount` / `SetMaxGoodStackAmount`

Gets or sets the maximum capacity of the primary production/storage stack.

```csharp
[LuaApiExport("Building_GetMaxStackAmount")]
public int GetMaxGoodStackAmount(int buildingId)

[LuaApiExport("Building_SetMaxStackAmount")]
public void SetMaxGoodStackAmount(int buildingId, int amount)
```

**Returns:** The maximum stack capacity, or `-1` if not found.

---

### `GetLocalStorageGoodType` / `SetLocalStorageGoodType`

Gets or sets the type of good managed by the building's primary storage.

```csharp
[LuaApiExport("Building_GetLocalStorageGoodType")]
public eGoods GetLocalStorageGoodType(int buildingId)

[LuaApiExport("Building_SetLocalStorageGoodType")]
public void SetLocalStorageGoodType(int buildingId, eGoods goodType)
```

---

### `GetLocalStorage`

Gets the amount of a specific good stored in a building's local storage.

```csharp
[LuaApiExport("Building_GetLocalStorage")]
public int GetLocalStorage(int buildingId, eGoods goodType)
```

---

### `GetAllLocalStorage`

Retrieves all local storage amounts for each good type in the specified building.

```csharp
[LuaApiExport("Building_GetAllLocalStorage")]
public int[] GetAllLocalStorage(int buildingId)
```

**Returns:** An array indexed by `eGoods` enum. Returns an empty array if not found.

---

### `AddLocalGoodsAmountEx`

Adds a specified amount of a good to a building's local storage.

```csharp
[LuaApiExport("Building_AddLocalGood")]
public void AddLocalGoodsAmountEx(int buildingId, eGoods good, int amount, int? capacity)
```

> **Warning:** Custom implementation - may not perfectly match the game's internal logic.

---

### `RemoveLocalGoodsAmountEx`

Removes a specified amount of a good from a building's local storage.

```csharp
[LuaApiExport("Building_RemoveLocalGood")]
public void RemoveLocalGoodsAmountEx(int buildingId, eGoods good, int amount)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `amount` | `int` | Should be negative (e.g., `-10` to remove 10). |

> **Warning:** Custom implementation - may not perfectly match the game's internal logic.

---

### `AddGoodToGoodsyard`

Adds a specified amount of a good to a goodsyard.

```csharp
[LuaApiExport("Building_AddGoodToGoodsyard")]
public bool AddGoodToGoodsyard(int buildingId, int buildingGlobalId, eGoods good, int amount, int? capacity, bool add)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `buildingId` | `int` | The ID of the goodsyard to modify. |
| `buildingGlobalId` | `int` | The GlobalID of the goodsyard. |
| `good` | `eGoods` | The type of good to add. |
| `amount` | `int` | The amount to add (positive). |
| `capacity` | `int?` | Optional capacity override. If null, building's own max is used. |
| `add` | `bool` | If the function should actually add to the goodsyard or not. |

> Behaves exactly like the game's internal goodsyard function.

---

### `UpdateVisualResourceGoods`

Updates the visual resources for a building. For full effect, also call `TryUpdateTileResourceVisualsForBuilding`.

```csharp
[LuaApiExport("Building_UpdateVisuals")]
public void UpdateVisualResourceGoods(int buildingId)
```

---

### `GetAliveEmptyResourceBuildingsOfTypeByPlayer`

Gets the count of all active, empty resource buildings of a specific type owned by a player.

```csharp
[LuaApiExport("Building_GetEmptyResourceCount")]
public int GetAliveEmptyResourceBuildingsOfTypeByPlayer(int playerId, eStructs building)
```

> 1:1 replacement for the game's internal function.

---

## Methods — Validation

### `IsValid`

Checks if a building ID is valid and the building exists.

```csharp
[LuaApiExport("Building_IsValid")]
public bool IsValid(int buildingId)
```

---

### `IsValidId`

Checks if a building ID is within the valid range.

```csharp
[LuaApiExport("Building_IsValidId")]
public bool IsValidId(int buildingId)
```

---

## Methods — Query System

### `GetAllAliveBuildings`

Gets all alive buildings in the game.

```csharp
[LuaApiExport("Building_GetAllAlive")]
public int[] GetAllAliveBuildings()
```

---

### `GetAllBuildings`

Fills a list with IDs of all buildings, with optional filters.

```csharp
public void GetAllBuildings(List<int> results, AliveState? stateFilter = null, eStructs? buildingType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `GetBuildingsWithinRect`

Fills a list with IDs of buildings within a rectangular area, with optional filters.

```csharp
public void GetBuildingsWithinRect(List<int> results, int x, int y, int width, int height, AliveState? stateFilter = null, eStructs? buildingType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `GetBuildingsWithinSphere`

Fills a list with IDs of buildings within a spherical area, with optional filters.

```csharp
public void GetBuildingsWithinSphere(List<int> results, int x, int y, int radius, AliveState? stateFilter = null, eStructs? buildingType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `QueryBuildings`

Begins a high-performance query over all possible building slots.

```csharp
public GameStructQuery<GameBuilding> QueryBuildings()
```

---

### `ExecuteQuery`

The core query execution method. All public query functions delegate to this.

```csharp
public void ExecuteQuery(List<int> results, RefPredicate<GameBuilding> basePredicate, AliveState? stateFilter, eStructs? buildingType, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

## Low-Level Access

### `GetBuildingsArray`

Returns the underlying array of game buildings managed by this instance.

```csharp
public SimpleNativeArray<GameBuilding> GetBuildingsArray()
```

---

### `GetBuildingsAsSpan`

Returns a span representing the current collection of buildings. Provides direct access to underlying building data without allocating additional memory.

```csharp
public Span<GameBuilding> GetBuildingsAsSpan()
```

> Modifications to the span affect the original collection.

---

### `GetBuildingManager`

Gets a native pointer to the current game building manager instance.

```csharp
public NativePointer<GameBuildingManager> GetBuildingManager()
```

---

### `TryGetBuildingById`

Attempts to retrieve a direct, raw pointer to a building by its ID.

```csharp
public bool TryGetBuildingById(int buildingId, out GameBuilding* building)
```

---

### `TryGetBuildingByIdEx`

Attempts to retrieve a safe, wrapped pointer to a building object by its ID.

```csharp
public bool TryGetBuildingByIdEx(int buildingId, out NativePointer<GameBuilding> building)
```
