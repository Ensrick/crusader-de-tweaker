# GamePlayerManagerAPI

**Namespace:** `SHCDESE.API`
**Assembly:** `SHCDESE.dll`

Provides a high-level API for interacting with game players and their associated resources.

```csharp
public sealed class GamePlayerManagerAPI
```

## Remarks

Singleton entry point for managing player-specific data such as gold, popularity, teams, and resource management. Also provides access to map-wide rules like available buildings, units, and trade goods.

---

## Constants

### `MAX_PLAYERS`

The maximum number of players supported by the game engine.

```csharp
public const int MAX_PLAYERS = 8
```

---

## Properties

### `Instance`

Gets the singleton instance.

```csharp
public static GamePlayerManagerAPI Instance { get; }
```

---

## Fields

| Field | Type |
|-------|------|
| `_choreManagerOptionsInternal` | `ChoreManagerOptionsInternal` |
| `_globalImprovedSiegeBehaviour` | `int*` |
| `_noKnockdownWalls` | `int*` |

---

## Methods — Player Gold & Resources

### `GetPlayerGold`

Gets the total gold for a specific player.

```csharp
[LuaApiExport("Player_GetGold")]
public uint GetPlayerGold(int playerId)
```

**Returns:** Gold amount on success; otherwise `0`.

---

### `SetPlayerGold`

Sets the total gold for a specific player.

```csharp
[LuaApiExport("Player_SetGold")]
public bool SetPlayerGold(int playerId, uint gold)
```

**Returns:** `true` on success; otherwise `false`.

---

### `AddPlayerGold`

Adds or subtracts gold from a specific player. The final amount is clamped at zero.

```csharp
[LuaApiExport("Player_AddGold")]
public bool AddPlayerGold(int playerId, int gold)
```

**Returns:** `true` on success; otherwise `false`.

---

### `GetGoodAmount`

Retrieves the quantity of the specified good owned by the given player.

```csharp
[LuaApiExport("Player_GetGoodAmount")]
public int GetGoodAmount(int playerId, eGoods good)
```

**Returns:** Number of units of the good; `0` if none.

---

### `HasGoodsAmount`

Checks if a player has at least a certain amount of a specific good.

```csharp
[LuaApiExport("Player_HasGoodsAmount")]
public bool HasGoodsAmount(int playerId, eGoods good, int amount)
```

---

### `TryAddGood`

Adds a specified amount of a good to a player, distributing among their storage buildings. This is the recommended function for adding goods.

```csharp
[LuaApiExport("Player_AddGood")]
public bool TryAddGood(int playerId, eGoods good, int amount)
```

---

### `RemoveGood`

Removes a specified amount of a good from a player's storage buildings. This is the recommended function for removing goods.

```csharp
[LuaApiExport("Player_RemoveGood")]
public void RemoveGood(int playerId, eGoods good, int amount, bool bDontSubtractResources = false)
```

| Parameter | Description |
|-----------|-------------|
| `bDontSubtractResources` | Prevents the visual subtraction effect (unconfirmed). |

---

### `AddIncomingGood`

Adds a specified amount to a player's count of incoming goods (e.g., from scenario start).

```csharp
[LuaApiExport("Player_AddIncomingGood")]
public void AddIncomingGood(int playerId, eGoods good, int amount)
```

---

### `SubtractIncomingGood`

Subtracts a specified amount from a player's count of incoming goods.

```csharp
[LuaApiExport("Player_SubtractIncomingGood")]
public void SubtractIncomingGood(int playerId, eGoods good, int amount)
```

---

### `ClearIncomingGood`

Clears all incoming goods for the specified player.

```csharp
[LuaApiExport("Player_ClearIncomingGoods")]
public void ClearIncomingGood(int playerId)
```

---

## Methods — Popularity & Taxes

### `GetPlayerPopularity` / `SetPlayerPopularity`

```csharp
[LuaApiExport("Player_GetPopularity")]
public uint GetPlayerPopularity(int playerId)

[LuaApiExport("Player_SetPopularity")]
public bool SetPlayerPopularity(int playerId, uint popularity)
```

---

### `GetRationsMode` / `SetRationsMode`

```csharp
[LuaApiExport("Player_GetRationsMode")]
public RationsMode GetRationsMode(int playerId)

[LuaApiExport("Player_SetRationsMode")]
public void SetRationsMode(int playerId, RationsMode mode)
```

---

### `GetTaxesMode` / `SetTaxesMode`

```csharp
[LuaApiExport("Player_GetTaxesMode")]
public TaxesMode GetTaxesMode(int playerId)

[LuaApiExport("Player_SetTaxesMode")]
public void SetTaxesMode(int playerId, TaxesMode mode)
```

---

## Methods — Teams & Alliances

### `GetPlayerTeam` / `SetPlayerTeam`

```csharp
[LuaApiExport("Player_GetTeam")]
public int GetPlayerTeam(int playerId)

[LuaApiExport("Player_SetTeam")]
public void SetPlayerTeam(int playerId, int team)
```

---

### `IsPlayerAlliedTo`

Checks if two players are on the same team.

```csharp
[LuaApiExport("Player_IsAlliedTo")]
public bool IsPlayerAlliedTo(int playerId1, int playerId2)
```

---

## Methods — Player Identity

### `GetLocalPlayerId`

```csharp
[LuaApiExport("Player_GetLocalId")]
public int GetLocalPlayerId()
```

---

### `GetAlivePlayerIds`

Gets IDs of all players currently alive and not defeated.

```csharp
[LuaApiExport("Player_GetAliveIds")]
public int[] GetAlivePlayerIds()
```

---

### `GetAllPlayerIds`

Gets all possible player IDs (1 through `MAX_PLAYERS`).

```csharp
[LuaApiExport("Player_GetAllIds")]
public int[] GetAllPlayerIds()
```

---

### `IsPlayerIdValid`

Validates if a player ID is within the valid range (1 to `MAX_PLAYERS`).

```csharp
[LuaApiExport("Player_IsPlayerIdValid")]
public bool IsPlayerIdValid(int playerId)
```

---

### `IsAIPlayer` / `SetIsAIPlayer`

```csharp
[LuaApiExport("Player_IsAI")]
public bool IsAIPlayer(int playerId)

[LuaApiExport("Player_SetIsAI")]
public void SetIsAIPlayer(int playerId, bool isAI)
```

---

### `GetAILord` / `SetAILord`

```csharp
[LuaApiExport("Player_GetAILord")]
public Enums.AILords GetAILord(int playerId)

[LuaApiExport("Player_SetAILord")]
public void SetAILord(int playerId, Enums.AILords aiLord)
```

`GetAILord` returns `SK_NULL` if the player is not AI or the ID is invalid.

---

## Methods — Lord & Keep

### `GetLordUnitId` / `SetLordUnitId`

```csharp
[LuaApiExport("Player_GetLordUnitId")]
public int GetLordUnitId(int playerId)

[LuaApiExport("Player_SetLordUnitId")]
public void SetLordUnitId(int playerId, int unitId)
```

---

### `GetLordUnitGlobalId` / `SetLordUnitGlobalId`

```csharp
[LuaApiExport("Player_GetLordUnitGlobalId")]
public int GetLordUnitGlobalId(int playerId)

[LuaApiExport("Player_SetLordUnitGlobalId")]
public void SetLordUnitGlobalId(int playerId, int globalId)
```

---

### `GetPlayerKeepId`

Gets the building ID of a player's keep.

```csharp
[LuaApiExport("Player_GetKeepId")]
public int GetPlayerKeepId(int playerId)
```

**Returns:** Keep ID on success; `-1` otherwise.

---

### `GetPlayerKeepPosition` / `SetPlayerKeepPosition`

Gets/sets the top-left tile coordinate of a player's keep.

```csharp
[LuaApiExport("Player_GetKeepPosition")]
public UnmanagedVector2<int> GetPlayerKeepPosition(int playerId)

[LuaApiExport("Player_SetKeepPosition")]
public bool SetPlayerKeepPosition(int playerId, UnmanagedVector2<uint> position)
```

---

### `GetPlayerKeepDoorPosition` / `SetPlayerKeepDoorPosition`

Gets/sets the tile coordinate of a player's keep door.

```csharp
[LuaApiExport("Player_GetKeepDoorPosition")]
public UnmanagedVector2<int> GetPlayerKeepDoorPosition(int playerId)

[LuaApiExport("Player_SetKeepDoorPosition")]
public bool SetPlayerKeepDoorPosition(int playerId, UnmanagedVector2<uint> position)
```

---

### `IsLocalPlayerKeepEnclosed`

```csharp
[LuaApiExport("Player_IsLocalPlayerKeepEnclosed")]
public bool IsLocalPlayerKeepEnclosed()
```

---

## Methods — Pause & Win/Loss

### `IsLocalPaused` / `SetLocalPaused`

```csharp
[LuaApiExport("Player_IsLocalPaused")]
public bool IsLocalPaused()

[LuaApiExport("Player_SetLocalPaused")]
public void SetLocalPaused(bool paused)
```

> **Warning:** `SetLocalPaused` does NOT pause the game in a proper multiplayer session.

---

### `IsPlayerPaused` / `SetPlayerIsPaused`

Controls whether a player or AI can build or interact with the game.

```csharp
[LuaApiExport("Player_IsPaused")]
public bool IsPlayerPaused(int playerId)

[LuaApiExport("Player_SetPaused")]
public void SetPlayerIsPaused(int playerId, bool paused)
```

---

### `SetWinLossState`

```csharp
[LuaApiExport("Player_SetWinLossState")]
public void SetWinLossState(int playerId, WinLossState state)
```

---

## Methods — Extreme Powers

### `IsLocalPlayerExtremePowersEnabled` / `SetLocalPlayerExtremePowersEnabled`

```csharp
[LuaApiExport("Player_IsLocalPlayerExtremePowersEnabled")]
public bool IsLocalPlayerExtremePowersEnabled()

[LuaApiExport("Player_SetLocalPlayerExtremePowersEnabled")]
public void SetLocalPlayerExtremePowersEnabled(bool enabled)
```

---

### `GetLocalPlayerExtremePowersMana` / `SetLocalPlayerExtremePowersMana`

```csharp
[LuaApiExport("Player_GetLocalPlayerExtremePowersMana")]
public int GetLocalPlayerExtremePowersMana(int playerId)

[LuaApiExport("Player_SetLocalPlayerExtremePowersMana")]
public void SetLocalPlayerExtremePowersMana(int playerId, int mana)
```

**Returns:** Mana on success; `-1` otherwise.

---

## Methods — Map Rules: Buildings

### `GetBuildingAvailability` / `SetBuildingAvailability`

```csharp
[LuaApiExport("Player_IsBuildingAvailable")]
public bool GetBuildingAvailability(eMappers building)

[LuaApiExport("Player_SetBuildingAvailable")]
public void SetBuildingAvailability(eMappers building, bool enabled)
```

---

### `SetAllBuildingAvailability`

```csharp
[LuaApiExport("Player_SetAllBuildingAvailability")]
public void SetAllBuildingAvailability(bool enabled)
```

---

## Methods — Map Rules: Units

### `IsUnitAllowed` / `SetIsUnitAllowed`

```csharp
[LuaApiExport("Player_IsUnitRecruitable")]
public bool IsUnitAllowed(eTroops unit)

[LuaApiExport("Player_SetUnitRecruitable")]
public void SetIsUnitAllowed(eTroops unit, bool enabled)
```

---

### `SetAllUnitsAllowed`

```csharp
[LuaApiExport("Player_SetAllUnitsAllowed")]
public void SetAllUnitsAllowed(bool enabled)
```

---

## Methods — Map Rules: Trade & Production

### `IsTradeGoodAllowed` / `SetTradeGoodAllowed`

```csharp
[LuaApiExport("Player_IsTradeGoodAllowed")]
public bool IsTradeGoodAllowed(eGoods good)

[LuaApiExport("Player_SetTradeGoodAllowed")]
public void SetTradeGoodAllowed(eGoods good, bool enabled)
```

---

### `SetAllTradeGoodsAllowed`

```csharp
[LuaApiExport("Player_SetAllTradeGoodsAllowed")]
public void SetAllTradeGoodsAllowed(bool enabled)
```

---

### `IsProductionGoodAllowed` / `SetIsProductionGoodAllowed`

```csharp
[LuaApiExport("Player_IsProductionGoodAllowed")]
public bool IsProductionGoodAllowed(eGoods goods)

[LuaApiExport("Player_SetProductionGoodAllowed")]
public void SetIsProductionGoodAllowed(eGoods goods, bool enabled)
```

---

### `SetAllProductionGoodAllowed`

```csharp
[LuaApiExport("Player_SetAllProductionGoodAllowed")]
public void SetAllProductionGoodAllowed(bool enabled)
```

---

## Methods — Food

### `IsFoodAllowed` / `SetFoodAllowed`

```csharp
[LuaApiExport("Player_IsFoodAllowed")]
public bool IsFoodAllowed(int playerId, eGoods food)

[LuaApiExport("Player_SetFoodAllowed")]
public void SetFoodAllowed(int playerId, eGoods food, bool allowed)
```

---

## Methods — Auto-Trade

### `SetAutoTrade`

Configures auto-trade settings for a specific good at the market.

```csharp
[LuaApiExport("Player_SetAutoTrade")]
public void SetAutoTrade(eGoods goods, bool enabled, ushort buyLevel, ushort sellLevel = 0)
```

| Parameter | Description |
|-----------|-------------|
| `buyLevel` | Storage amount below which the good is auto-bought. `-1` for no-op. |
| `sellLevel` | Storage amount above which the good is auto-sold. `-1` for no-op. |

---

## Methods — Trade Prices

### `GetTradeBasePrice`

Gets the base trade price of a good at the market as a `PackedGoodPrice` struct containing separate buy and sell prices.

```csharp
[LuaApiExport("Player_GetTradeBasePrice")]
public PackedGoodPrice GetTradeBasePrice(eGoods good)
```

**Returns:** A `PackedGoodPrice` struct with `BuyPrice` and `SellPrice` fields. Returns a default struct on error.

> **Note (SHCDE-SE 1.20.0):** The previous API returned `Int64` with the buy price truncated to the lower 32 bits, making it impossible to read the sell price. This was fixed in 1.20.0 with the `PackedGoodPrice` struct.

**Example:**
```csharp
var price = Plugin.PlayerApi.GetTradeBasePrice(eGoods.STORED_WOOD_PLANKS);
int buy  = price.BuyPrice;
int sell = price.SellPrice;
```

---

### `SetTradeBasePrice`

Sets the base trade price of a good at the market.

```csharp
[LuaApiExport("Player_SetTradeBasePrice")]
public void SetTradeBasePrice(eGoods good, PackedGoodPrice price)
```

> **Note (SHCDE-SE 1.20.0):** Previously this was a misnamed overload of `GetTradeBasePrice(eGoods, long)`. Corrected to `SetTradeBasePrice` in 1.20.0.

**Example:**
```csharp
var current = Plugin.PlayerApi.GetTradeBasePrice(eGoods.STORED_WOOD_PLANKS);
current.BuyPrice = 150;
Plugin.PlayerApi.SetTradeBasePrice(eGoods.STORED_WOOD_PLANKS, current);
```

---

## Methods — Skirmish Defaults

### `GetPlayerSkirmishDefaultGold` / `SetPlayerSkirmishDefaultGold`

```csharp
[LuaApiExport("Player_GetSkirmishDefaultGold")]
public uint GetPlayerSkirmishDefaultGold(int level)

[LuaApiExport("Player_SetSkirmishDefaultGold")]
public bool SetPlayerSkirmishDefaultGold(int level, uint gold)
```

---

### `GetPlayerSkirmishDefaultResources` / `SetPlayerSkirmishDefaultResources`

```csharp
[LuaApiExport("Player_GetSkirmishDefaultResources")]
public uint GetPlayerSkirmishDefaultResources(eGoods good)

[LuaApiExport("Player_SetSkirmishDefaultResources")]
public bool SetPlayerSkirmishDefaultResources(eGoods good, uint amount)
```

---

### `GetPlayerSkirmishDefaultUnitsAmount` / `SetPlayerSkirmishDefaultUnitsAmount`

> **Note:** Not all unit types are supported and may lead to unexpected results.

```csharp
[LuaApiExport("Player_GetSkirmishDefaultUnitsAmount")]
public int GetPlayerSkirmishDefaultUnitsAmount(eChimps unit)

[LuaApiExport("Player_SetSkirmishDefaultUnitsAmount")]
public void SetPlayerSkirmishDefaultUnitsAmount(eChimps unit, uint amount)
```

---

## Methods — Gameplay Options

| Method | Option | Signature |
|--------|--------|-----------|
| `IsAdvancedOptionsEnabled` / `SetAdvancedOptionsEnabled` | Advanced Options | `bool` / `void(bool)` |
| `IsAdvancedSkirmishOptionsEnabled` / `SetAdvancedSkirmishOptionsEnabled` | Advanced Skirmish Options | `bool` / `void(bool)` |
| `IsBetterHealers` / `SetBetterHealers` | Better Healers | `bool` / `void(bool)` |
| `IsFasterPeasants` / `SetFasterPeasants` | Faster Peasants | `bool` / `void(bool)` |
| `IsGlobalImprovedSiegeBehaviour` / `SetGlobalImprovedSiegeBehaviour` | Improved Siege Behaviour | `bool` / `void(bool)` |
| `IsImprovedArabSwordsman` / `SetImprovedArabSwordsman` | Improved Arab Swordsman | `bool` / `void(bool)` |
| `IsImprovedFletchers` / `SetImprovedFletchers` | Improved Fletchers | `bool` / `void(bool)` |
| `IsImprovedLadderman` / `SetImprovedLadderman` | Improved Ladderman | `bool` / `void(bool)` |
| `IsImprovedSpearman` / `SetImprovedSpearman` | Improved Spearman | `bool` / `void(bool)` |
| `IsNerfEunuchs` / `SetNerfEunuchs` | Nerf Eunuchs | `bool` / `void(bool)` |
| `IsRebalancedHorseArchers` / `SetRebalancedHorseArchers` | Rebalanced Horse Archers | `bool` / `void(bool)` |
| `IsUncappedPeasants` / `SetUncappedPeasants` | Uncapped Peasants | `bool` / `void(bool)` |
| `GetEnemyHealthModifier` / `SetEnemyHealthModifier` | Enemy Health | `EnemyHPModifier` / `void(EnemyHPModifier)` |
| `IsNoKnockdownWalls` / `SetNoKnockdownWalls` | Strong Walls | `bool` / `void(bool)` |

> **Warning:** `SetNoKnockdownWalls` can break AI behaviour.

---

## Methods — Camera & View

### `GetScreenCenterPosition` / `SetScreenCenterPosition`

```csharp
[LuaApiExport("Player_GetScreenCenterPosition")]
public Vector2 GetScreenCenterPosition()

[LuaApiExport("Player_SetScreenCenterPosition")]
public void SetScreenCenterPosition(Vector2 position)
```

> **Warning:** `SetScreenCenterPosition` operates in raw Unity World Space. For tile-based positioning, use `SetScreenCenterToTilePosition` instead.

---

### `GetScreenCenterTilePosition`

Gets the local game tile coordinate at the center of the screen. Accounts for camera position, zoom, rotation, and terrain height.

```csharp
public Vector2 GetScreenCenterTilePosition()
```

---

### `SetScreenCenterToTilePosition`

Moves the camera to center on a specific local game tile coordinate. **This is the recommended function for camera positioning.**

```csharp
[LuaApiExport("Player_SetScreenCenterToTilePosition")]
public void SetScreenCenterToTilePosition(Vector2 position)
```

The conversion process:
1. Takes a Local Tile Coordinate (the game logical grid).
2. Converts to an internal Isometric Tilemap Coordinate, accounting for map rotation.
3. Calculates the base Unity World Position for that tile.
4. Adjusts the World Position Y-axis based on terrain height.
5. Passes the final position to the camera controller.

---

### `SetScreenCenterToUnit`

Convenience wrapper that centers the camera on a unit by retrieving its tile position.

```csharp
[LuaApiExport("Player_SetScreenCenterToUnit")]
public void SetScreenCenterToUnit(int unitId)
```

---

### `SetScreenCenterToBuilding`

Convenience wrapper that centers the camera on a building.

```csharp
[LuaApiExport("Player_SetScreenCenterToBuilding")]
public void SetScreenCenterToBuilding(int buildingId)
```

---

### `SetScreenCenterToProjectile`

Convenience wrapper that centers the camera on a projectile.

```csharp
[LuaApiExport("Player_SetScreenCenterToProjectile")]
public void SetScreenCenterToProjectile(int projectileId)
```

---

### `GetZoom` / `SetZoom`

```csharp
public float GetZoom()

[LuaApiExport("Player_SetZoom")]
public void SetZoom(float scale)
```

---

### `GetCameraMoveSpeed` / `SetCameraMoveSpeed`

```csharp
public float GetCameraMoveSpeed()

[LuaApiExport("Player_SetCameraMoveSpeed")]
public void SetCameraMoveSpeed(float speed)
```

---

### `GetCameraZoomSpeed` / `SetCameraZoomSpeed`

```csharp
public float GetCameraZoomSpeed()

[LuaApiExport("Player_SetCameraZoomSpeed")]
public void SetCameraZoomSpeed(float speed)
```

---

### `IsCameraControlsDisabled` / `SetCameraControlsEnabled`

```csharp
public bool IsCameraControlsDisabled()

[LuaApiExport("Player_SetCameraControlsEnabled")]
public void SetCameraControlsEnabled(bool enabled)
```

---

### `GetCurrentRotation`

```csharp
public Dircs GetCurrentRotation()
```

---

### `GetRotationCentre`

```csharp
public UnmanagedVector2<int> GetRotationCentre()
```

---

### `SetMapRotation`

```csharp
[LuaApiExport("Player_SetMapRotation")]
public void SetMapRotation(Dircs rotation, int centreX = -1, int centreY = -1, bool force = false)
```

---

### `RotateMapLeft` / `RotateMapRight`

```csharp
[LuaApiExport("Player_RotateMapLeft")]
public void RotateMapLeft()

[LuaApiExport("Player_RotateMapRight")]
public void RotateMapRight()
```

---

### `IsMapLocked` / `ToggleMapLocked`

When unlocked, the camera can move beyond map boundaries. Useful for developer tools or cinematic sequences.

```csharp
public bool IsMapLocked()

[LuaApiExport("Player_ToggleMapLocked")]
public void ToggleMapLocked()
```

---

### `ToggleHealthBars`

Toggles visibility of health bars above units and buildings.

```csharp
[LuaApiExport("Player_ToggleHealthBars")]
public void ToggleHealthBars()
```

---

## Methods — Cursor & Selection

### `GetMousePosition`

Gets the local tile coordinate under the mouse cursor.

```csharp
[LuaApiExport("Player_GetMousePosition")]
public UnmanagedVector2<uint> GetMousePosition()
```

---

### `IsCursorInGame`

Checks if the cursor is in the playable game area (not over UI).

```csharp
[LuaApiExport("Player_IsCursorInGame")]
public bool IsCursorInGame()
```

---

### `GetHoveredTileId` / `GetHoveredUnitId` / `GetHoveredBuildingId` / `GetHoveredBuildingTileId`

```csharp
[LuaApiExport("Player_GetHoveredTileId")]
public int GetHoveredTileId()

[LuaApiExport("Player_GetHoveredUnitId")]
public int GetHoveredUnitId()

[LuaApiExport("Player_GetHoveredBuildingId")]
public int GetHoveredBuildingId()

[LuaApiExport("Player_GetHoveredBuildingTileId")]
public int GetHoveredBuildingTileId()
```

---

### `GetHoveredChimpsCount`

```csharp
[LuaApiExport("Player_GetHoveredUnitCount")]
public int GetHoveredChimpsCount()
```

---

### `GetSelectedChimps` / `GetSelectedChimpsCount`

```csharp
[LuaApiExport("Player_GetSelectedUnits")]
public int[] GetSelectedChimps()

[LuaApiExport("Player_GetSelectedUnitCount")]
public int GetSelectedChimpsCount()
```

---

### `GetSelectedBuildingId`

```csharp
[LuaApiExport("Player_GetSelectedBuildingId")]
public int GetSelectedBuildingId()
```

**Returns:** Building ID, or `0` if none selected (may retain last known selection).

---

## Methods — Map & Engine

### `GetCurrentMapName`

Retrieves the name of the currently active map. Does not contain the full file path. May or may not include the `.map` extension.

```csharp
[LuaApiExport("Player_GetCurrentMapName")]
public string GetCurrentMapName()
```

---

### `IsInMapEditor`

```csharp
[LuaApiExport("Player_IsInMapEditor")]
public bool IsInMapEditor()
```

---

### `GetFrameTime` / `SetFrameTime`

Gets/sets the local frame time (tick rate) for the native engine.

> **Warning:** Do not call `SetFrameTime` in multiplayer.

```csharp
public double GetFrameTime()

[LuaApiExport("Player_SetFrameTime")]
public void SetFrameTime(double fps)
```

---

## Methods — Noesis GUI

### `IsOverNoesisGUI`

```csharp
public bool IsOverNoesisGUI()
```

---

### `GetNoesisHasKeyboard` / `SetNoesisHasKeyboard`

```csharp
public bool GetNoesisHasKeyboard()

[LuaApiExport("Player_SetNoesisHasKeyboard")]
public void SetNoesisHasKeyboard(bool hasKeyboard)
```

---

## Methods — Query System

### `QueryPlayerResources`

Begins a high-performance query over all possible player resource slots.

```csharp
public GameStructQuery<GamePlayerResources> QueryPlayerResources()
```

---

### `ExecuteQuery`

Core query execution method. All public query functions delegate to this.

```csharp
public void ExecuteQuery(List<int> results, RefPredicate<GamePlayerResources> basePredicate)
```

---

### `GetAllPlayerResources`

Fills a list with IDs of all player resources.

```csharp
public void GetAllPlayerResources(List<int> results)
```

---

### `GetPlayerResources`

Gets a native pointer to the game player resource manager.

```csharp
public SimpleNativeArray<GamePlayerResources> GetPlayerResources()
```

---

### `GetCursorManager`

```csharp
public NativePointer<GameCursorManager> GetCursorManager()
```

---

## Low-Level Access

### `TryGetPlayerResourcesById`

Retrieves a direct raw pointer to a player's resource data.

```csharp
public bool TryGetPlayerResourcesById(int playerId, out GamePlayerResources* resources)
```

---

### `TryGetPlayerResourcesByIdEx`

Retrieves a safe wrapped pointer to a player's resource data.

```csharp
public bool TryGetPlayerResourcesByIdEx(int playerId, out NativePointer<GamePlayerResources> resources)
```
