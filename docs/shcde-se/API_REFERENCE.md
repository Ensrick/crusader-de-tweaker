# SHCDE-SE API Reference for Crusader DE Tweaker

This document provides a comprehensive reference for the SHCDE-SE (Stronghold Crusader DE Script Extender) APIs used in Crusader DE Tweaker. It's designed for both human developers and AI assistants to quickly understand available APIs and their usage.

## Table of Contents

1. [Overview](#overview)
2. [Initialization](#initialization)
3. [GameUnitManagerAPI](#gameunitmanagerapi)
4. [GameBuildingManagerAPI](#gamebuildingmanagerapi)
5. [Event Hooks](#event-hooks)
6. [Enums and Types](#enums-and-types)
7. [Usage Examples](#usage-examples)
8. [Common Patterns](#common-patterns)

## Overview

Crusader DE Tweaker uses the following SHCDE-SE APIs:

- **GameUnitManagerAPI**: Unit management, stats, damage tables
- **GameBuildingManagerAPI**: Building management, costs, health
- **Event Hooks (R3)**: Real-time event subscriptions for damage, creation, etc.
- **Enums**: `eChimps` (unit types), `eStructs` (building types), etc.

All APIs are accessed through singleton instances accessed via `.Instance` property.

## Initialization

### Library Load Event

SHCDE-SE APIs are not available until the game library is loaded. Subscribe to the library load event:

```csharp
using SHCDESE.API.LowLevel;

// In Plugin.Awake()
CrusaderLibrary.Instance.LibraryLoaded += _ => OnLibraryLoaded();

private void OnLibraryLoaded()
{
    // APIs are now available
    var unitApi = GameUnitManagerAPI.Instance;
    var buildingApi = GameBuildingManagerAPI.Instance;
}
```

### Accessing APIs

```csharp
// Get singleton instances
GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
```

## GameUnitManagerAPI

The primary API for unit-related operations.

### Singleton Access

```csharp
GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
```

### Unit Information Methods

#### GetType
Gets the unit type (`eChimps`) for a unit ID.

```csharp
eChimps GetType(int unitId)
```

**Example:**
```csharp
eChimps unitType = Plugin.UnitApi.GetType(unitId);
if (unitType == eChimps.CHIMP_TYPE_SWORDSMAN)
{
    // Handle swordsman
}
```

**Used in:** `Config/BepInEx/Systems/Handlers/MeleeDamageMultiplierHandler.cs` - Getting attacker/defender types for damage hooks

#### GetMaxHealth / SetMaxHealth
Gets or sets the maximum health for a unit.

```csharp
int GetMaxHealth(int unitId)
void SetMaxHealth(int unitId, int maxHealth)
```

**Example:**
```csharp
int currentMaxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
int newMaxHealth = (int)(currentMaxHealth * 1.5f); // 50% increase
Plugin.UnitApi.SetMaxHealth(unitId, newMaxHealth);
```

**Used in:** `Config/BepInEx/Systems/Handlers/HealthMultiplierHandler.cs` - Applying health multipliers

#### GetCurrentHealth / SetCurrentHealth
Gets or sets the current health for a unit.

```csharp
void SetCurrentHealth(int unitId, int health)
```

**Example:**
```csharp
// Set current health to max (full heal)
int maxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
Plugin.UnitApi.SetCurrentHealth(unitId, maxHealth);
```

**Used in:** `Config/BepInEx/Systems/Handlers/HealthMultiplierHandler.cs` - Setting health after multiplier applied

#### GetDefaultHealth / SetDefaultHealth
Gets or sets the default (template) health for a unit type. This affects all units of that type.

```csharp
UInt32 GetDefaultHealth(eChimps chimp)
void SetDefaultHealth(eChimps chimp, UInt32 value)
```

**Example:**
```csharp
// Set default health for all Swordsmen
uint defaultHealth = Plugin.UnitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_SWORDSMAN);
Plugin.UnitApi.SetDefaultHealth(eChimps.CHIMP_TYPE_SWORDSMAN, 150);
```

**Used in:** `Config/Toml/Units/Properties/HealthProperty.cs` - Setting unit health from TOML

#### GetDefaultSpeed / SetDefaultSpeed
Gets or sets the default speed for a unit type. Lower values = faster movement.

```csharp
UInt16 GetDefaultSpeed(eChimps chimp)
void SetDefaultSpeed(eChimps chimp, UInt16 value)
```

**Note:** Speed is clamped between 0 and 6. Lower values mean faster movement.

**Example:**
```csharp
// Make Swordsmen faster (lower speed value)
ushort currentSpeed = Plugin.UnitApi.GetDefaultSpeed(eChimps.CHIMP_TYPE_SWORDSMAN);
Plugin.UnitApi.SetDefaultSpeed(eChimps.CHIMP_TYPE_SWORDSMAN, (ushort)(currentSpeed - 1));
```

**Used in:** `Config/Toml/Units/Properties/SpeedProperty.cs` - Setting unit speed from TOML

#### GetSpeed / SetSpeed
Gets or sets the current speed for a specific unit instance.

```csharp
int GetSpeed(int unitId)
void SetSpeed(int unitId, ushort speedLevel)
```

### Damage Methods

#### GetMeleeDamageFromTo
Gets the melee damage that one unit type deals to another unit type. This reads from the game's damage lookup table.

```csharp
int GetMeleeDamageFromTo(eChimps source, eChimps target)
```

**Example:**
```csharp
// Get damage Swordsman deals to Knight
int damage = Plugin.UnitApi.GetMeleeDamageFromTo(
    eChimps.CHIMP_TYPE_SWORDSMAN,
    eChimps.CHIMP_TYPE_KNIGHT
);
// Returns: 50 (from game's damage table)
```

**Used in:** `Config/DamageMatrix/Melee/MeleeDamageMatrixLoader.cs` - Reading base damage values from game's lookup table

**Note:** This reads from the game's internal damage table. The mod's `DamageCalculator` provides calculated damage based on TOML config, which may differ.

### Cost Methods

#### GetGoldCost / SetGoldCost
Gets or sets the gold cost for a unit type.

```csharp
int GetGoldCost(eChimps chimp)
void SetGoldCost(eChimps chimp, int value)
```

**Example:**
```csharp
// Get and modify gold cost
int currentCost = Plugin.UnitApi.GetGoldCost(eChimps.CHIMP_TYPE_SWORDSMAN);
Plugin.UnitApi.SetGoldCost(eChimps.CHIMP_TYPE_SWORDSMAN, currentCost * 2); // Double cost
```

**Used in:** `Config/Toml/Units/Properties/GoldCostProperty.cs` - Setting unit gold cost from TOML

### Resource Cost Methods

#### GetResourceTypeCost / SetResourceTypeCost
Gets or sets resource type costs for units (wood, stone, iron, pitch).

```csharp
// Methods exist for each resource type
UnitGoodCosts GetResourceTypeCost(eChimps chimp)
void SetResourceTypeCost(eChimps chimp, UnitGoodCosts cost)
```

**Used in:** `Config/Toml/Units/Properties/ResourceTypeProperties.cs` - Setting unit resource costs from TOML

### Damage Lookup Tables

The API provides access to damage lookup tables:

```csharp
// Melee damage table (unit vs unit)
DamageLookupTable MeleeDamageLookupTable { get; }

// Ranged damage tables (internal, accessed via array)
// _rangedArrowDamageArray
// _rangedBoltDamageArray
// _rangedSlingerDamageArray
// _rangedJavelinDamageArray
```

**Note:** These are internal arrays. Use `GetMeleeDamageFromTo()` for public access.

## GameBuildingManagerAPI

The primary API for building-related operations.

### Singleton Access

```csharp
GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
```

### Building Information Methods

#### GetType
Gets the building type (`eStructs`) for a building ID.

```csharp
eStructs GetType(int buildingId)
```

**Example:**
```csharp
eStructs buildingType = Plugin.BuildingApi.GetType(buildingId);
if (buildingType == eStructs.STRUCT_BARRACKS_STONE)
{
    // Handle barracks
}
```

**Used in:** Feature roadmap - Identifying structure types for multipliers

#### GetHealth / SetHealth
Gets or sets the current health for a building.

```csharp
int GetHealth(int buildingId)
void SetHealth(int buildingId, Int16 health)
```

**Example:**
```csharp
int currentHealth = Plugin.BuildingApi.GetHealth(buildingId);
Plugin.BuildingApi.SetHealth(buildingId, (short)(currentHealth + 100)); // Heal 100 HP
```

**Used in:** `Config/Toml/Structures/Properties/StructureHealthProperty.cs` - Setting building health from TOML

#### GetMaxHealth / SetMaxHealth
Gets or sets the maximum health for a building.

```csharp
int GetMaxHealth(int buildingId)
void SetMaxHealth(int buildingId, UInt16 health)
```

**Example:**
```csharp
int maxHealth = Plugin.BuildingApi.GetMaxHealth(buildingId);
Plugin.BuildingApi.SetMaxHealth(buildingId, (ushort)(maxHealth * 2)); // Double max health
```

**Used in:** `Config/Toml/Structures/Properties/StructureHealthProperty.cs` - Setting building max health from TOML

### Cost Methods

#### GetDefaultCost / SetDefaultCost
Gets or sets the default cost for a building type. This affects all buildings of that type.

```csharp
BuildingCost GetDefaultCost(eStructs building)
void SetDefaultCost(eStructs building, BuildingCost newCost, bool updateUnityEngineSite = true)
```

**BuildingCost Structure:**
```csharp
public struct BuildingCost
{
    public int Gold;
    public int Wood;
    public int Stone;
    public int Iron;
    public int Pitch;
}
```

**Example:**
```csharp
// Get current cost
BuildingCost cost = Plugin.BuildingApi.GetDefaultCost(eStructs.STRUCT_BARRACKS_STONE);

// Modify cost
cost.Stone = 30; // Double stone cost
Plugin.BuildingApi.SetDefaultCost(eStructs.STRUCT_BARRACKS_STONE, cost);
```

**Used in:** `Config/Toml/Structures/Properties/StructureCostProperties.cs` - Setting building costs from TOML

#### Individual Cost Getters/Setters
Individual methods exist for each resource type:

```csharp
int GetGoldCost(eStructs building)
void SetGoldCost(eStructs building, Int32 value)

int GetWoodCost(eStructs building)
void SetWoodCost(eStructs building, Int32 value)

int GetStoneCost(eStructs building)
void SetStoneCost(eStructs building, Int32 value)

int GetIronCost(eStructs building)
void SetIronCost(eStructs building, Int32 value)

int GetPitchCost(eStructs building)
void SetPitchCost(eStructs building, Int32 value)
```

**Used in:** `Config/Toml/Structures/Properties/StructureCostProperties.cs` - Individual cost property handlers

### Housing Methods

#### GetHousingPopulationSpace / SetHousingPopulationSpace
Gets or sets the housing population space for a building type.

```csharp
UInt16 GetHousingPopulationSpace(eStructs building)
void SetHousingPopulationSpace(eStructs building, UInt16 value)
```

**Example:**
```csharp
// Increase housing capacity
ushort currentSpace = Plugin.BuildingApi.GetHousingPopulationSpace(eStructs.STRUCT_HOVEL);
Plugin.BuildingApi.SetHousingPopulationSpace(eStructs.STRUCT_HOVEL, (ushort)(currentSpace + 4));
```

**Used in:** `Config/Toml/Structures/Properties/StructureHousingPopulationSpaceProperty.cs` - Setting housing space from TOML

### Wall Cost Methods

**Note:** Wall costs are handled separately. The API supports wall cost multipliers:

```csharp
// Internal methods (used by game)
internal static int GetLowWallCostMultiplierInternal()
internal static int GetHighWallCostMultiplierInternal()
```

**Status:** Wall cost modification is now supported by the API (as mentioned in feature roadmap).

## Event Hooks

SHCDE-SE uses R3 (Reactive Extensions) for event hooks. Events have two phases:
- **Pre**: Before the game's original logic executes (can modify parameters)
- **Post**: After the game's original logic executes (can read results)

### Unit Event Hooks

Located in `SHCDESE.EventAPI.UnitR3EventHooks`.

#### OnUnitTakeMeleeDamage
Fired when a unit takes damage from a melee attack. **Pre** phase allows modifying damage.

```csharp
UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
    .Where(args => args.Phase == EventHookPhase.Pre)
    .Subscribe(args =>
    {
        // args.Damage - damage amount (can be modified)
        // args.AttackingUnitId - ID of attacking unit
        // args.DamagedUnitId - ID of unit taking damage
        
        // Modify damage
        args.Damage = (int)(args.Damage * 1.5f); // 50% more damage
    });
```

**Event Args:**
```csharp
public class UnitTakeDamageByMeleeEventArgs : R3EventArgs
{
    public int AttackingUnitId { get; set; }
    public int DamagedUnitId { get; set; }
    public int Damage { get; set; } // Can be modified in Pre phase
}
```

**Used in:** `Config/BepInEx/Systems/Handlers/MeleeDamageMultiplierHandler.cs` - Applying `UnitMeleeDamageTakenMultiplier`

#### OnUnitCreate
Fired when a new unit is spawned. **Post** phase provides the new unit's ID.

```csharp
UnitR3EventHooks.OnUnitCreate.Observable
    .Where(args => args.Phase == EventHookPhase.Post)
    .Subscribe(args =>
    {
        // args.UnitType - type of unit created
        // args.ReturnValue - unit ID (in Post phase)
        
        int unitId = (int)args.ReturnValue;
        eChimps unitType = args.UnitType;
        
        // Apply modifications to newly created unit
        if (unitType == eChimps.CHIMP_TYPE_SWORDSMAN)
        {
            int maxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
            Plugin.UnitApi.SetMaxHealth(unitId, maxHealth * 2);
        }
    });
```

**Event Args:**
```csharp
public class UnitCreateEventArgs : R3EventArgs
{
    public eChimps UnitType { get; set; }
    public long ReturnValue { get; set; } // Unit ID (Post phase only)
}
```

**Used in:** `Config/BepInEx/Systems/Handlers/HealthMultiplierHandler.cs` - Applying `UnitHealthMultiplier` to newly created units; `Config/Toml/ConfigLoader.cs` - `RegisterSessionHooks()` links horse-requiring units to stable slots

#### OnUnitTakeProjectileDamage
Fired when a unit takes damage from a projectile (arrow, bolt, etc.).

```csharp
UnitR3EventHooks.OnUnitTakeProjectileDamage.Observable
    .Where(args => args.Phase == EventHookPhase.Pre)
    .Subscribe(args =>
    {
        // Modify projectile damage
        args.Damage = (int)(args.Damage * 0.5f); // 50% less damage
    });
```

**Note:** Not currently used in Crusader DE Tweaker, but available for future features.

### Building Event Hooks

Located in `SHCDESE.EventAPI.BuildingR3EventHooks`.

#### OnBuildingTileTakeDamage
Fired when a building's tile takes damage. **Pre** phase allows modifying damage.

```csharp
BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
    .Where(args => args.Phase == EventHookPhase.Pre)
    .Subscribe(args =>
    {
        // args.Damage - damage amount (can be modified)
        // args.BuildingId - ID of building taking damage
        
        // Modify damage
        args.Damage = (int)(args.Damage * 2.0f); // Double damage
    });
```

**Event Args:**
```csharp
public class BuildingTileTakeDamageEventArgs : R3EventArgs
{
    public int BuildingId { get; set; }
    public int Damage { get; set; } // Can be modified in Pre phase
    public int PlayerIdSource { get; set; } // Player causing damage
}
```

**Used in:** `Config/BepInEx/Systems/Handlers/StructureDamageMultiplierHandler.cs` - Applying structure damage multipliers

#### OnBuildingSpawn
Fired when a building is spawned. **Post** phase provides the building ID and owner.

Namespace: `SHCDESE.EventAPI.Buildings` — requires `using SHCDESE.EventAPI.Buildings;`

```csharp
BuildingR3EventHooks.OnBuildingSpawn.Observable
    .Where(args => args.Phase == EventHookPhase.Post
                && args.Building == eStructs.STRUCT_TRADEPOST
                && args.PlayerId == Plugin.PlayerApi.GetLocalPlayerId())
    .Subscribe(args =>
    {
        // args.Building   - eStructs type of the spawned building
        // args.PlayerId   - owner player ID
        // args.ReturnValue - building instance ID (Post phase)
        // args.Phase      - Pre or Post
        // args.TileX/Y    - tile position
        // args.BuildingScale, args.HeightElevation, args.VisualPlayerId, args.SpriteVariationIndex

        int buildingId = (int)args.ReturnValue;
    });
```

**Event Args:** `BuildingSpawnEventArgs` (see `docs/shcde-se/BuildingSpawnEventArgs.md`)

**Used in:** `Config/Toml/ConfigLoader.cs` - `RegisterSessionHooks()` applies auto-trade settings when the local player's marketplace (`STRUCT_TRADEPOST`) spawns

### Map Event Hooks

Located in `SHCDESE.EventAPI` — `MapLoaderR3EventHooks`.

#### OnStartMap
Fired after a new game session begins (map loaded, units placed). API calls that require an active session (e.g., `SetNoKnockdownWalls`, `SetAutoTrade`) must be deferred to this event.

```csharp
MapLoaderR3EventHooks.OnStartMap.Observable
    .Subscribe(_ =>
    {
        // Called once per map load. Re-apply all per-session settings here.
        Plugin.PlayerApi.SetNoKnockdownWalls(true);
    });
```

**Used in:** `Config/Toml/ConfigLoader.cs` - `RegisterSessionHooks()` re-applies all global config settings on each map load.

#### OnUnloadMap
Fired when a map is unloaded (game session ending or returning to menu).

```csharp
MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(e => { /* cleanup */ });
```

> SHCDE-SE uses this internally to clear override arrays (health defaults, housing, fire damage).

---

### Event Hook Phases

```csharp
public enum EventHookPhase
{
    Pre,   // Before game logic executes
    Post   // After game logic executes
}
```

**Important:**
- **Pre** phase: Can modify event args (e.g., `args.Damage`)
- **Post** phase: Can read results (e.g., `args.ReturnValue` for IDs)

## Enums and Types

### eChimps (Unit Types)

Enumeration of all unit types in the game.

**Common Units:**
```csharp
eChimps.CHIMP_TYPE_SWORDSMAN
eChimps.CHIMP_TYPE_KNIGHT
eChimps.CHIMP_TYPE_ARCHER
eChimps.CHIMP_TYPE_SPEARMAN
eChimps.CHIMP_TYPE_MACEMAN
eChimps.CHIMP_TYPE_PIKEMAN
eChimps.CHIMP_TYPE_XBOWMAN
eChimps.CHIMP_TYPE_LORD
// ... many more
```

**Special Units:**
```csharp
eChimps.CHIMP_TYPE_NULL  // Invalid/empty unit
eChimps.CHIMP_TYPE_PEASANT
eChimps.CHIMP_TYPE_ENGINEER
eChimps.CHIMP_TYPE_TUNNELER
// ... etc
```

**Used in:** Throughout the codebase for unit identification

### eStructs (Building Types)

Enumeration of all building types in the game.

**Common Buildings:**
```csharp
eStructs.STRUCT_BARRACKS_STONE
eStructs.STRUCT_ARMOURY
eStructs.STRUCT_HOVEL
eStructs.STRUCT_KEEP_ONE
eStructs.STRUCT_TOWER1
eStructs.STRUCT_TOWER2
eStructs.STRUCT_TOWER3
eStructs.STRUCT_TOWER4
// ... many more
```

**Walls:**
```csharp
eStructs.STRUCT_STONE_WALL
eStructs.STRUCT_CRENAL_WALL
eStructs.STRUCT_WOOD_WALL
```

**Special:**
```csharp
eStructs.STRUCT_NULL  // Invalid/empty building
eStructs.STRUCT_MAX   // Maximum enum value
```

**Used in:** Throughout the codebase for building identification

### BuildingCost

Structure representing building resource costs.

```csharp
public struct BuildingCost
{
    public int Gold;
    public int Wood;
    public int Stone;
    public int Iron;
    public int Pitch;
}
```

**Used in:** `Config/Toml/Structures/Properties/StructureCostProperties.cs`

### UnitGoodCosts

Structure representing unit resource costs.

```csharp
// Defined in SHCDE-SE, used for unit resource costs
```

**Used in:** `Config/Toml/Units/Properties/ResourceTypeProperties.cs`

## Usage Examples

### Example 1: Applying Health Multiplier to New Units

```csharp
// From Config/BepInEx/Systems/Handlers/HealthMultiplierHandler.cs
UnitR3EventHooks.OnUnitCreate.Observable
    .Where(args => args.Phase == EventHookPhase.Post)
    .Subscribe(args =>
    {
        if (Mathf.Approximately(UnitHealthMultiplier.Value, 1.0f))
            return;

        eChimps unitType = args.UnitType;
        
        // Skip non-modifiable units
        if (Data.UnitCategories.IsNonModifiable(unitType))
            return;

        try
        {
            int unitId = (int)args.ReturnValue;
            
            // Get current health and apply multiplier
            int currentMaxHealth = Plugin.UnitApi.GetMaxHealth(unitId);
            int newMaxHealth = Mathf.Max(1, (int)(currentMaxHealth * UnitHealthMultiplier.Value));
            
            Plugin.UnitApi.SetMaxHealth(unitId, newMaxHealth);
            Plugin.UnitApi.SetCurrentHealth(unitId, newMaxHealth);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Failed to apply health multiplier to {unitType}: {ex.Message}");
        }
    });
```

### Example 2: Modifying Unit Damage Taken

```csharp
// From Config/BepInEx/Systems/Handlers/MeleeDamageMultiplierHandler.cs
UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable
    .Where(args => args.Phase == EventHookPhase.Pre)
    .Subscribe(args =>
    {
        if (Mathf.Approximately(UnitMeleeDamageTakenMultiplier.Value, 1.0f))
            return;

        eChimps attacker = Plugin.UnitApi.GetType(args.AttackingUnitId);
        eChimps defender = Plugin.UnitApi.GetType(args.DamagedUnitId);

        // Get base damage (from game's table or event args)
        int baseDamage = args.Damage > 0
            ? args.Damage
            : Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);

        // Apply multiplier
        int modified = Mathf.Max(1, (int)(baseDamage * UnitMeleeDamageTakenMultiplier.Value));
        args.Damage = modified;
    });
```

### Example 3: Setting Building Cost from TOML

```csharp
// From Config/Toml/Structures/Properties/StructureCostProperties.cs
protected override void SetToAPI(eStructs structure, int value)
{
    var cost = Plugin.BuildingApi.GetDefaultCost(structure);
    SetCost(ref cost, value);  // Sets specific resource (Gold, Wood, etc.)
    Plugin.BuildingApi.SetDefaultCost(structure, cost);
}
```

### Example 4: Setting Unit Health from TOML

```csharp
// From Config/Toml/Units/Properties/HealthProperty.cs
protected override void SetToAPI(eChimps unit, uint value)
{
    Plugin.UnitApi.SetDefaultHealth(unit, value);
}
```

## Common Patterns

### Pattern 1: Event Hook Subscription

```csharp
// Standard pattern for event hooks
SomeEventHooks.OnSomeEvent.Observable
    .Where(args => args.Phase == EventHookPhase.Pre)  // or Post
    .Subscribe(args =>
    {
        // Modify args in Pre phase
        // Read results in Post phase
    });
```

### Pattern 2: API Access with Error Handling

```csharp
try
{
    // Get API instance
    var api = GameUnitManagerAPI.Instance;
    
    // Use API
    int health = api.GetMaxHealth(unitId);
    
    // Modify
    api.SetMaxHealth(unitId, health * 2);
}
catch (Exception ex)
{
    Plugin.Logger.LogError($"API call failed: {ex.Message}");
}
```

### Pattern 3: Type Checking Before Modification

```csharp
// Check if unit/building is modifiable
eChimps unitType = Plugin.UnitApi.GetType(unitId);
if (Data.UnitCategories.IsNonModifiable(unitType))
    return;  // Skip modification

eStructs buildingType = Plugin.BuildingApi.GetType(buildingId);
if (Data.StructureCategories.NonModable.Contains(buildingType))
    return;  // Skip modification
```

### Pattern 4: Getting Default Values

```csharp
// Get default value for unit type
uint defaultHealth = Plugin.UnitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_SWORDSMAN);
ushort defaultSpeed = Plugin.UnitApi.GetDefaultSpeed(eChimps.CHIMP_TYPE_SWORDSMAN);

// Get default value for building type
BuildingCost defaultCost = Plugin.BuildingApi.GetDefaultCost(eStructs.STRUCT_BARRACKS_STONE);
ushort defaultHousing = Plugin.BuildingApi.GetHousingPopulationSpace(eStructs.STRUCT_HOVEL);
```

## Important Notes

1. **API Availability**: APIs are only available after `CrusaderLibrary.Instance.LibraryLoaded` event fires.

2. **Thread Safety**: Most API calls should be made on the main game thread. Event hooks are already on the correct thread.

3. **Error Handling**: Always wrap API calls in try-catch blocks. APIs may return null or throw exceptions if the game state is invalid.

4. **ID Validity**: Always check if unit/building IDs are valid before using them. Use `TryGetUnitById` / `TryGetBuildingById` for safety.

5. **Default vs Instance Values**: 
   - `GetDefaultHealth()` / `SetDefaultHealth()` affects the template (all units of that type)
   - `GetMaxHealth()` / `SetMaxHealth()` affects a specific unit instance

6. **Event Hook Phases**:
   - **Pre**: Modify parameters before game logic
   - **Post**: Read results after game logic

7. **Damage Tables**: The game has multiple damage tables:
   - Melee damage (unit vs unit)
   - Ranged damage (projectile types)
   - The mod's `DamageCalculator` provides calculated damage based on TOML config

## Additional Resources

- **SHCDE-SE Documentation**: See `shcde-script-extender-main/docs/guides/` for more detailed API documentation
- **SHCDE-SE Source**: See `shcde-script-extender-main/src/SHCDESE.BepInEx/API/` for full API implementations
- **Event API**: See `shcde-script-extender-main/src/SHCDESE.BepInEx/EventAPI/` for all available events

## Quick Reference

### Most Used Methods

**Units:**
- `GetType(int unitId)` - Get unit type
- `GetMaxHealth(int unitId)` / `SetMaxHealth(int unitId, int health)` - Health
- `GetDefaultHealth(eChimps chimp)` / `SetDefaultHealth(eChimps chimp, uint value)` - Default health
- `GetDefaultSpeed(eChimps chimp)` / `SetDefaultSpeed(eChimps chimp, ushort value)` - Default speed
- `GetGoldCost(eChimps chimp)` / `SetGoldCost(eChimps chimp, int value)` - Gold cost
- `GetMeleeDamageFromTo(eChimps source, eChimps target)` - Damage lookup

**Buildings:**
- `GetType(int buildingId)` - Get building type
- `GetHealth(int buildingId)` / `SetHealth(int buildingId, short health)` - Health
- `GetMaxHealth(int buildingId)` / `SetMaxHealth(int buildingId, ushort health)` - Max health
- `GetDefaultCost(eStructs building)` / `SetDefaultCost(eStructs building, BuildingCost cost)` - Costs

**Events:**
- `UnitR3EventHooks.OnUnitTakeMeleeDamage` - Unit melee damage
- `UnitR3EventHooks.OnUnitCreate` - Unit creation
- `BuildingR3EventHooks.OnBuildingTileTakeDamage` - Building damage

