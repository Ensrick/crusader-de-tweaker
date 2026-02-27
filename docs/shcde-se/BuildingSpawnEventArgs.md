# BuildingSpawnEventArgs

**Namespace:** `SHCDESE.EventAPI.Buildings`
**Assembly:** `SHCDESE.dll`

Fired by `BuildingR3EventHooks.OnBuildingSpawn` when a building is spawned.

```csharp
public class BuildingSpawnEventArgs : EventHookBase
```

## Inheritance

`object` → `EventArgs` → `EventHookBase` → `BuildingSpawnEventArgs`

## Inherited Members

| Member | Source |
|--------|--------|
| `Phase` | `EventHookBase` |
| `SkipOriginalFunction` | `EventHookBase` |

---

## Constructor

```csharp
public BuildingSpawnEventArgs(
    EventHookPhase phase,
    NativePointer<GameBuildingManager> pBuildingManager,
    int playerId,
    int tileX,
    int tileY,
    short heightElevation,
    eStructs building,
    int buildingScale,
    int visualPlayerId,
    int spriteVariationIndex)
```

---

## Properties

### `Building`

The type of building being spawned.

```csharp
public eStructs Building { get; set; }
```

---

### `BuildingManager`

```csharp
public NativePointer<GameBuildingManager> BuildingManager { get; }
```

---

### `BuildingScale`

```csharp
public int BuildingScale { get; set; }
```

---

### `HeightElevation`

```csharp
public short HeightElevation { get; set; }
```

---

### `PlayerId`

The player who owns the building being spawned.

```csharp
public int PlayerId { get; set; }
```

---

### `ReturnValue`

The building's instance ID. Available in the **Post** phase only.

```csharp
public long ReturnValue { get; set; }
```

---

### `SpriteVariationIndex`

```csharp
public int SpriteVariationIndex { get; set; }
```

---

### `TileX` / `TileY`

Top-left tile coordinates of the building.

```csharp
public int TileX { get; set; }
public int TileY { get; set; }
```

---

### `VisualPlayerId`

The player ID used to determine the building's colour scheme.

```csharp
public int VisualPlayerId { get; set; }
```

---

## Usage Example

```csharp
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;

BuildingR3EventHooks.OnBuildingSpawn.Observable
    .Where(args => args.Phase == EventHookPhase.Post
                && args.Building == eStructs.STRUCT_TRADEPOST
                && args.PlayerId == Plugin.PlayerApi.GetLocalPlayerId())
    .Subscribe(args =>
    {
        int buildingId = (int)args.ReturnValue;
        // Market is now active for the local player
    });
```
