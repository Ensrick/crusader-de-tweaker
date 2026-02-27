# GameUnitManagerAPI

**Namespace:** `SHCDESE.API`
**Assembly:** `SHCDESE.dll`

Provides a high-level API for interacting with game units.

```csharp
public sealed class GameUnitManagerAPI
```

## Remarks

This class is a singleton that serves as the primary entry point for creating, deleting, and querying units in the game world. It offers direct manipulation of unit properties, access to game data tables (like costs and damage), and a query system.

---

## Properties

### `Instance`

Gets the singleton instance.

```csharp
public static GameUnitManagerAPI Instance { get; }
```

---

### `MeleeDamageLookupTable`

Gets the damage lookup table for standard melee attacks.

```csharp
public ManagedNativeMatrix<int> MeleeDamageLookupTable { get; }
```

---

## Methods — Unit Creation & Deletion

### `CreateUnitLocal`

Creates a unit at a local (8x scaled) tile position.

```csharp
[LuaApiExport("Unit_CreateLocal")]
public long CreateUnitLocal(int playerColorId, int playerOwnerId, int localTileX, int localTileY, int heightElevation, eChimps chimp)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `playerColorId` | `int` | The player sprite color this unit will use. |
| `playerOwnerId` | `int` | The ID of the player who will own and control the unit. |
| `localTileX` | `int` | The local tile X-coordinate (world coordinate * 8). |
| `localTileY` | `int` | The local tile Y-coordinate (world coordinate * 8). |
| `heightElevation` | `int` | The unit's height elevation on the tile. |
| `chimp` | `eChimps` | The type of unit to create. |

**Returns:** `long` — The unique ID of the newly created unit.

> Internally calls `CreateUnitWorld` after scaling the coordinates.

---

### `CreateUnitWorld`

Creates a unit at a specific world tile position.

```csharp
[LuaApiExport("Unit_CreateWorld")]
public long CreateUnitWorld(int playerColorId, int playerOwnerId, int worldTileX, int worldTileY, int heightElevation, eChimps chimp)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `playerColorId` | `int` | The player sprite color this unit will use. |
| `playerOwnerId` | `int` | The ID of the player who will own and control the unit. |
| `worldTileX` | `int` | The world tile X-coordinate. |
| `worldTileY` | `int` | The world tile Y-coordinate. |
| `heightElevation` | `int` | The unit's height elevation on the tile. |
| `chimp` | `eChimps` | The type of unit to create. |

**Returns:** `long` — The unique ID of the newly created unit.

---

### `DeleteUnit`

Immediately deletes a unit from the game.

```csharp
[LuaApiExport("Unit_Delete")]
public void DeleteUnit(int unitId)
```

> For a safer alternative, consider using `DeleteUnitSafe`.

---

### `DeleteUnitSafe`

Safely marks a unit object for deletion by the game engine.

```csharp
[LuaApiExport("Unit_DeleteSafe")]
public bool DeleteUnitSafe(int unitId)
```

**Returns:** `true` if the unit was found and marked for deletion; otherwise `false`.

> **Recommended.** Changes the unit's state, allowing the game engine to clean it up gracefully on a subsequent frame.

---

### `KillUnit`

Instantly kills a unit by setting its health to zero.

```csharp
[LuaApiExport("Unit_Kill")]
public void KillUnit(int unitId)
```

---

## Methods — Health

### `GetDefaultHealth` / `SetDefaultHealth`

Gets or sets the default (maximum) health for a specific unit type.

```csharp
[LuaApiExport("Unit_GetDefaultHealth")]
public uint GetDefaultHealth(eChimps chimp)

[LuaApiExport("Unit_SetDefaultHealth")]
public void SetDefaultHealth(eChimps chimp, uint value)
```

---

### `GetMaxHealth` / `SetMaxHealth`

Gets or sets the max health for a specific unit instance.

```csharp
[LuaApiExport("Unit_GetMaxHealth")]
public int GetMaxHealth(int unitId)

[LuaApiExport("Unit_SetMaxHealth")]
public void SetMaxHealth(int unitId, int maxHealth)
```

---

### `GetCurrentHealth` / `SetCurrentHealth`

Gets or sets a unit's current health (respecting max HP on set).

```csharp
[LuaApiExport("Unit_GetCurrentHealth")]
public int GetCurrentHealth(int unitId)

[LuaApiExport("Unit_SetCurrentHealth")]
public void SetCurrentHealth(int unitId, int health)
```

---

### `GetShieldCurrentHealth` / `SetShieldHealth`

Retrieve or set the **current** shield health for a live unit instance (demolisher-exclusive).

```csharp
[LuaApiExport("Unit_GetShieldCurrentHealth")]
public int GetShieldCurrentHealth(int unitId)

[LuaApiExport("Unit_SetShieldCurrentHealth")]
public void SetShieldHealth(int unitId, ushort health)
```

**Returns:** The shield current health on success; otherwise `-1`.

---

### `GetDefaultShieldHealth` / `SetDefaultShieldHealth`

Get or set the **default** shield health for all Bedouin Demolisher units. Wraps `GameGlobalsManager.Instance.BedouinDemolisherShieldHealth`. Use this to change the starting shield health applied to all newly spawned demolishers.

```csharp
[LuaApiExport("Unit_GetDefaultShieldHealth")]
public UInt16 GetDefaultShieldHealth()

[LuaApiExport("Unit_SetDefaultShieldHealth")]
public void SetDefaultShieldHealth(UInt16 health)
```

**Example:**
```csharp
Plugin.UnitApi.SetDefaultShieldHealth(50000); // all new demolishers start with 50000 shield
```

---

## Methods — Speed

### `GetDefaultSpeed` / `SetDefaultSpeed`

Gets or sets the default speed for a specific unit type. A lower value means faster movement. Clamped between 0 and 6.

```csharp
[LuaApiExport("Unit_GetDefaultSpeed")]
public ushort GetDefaultSpeed(eChimps chimp)

[LuaApiExport("Unit_SetDefaultSpeed")]
public void SetDefaultSpeed(eChimps chimp, ushort value)
```

---

### `GetSpeed` / `SetSpeed`

Gets or sets the current speed for a specific unit instance.

```csharp
[LuaApiExport("Unit_GetSpeed")]
public int GetSpeed(int unitId)

[LuaApiExport("Unit_SetSpeed")]
public void SetSpeed(int unitId, ushort speedLevel)
```

**Returns:** The current speed level of the unit; otherwise `-1`.

---

### `GetDefaultCavalryRunSpeedBonus` / `SetDefaultCavalryRunSpeedBonus`

Gets or sets the default run speed bonus for a specific cavalry unit type.

```csharp
[LuaApiExport("Unit_GetDefaultCavalryRunSpeedBonus")]
public ushort GetDefaultCavalryRunSpeedBonus(eChimps chimp)

[LuaApiExport("Unit_SetDefaultCavalryRunSpeedBonus")]
public void SetDefaultCavalryRunSpeedBonus(eChimps chimp, ushort value)
```

---

## Methods — Damage (Dealing)

### `DamageUnitMelee`

Damage a unit by a melee damage source. This function can kill units.

```csharp
[LuaApiExport("Unit_MeleeDamage")]
public bool DamageUnitMelee(int attackerUnitId, int victimUnitId, int damage = 0)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `attackerUnitId` | `int` | The ID of the unit that causes damage. Can be 0. |
| `victimUnitId` | `int` | The ID of the unit that receives damage. |
| `damage` | `int` | The amount of damage to inflict. Leave at 0 for default damage by attacker. |

**Returns:** `true` on success.

---

### `DamageUnitRanged`

Damage a unit by a ranged damage source. This function can kill units.

```csharp
[LuaApiExport("Unit_RangedDamage")]
public bool DamageUnitRanged(int victimUnitId, int projectileId, int damage = 0)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `victimUnitId` | `int` | The ID of the unit that receives damage. |
| `projectileId` | `int` | The ID of the projectile that causes damage. Cannot be 0. |
| `damage` | `int` | The amount of damage to inflict. Leave at 0 for default damage by projectile. |

**Returns:** `true` on success.

---

### `DamageUnitEx`

Influences a unit's health by a value. This function does not usually kill a unit by itself.

```csharp
[LuaApiExport("Unit_DamageEx")]
public void DamageUnitEx(int unitId, int deltaHealth)
```

---

### `DamageUnitEx2`

Damages a unit based on the game's conventional damage tables.

```csharp
[LuaApiExport("Unit_DamageEx2")]
public void DamageUnitEx2(int unitId, eChimps attacker, eChimps defender)
```

> **Warning:** This function is untested and might not account for all unit types.

---

## Methods — Damage Lookup Tables (Melee)

### `GetMeleeDamageFromTo` / `SetMeleeDamageFromTo`

Gets or sets the base damage a melee unit inflicts upon a target unit type.

```csharp
[LuaApiExport("Unit_GetMeleeDamageFromTo")]
public int GetMeleeDamageFromTo(eChimps source, eChimps target)

[LuaApiExport("Unit_SetMeleeDamageFromTo")]
public void SetMeleeDamageFromTo(eChimps source, eChimps target, int value)
```

---

### `GetMeleeEunuchAOEDamageTo` / `SetMeleeEunuchAOEDamageTo`

Gets or sets the base damage from a Eunuch's area-of-effect attack upon a target unit type.

```csharp
[LuaApiExport("Unit_GetMeleeEunuchAOEDamageTo")]
public int GetMeleeEunuchAOEDamageTo(eChimps target)

[LuaApiExport("Unit_SetMeleeEunuchAOEDamageTo")]
public void SetMeleeEunuchAOEDamageTo(eChimps target, int value)
```

---

## Methods — Damage Lookup Tables (Ranged)

### `GetRangedArrowDamageTo` / `SetRangedArrowDamageTo`

Gets or sets the base damage from an arrow projectile upon a target unit type.

```csharp
[LuaApiExport("Unit_GetRangedArrowDamageTo")]
public int GetRangedArrowDamageTo(eChimps target)

[LuaApiExport("Unit_SetRangedArrowDamageTo")]
public void SetRangedArrowDamageTo(eChimps target, int value)
```

---

### `GetRangedBoltDamageTo` / `SetRangedBoltDamageTo`

Gets or sets the base damage from a bolt projectile (e.g., from a Crossbowman or Ballista) upon a target unit type.

```csharp
[LuaApiExport("Unit_GetRangedBoltDamageTo")]
public int GetRangedBoltDamageTo(eChimps target)

[LuaApiExport("Unit_SetRangedBoltDamageTo")]
public void SetRangedBoltDamageTo(eChimps target, int value)
```

---

### `GetRangedSlingerDamageTo` / `SetRangedSlingerDamageTo`

Gets or sets the base damage from a slinger's stone projectile upon a target unit type.

```csharp
[LuaApiExport("Unit_GetRangedSlingerDamageTo")]
public int GetRangedSlingerDamageTo(eChimps target)

[LuaApiExport("Unit_SetRangedSlingerDamageTo")]
public void SetRangedSlingerDamageTo(eChimps target, int value)
```

---

### `GetRangedJavelinDamageTo` / `SetRangedJavelinDamageTo`

Gets or sets the base damage from a javelin projectile upon a target unit type.

```csharp
[LuaApiExport("Unit_GetRangedJavelinDamageTo")]
public int GetRangedJavelinDamageTo(eChimps target)

[LuaApiExport("Unit_SetRangedJavelinDamageTo")]
public void SetRangedJavelinDamageTo(eChimps target, int value)
```

---

## Methods — Fire & Healing

### `GetFireDamage` / `SetFireDamage`

Gets or sets the fire damage for a unit type. Default is 100 for almost all units.

```csharp
[LuaApiExport("Unit_GetFireDamage")]
public int GetFireDamage(eChimps unit)

[LuaApiExport("Unit_SetFireDamage")]
public void SetFireDamage(eChimps unit, int damage)
```

---

### `GetBedouinHeal` / `SetBedouinHeal`

Gets or sets the heal amount from a Bedouin healer. Default is 10 for all units.

```csharp
[LuaApiExport("Unit_GetBedouinHeal")]
public int GetBedouinHeal(eChimps unit)

[LuaApiExport("Unit_SetBedouinHeal")]
public void SetBedouinHeal(eChimps unit, int heal)
```

---

## Methods — Gold & Resource Costs

### `GetUnitGoldCost` / `SetUnitGoldCost`

Gets or sets the gold cost to recruit a specific unit type.

```csharp
[LuaApiExport("Unit_GetGoldCost")]
public int GetUnitGoldCost(eChimps chimp)

[LuaApiExport("Unit_SetGoldCost")]
public void SetUnitGoldCost(eChimps chimp, int value)
```

**Returns:** The gold cost; otherwise `0`.

---

### `GetUnitGoodCosts` / `SetUnitGoodCosts`

Gets or sets the resource costs (besides gold) to recruit a European unit type.

```csharp
[LuaApiExport("Unit_GetGoodCosts")]
public UnitGoodCosts GetUnitGoodCosts(eChimps chimp)

public void SetUnitGoodCosts(eChimps chimp, UnitGoodCosts costs)
```

**Returns:** A `UnitGoodCosts` struct detailing the required goods.

> When signifying a horse requirement, set it as the last good requirement:
> `SetUnitGoodCosts(eChimps.CHIMP_TYPE_KNIGHT, eGoods.STORED_SWORDS, eGoods.STORED_METAL_ARMOUR, eGoods.STORED_NULL, eGoods._SE_REQUIRE_HORSE)`

---

## Methods — Unit Information

### `GetType`

Gets the type of a unit.

```csharp
[LuaApiExport("Unit_GetType")]
public eChimps GetType(int unitId)
```

**Returns:** The unit type (`eChimps`).

---

### `GetOwner`

Gets the player owner of a unit.

```csharp
[LuaApiExport("Unit_GetOwner")]
public int GetOwner(int unitId)
```

**Returns:** The player owner ID.

---

### `GetTribe`

Gets the tribe of a unit.

```csharp
[LuaApiExport("Unit_GetTribe")]
public int GetTribe(int unitId)
```

---

### `GetGlobalId` / `GetByGlobalId`

Converts between unit IDs and global IDs.

```csharp
[LuaApiExport("Unit_GetGlobalId")]
public int GetGlobalId(int unitId)

[LuaApiExport("Unit_GetByGlobalId")]
public int GetByGlobalId(int globalId)
```

---

### `GetGameMaterial` / `SetGameMaterial`

Gets or sets the game material of a unit.

```csharp
[LuaApiExport("Unit_GetGameMaterial")]
public GM GetGameMaterial(int unitId)

[LuaApiExport("Unit_SetGameMaterial")]
public void SetGameMaterial(int unitId, GM material)
```

---

### `GetUnitArmyCount`

Retrieves the total count of a specific army unit type for the local player.

```csharp
[LuaApiExport("Unit_GetArmyCount")]
public int GetUnitArmyCount(eChimps chimp)
```

> Reads directly from the game's internal army count table.

---

## Methods — Position

### `GetCurrentLocalTilePosition` / `SetCurrentLocalTilePosition`

Gets or sets the current local tile position of a unit.

```csharp
[LuaApiExport("Unit_GetLocalPosition")]
public UnmanagedVector2<ushort> GetCurrentLocalTilePosition(int unitId)

[LuaApiExport("Unit_SetLocalPosition")]
public void SetCurrentLocalTilePosition(int unitId, UnmanagedVector2<ushort> localTilePosition)
```

> Local coordinates are the coarse-grained grid positions used by the map grid system.
>
> **Warning:** Setting position directly can cause visual glitches (unit briefly turning invisible before reappearing).

---

### `GetCurrentWorldTilePosition` / `SetCurrentWorldTilePosition`

Gets or sets the current world tile position of a unit.

```csharp
[LuaApiExport("Unit_GetWorldPosition")]
public UnmanagedVector2<ushort> GetCurrentWorldTilePosition(int unitId)

[LuaApiExport("Unit_SetWorldPosition")]
public void SetCurrentWorldTilePosition(int unitId, UnmanagedVector2<ushort> worldTilePosition)
```

> World coordinates are fine-grained: 1 local tile = 8 world tiles. Subject to the same visual glitches as local position setting.

---

### `GetCurrentUnityPosition`

Gets the current Unity-side position of a unit.

```csharp
[LuaApiExport("Unit_GetUnityPosition")]
public Vector3 GetCurrentUnityPosition(int unitId)
```

---

### `MoveToTile`

Orders a unit to move to a specific tile coordinate.

```csharp
[LuaApiExport("Unit_MoveTo")]
public void MoveToTile(int unitId, int tileX, int tileY, int unknown = 0)
```

---

## Methods — Visibility & Selection

### `GetIsInvisible` / `SetIsInvisible`

Gets or sets the invisibility state of a unit.

```csharp
[LuaApiExport("Unit_IsInvisible")]
public bool GetIsInvisible(int unitId)

[LuaApiExport("Unit_SetInvisible")]
public void SetIsInvisible(int unitId, bool isInvisible)
```

---

### `IsVisualHidden` / `SetVisualHidden`

Checks or sets if a unit is supposed to be hidden visually (Unity-side).

```csharp
[LuaApiExport("Unit_GetVisualHidden")]
public bool IsVisualHidden(int unitId)

[LuaApiExport("Unit_SetVisualHidden")]
public void SetVisualHidden(int unitId, bool hidden)
```

---

### `IsSelectable` / `SetSelectable`

Gets or sets the selectability of a unit.

```csharp
[LuaApiExport("Unit_IsSelectable")]
public bool IsSelectable(int unitId)

[LuaApiExport("Unit_SetSelectable")]
public void SetSelectable(int unitId, bool selectable)
```

---

### `IsRendered`

Checks if a unit is strictly within the camera view.

```csharp
[LuaApiExport("Unit_IsRendered")]
public bool IsRendered(int unitId)
```

---

## Methods — Sprite

### `GetSpriteScale` / `SetSpriteScale`

Gets or sets the local scale of a unit's sprite.

```csharp
[LuaApiExport("Unit_GetSpriteScale")]
public Vector3 GetSpriteScale(int unitId)

[LuaApiExport("Unit_SetSpriteScale")]
public void SetSpriteScale(int unitId, Vector3 scale)
```

---

### `ResetSpriteScale`

Resets the local scale of a unit's sprite.

```csharp
[LuaApiExport("Unit_ResetSpriteScale")]
public void ResetSpriteScale(int unitId, Vector3 scale)
```

---

## Methods — Validation

### `IsValid`

Checks if a unit ID is valid and the unit exists.

```csharp
[LuaApiExport("Unit_IsValid")]
public bool IsValid(int unitId)
```

---

### `IsValidId`

Checks if a unit ID is within the valid range.

```csharp
[LuaApiExport("Unit_IsValidId")]
public bool IsValidId(int unitId)
```

---

## Methods — Query System

### `GetAllAliveUnits`

Gets all alive units in the game.

```csharp
[LuaApiExport("Unit_GetAllAlive")]
public int[] GetAllAliveUnits()
```

---

### `GetAllChimpTypes`

Gets all available chimp types.

```csharp
[LuaApiExport("Unit_GetAllChimpTypes")]
public eChimps[] GetAllChimpTypes()
```

---

### `GetAllUnits`

Fills a list with IDs of all units, with optional filters (no spatial constraint).

```csharp
public void GetAllUnits(List<int> results, AliveState? stateFilter = null, eChimps? unitType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `GetUnitsWithinRect`

Fills a list with IDs of units within a rectangular area, with optional filters.

```csharp
public void GetUnitsWithinRect(List<int> results, int x, int y, int width, int height, AliveState? stateFilter = null, eChimps? unitType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `GetUnitsWithinSphere`

Fills a list with IDs of units within a spherical area, with optional filters.

```csharp
public void GetUnitsWithinSphere(List<int> results, int x, int y, int radius, AliveState? stateFilter = null, eChimps? unitType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
```

---

### `QueryUnits`

Begins a high-performance query over all possible unit slots.

```csharp
public GameStructQuery<GameUnit> QueryUnits()
```

---

### `ExecuteQuery`

The core query execution method. All public query functions delegate to this.

```csharp
public void ExecuteQuery(List<int> results, RefPredicate<GameUnit> basePredicate, AliveState? stateFilter, eChimps? unitType, PlayerRelationship? relationship, int? povPlayerId)
```

---

## Low-Level Access

### `GetUnitArray`

Returns the underlying array of game units managed by this instance.

```csharp
public SimpleNativeArray<GameUnit> GetUnitArray()
```

---

### `GetUnitsAsSpan`

Returns a span representing the current collection of units. Provides direct access to underlying unit data without allocating additional memory.

```csharp
public Span<GameUnit> GetUnitsAsSpan()
```

> Modifications to the span affect the original collection.

---

### `GetUnitManager`

Gets a native pointer to the current game unit manager instance.

```csharp
public NativePointer<GameUnitManager> GetUnitManager()
```

---

### `TryGetUnitById`

Attempts to retrieve a direct, raw pointer to a unit by its ID.

```csharp
public bool TryGetUnitById(int unitId, out GameUnit* unit)
```

---

### `TryGetUnitByIdEx`

Attempts to retrieve a safe, wrapped pointer to a unit by its ID.

```csharp
public bool TryGetUnitByIdEx(int unitId, out NativePointer<GameUnit> unit)
```
