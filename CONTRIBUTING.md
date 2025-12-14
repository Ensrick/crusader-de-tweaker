# Contributing to Crusader DE Tweaker

This document is designed to help both human developers and AI assistants understand the project structure, architecture, and contribution guidelines.

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Key Systems](#key-systems)
4. [Configuration System](#configuration-system)
5. [Damage Calculation System](#damage-calculation-system)
6. [Development Workflow](#development-workflow)
7. [Code Style and Conventions](#code-style-and-conventions)
8. [Testing and Validation](#testing-and-validation)

## Project Overview

**Crusader DE Tweaker** is a BepInEx 5 plugin for Stronghold Crusader: Definitive Edition that allows users to modify unit and structure properties through configuration files. The mod uses the **Stronghold Crusader DE Script Extender (SHCDE-SE)** API to access game internals.

### Core Purpose

The mod provides two main configuration mechanisms:

1. **TOML Configuration Files**: Primary method for configuring unit/structure stats (health, cost, damage, armor, tags)
2. **CSV Damage Matrices**: Reference files that can be edited to override TOML damage calculations
3. **BepInEx Config**: Real-time multipliers via event hooks (damage taken, health, etc.)

### Dependencies

- **BepInEx 5**: Plugin framework
- **SHCDE-SE**: Script extender API (provides game internals access)
- **Tomlyn**: TOML file parsing library
- **Unity Engine**: Game engine (via SHCDE-SE)

## Architecture

### High-Level Flow

```
Game Start
    ↓
Plugin.Awake() → Wait for SHCDE-SE Library Load
    ↓
ConfigManager.Initialize()
    ├─→ Generate default TOML/CSV files
    ├─→ Load TOML configs (Units, Structures, Armor)
    ├─→ Load CSV damage matrices (if modified)
    └─→ Validate all configs
    ↓
ConfigManagerBepinex.Initialize()
    └─→ Apply real-time hooks (multipliers)
    ↓
Game Running → Event hooks intercept damage/health changes
```

### Directory Structure

```
CrusaderDETweaker/
├── Config/
│   ├── BepInEx/              # Real-time multiplier hooks
│   ├── Core/                 # IConfigSystem interface
│   ├── DamageMatrix/         # CSV damage matrix system
│   │   ├── Core/            # CSV helpers, loaders, generators
│   │   ├── Melee/           # Melee damage matrix
│   │   ├── Ranged/          # Ranged damage matrix
│   │   └── EunuchAoe/       # Area-of-effect damage
│   └── Toml/                 # TOML configuration system
│       ├── Armor/           # Armor configuration
│       ├── Core/            # Property handlers, registries
│       ├── Structures/      # Structure properties
│       ├── Units/           # Unit properties
│       └── Systems/          # Config system implementations
├── Data/                     # Data models
│   ├── UnitDamageData.cs    # Unit damage/armor/tags model
│   ├── ProjectileDamageData.cs
│   └── UnitCategories.cs
├── Systems/                   # Core game systems
│   ├── DamageCalculator.cs  # Main damage calculation logic
│   ├── UnitDamageRegistry.cs # Unit data registry
│   ├── StatsStructures.cs    # Structure stat modifications
│   └── Verification/        # Damage verification system
├── Output/                   # Generated config files (gitignored)
└── Plugin.cs                # Main plugin entry point
```

## Key Systems

### 1. Configuration System (`ConfigManager`)

The unified configuration system uses the `IConfigSystem` interface to manage multiple config types:

- **UnitConfigSystem**: Handles unit properties (health, speed, cost, damage, armor, tags)
- **StructureConfigSystem**: Handles structure properties (health, cost, housing)
- **ArmorConfigSystem**: Handles armor category definitions
- **DamageMatrixConfigSystem**: Handles CSV damage matrix loading

**Key Pattern**: All config systems implement `IConfigSystem` with:
- `GenerateDefaults()`: Create default config files
- `Load()`: Load and apply configs
- `Validate()`: Verify config correctness

### 2. Property Registry System

The TOML system uses a registry pattern for extensible property handling:

- **UnitPropertyRegistry**: Registers handlers for unit properties
- **StructurePropertyRegistry**: Registers handlers for structure properties

Each property has a handler class that:
- Defines the property name
- Reads from game API
- Writes to game API
- Validates values

**Example**: `BaseMeleeDamageProperty` handles the `BaseMeleeDamage` property for units.

### 3. Damage Calculation System (`DamageCalculator`)

The damage system is the most complex part of the mod. It calculates melee damage using:

**Formula**: `BaseDamage × WeaponVsArmorMultiplier × ArmorValue × TagModifiers × SpecialModifiers`

**Key Components**:

1. **Weapon Categories**: Sword, Mace, Polearm, Lance, Axe, Dagger, Ranged, Beast, Unarmed
2. **Armor Categories**: None, Light, Medium, Heavy, Siege
3. **Weapon vs Armor Table**: Lookup table mapping (Weapon, Armor) → multiplier
4. **Tag Modifiers**: Special multipliers for tag combinations (e.g., Polearm vs Ladderman = 5x)
5. **Unit-Specific Modifiers**: Overrides for specific unit pairs

**Special Cases**:
- Weak attackers (base ≤ 2): Deal flat damage, ignore all modifiers
- Assassin: Uses special formula (not lookup table)
- Ranged in melee: Special calculation with caps
- Beast units: Large beasts ignore armor, small beasts have caps
- SiegeDefense: Trebuchet's special defense, bypassed by strong melee

### 4. Damage Matrix System (CSV)

CSV files serve as:
- **Reference**: Show actual game damage values
- **Override**: Users can edit CSV to override TOML calculations
- **Verification**: Compare calculated vs actual damage

**CSV Files**:
- `CrusaderDETweaker_MeleeDamage.csv`: Melee damage matrix
- `CrusaderDETweaker_RangedDamage.csv`: Ranged damage matrix
- `CrusaderDETweaker_EunuchAoeDamage.csv`: Area-of-effect damage

**Loading Logic**:
1. Load CSV if it exists
2. Compare each value to calculated damage from TOML
3. Only apply CSV values that differ from calculated
4. This allows users to fine-tune specific unit pairs

### 5. BepInEx Multiplier System

Real-time multipliers via event hooks:

- **UnitMeleeDamageTakenMultiplier**: Global multiplier for all melee damage to units
- **StructureDamageTakenMultiplier**: Global multiplier for all damage to structures
- **WallDamageTakenMultiplier**: Multiplier for damage to walls (stone, crenel, wood walls)
- **TowerDamageTakenMultiplier**: Multiplier for damage to towers (tower levels 1-5)
- **WoodenStructureDamageTakenMultiplier**: Multiplier for damage to wooden structures (structures with wood cost > 0 and stone cost = 0)
- **UnitHealthMultiplier**: Global multiplier for unit max health

**Implementation**: Uses R3 event hooks (`UnitR3EventHooks`, `BuildingR3EventHooks`) to intercept damage/health events before they're applied.

**Multiplier Stacking**: Structure multipliers stack multiplicatively. For example, a wooden wall will have both `WallDamageTakenMultiplier` and `WoodenStructureDamageTakenMultiplier` applied, then `StructureDamageTakenMultiplier`.

**Validation**: All multipliers are validated on initialization. Negative values will log a warning but are allowed (may cause unexpected behavior).

## Configuration System

### TOML File Structure

**Units** (`CrusaderDETweaker_Units.toml`):
```toml
[CHIMP_TYPE_SWORDSMAN]
Health = 100
Speed = 1.0
GoldCost = 50
BaseMeleeDamage = 100
ArmorValue = 0.5
Tags = ["Weapon_Sword", "Armor_Heavy"]
```

**Structures** (`CrusaderDETweaker_Structures.toml`):
```toml
[STRUCT_BARRACKS_STONE]
Health = 500
GoldCost = 0
WoodCost = 0
StoneCost = 15
IronCost = 0
PitchCost = 0
HousingPopulationSpace = 0
```

**Armor** (`CrusaderDETweaker_Armor.toml`):
```toml
[Armor_Heavy]
ArmorValue = 0.5
Description = "Heavy armor - takes 50% damage"
```

### CSV File Structure

**Melee Damage Matrix**:
```csv
Attacker,Defender,Damage
CHIMP_TYPE_SWORDSMAN,CHIMP_TYPE_KNIGHT,50
CHIMP_TYPE_SWORDSMAN,CHIMP_TYPE_ARAB_SLAVE,200
```

**Override Logic**:
- If CSV value matches calculated → use calculated (TOML)
- If CSV value differs → use CSV value (override)

## Damage Calculation System

### How Damage is Calculated

1. **Get Unit Data**: Retrieve `UnitDamageData` for attacker and defender
2. **Check Weak Attacker**: If base damage ≤ 2, return flat damage
3. **Get Categories**: Determine `WeaponCategory` and `ArmorCategory` from tags
4. **Check SiegeDefense**: Handle special Trebuchet defense rules
5. **Calculate Base Damage**: Apply weapon vs armor multiplier and armor value
6. **Apply Special Cases**: Handle Assassin, Ranged, Unarmed, Beast separately
7. **Apply Tag Modifiers**: Check for tag vs tag multipliers
8. **Apply Unit Modifiers**: Check for unit-specific overrides
9. **Apply Caps**: Limit damage based on weapon type and armor
10. **Apply Minimum**: Ensure damage ≥ 2

### Key Data Structures

**UnitDamageData**:
- `BaseMeleeDamage`: Base damage output
- `ArmorValue`: Damage taken multiplier (0.5 = heavy, 1.0 = normal, 2.0 = unarmored)
- `Tags`: HashSet of tags (Weapon_*, Armor_*, etc.)
- `SpecialModifiers`: Dictionary of unit-specific multipliers

**WeaponVsArmorTable**:
- Maps `(WeaponCategory, ArmorCategory)` → `float` multiplier
- Derived from game data analysis

**TagVsTagModifiers**:
- Maps `(attackerTag, defenderTag)` → `float` multiplier
- Examples: `("Weapon_Polearm", "Ladderman")` → 5.0

## Development Workflow

### Setting Up Development Environment

1. Install Stronghold Crusader: Definitive Edition
2. Install BepInEx 5 to game directory
3. Install SHCDE-SE to game directory
4. Clone this repository
5. Open `CrusaderDETweaker.sln` in Visual Studio
6. Update project references to point to game directory DLLs

### Making Changes

1. **Adding a New Property**:
   - Create property handler class in `Config/Toml/Units/Properties/` or `Config/Toml/Structures/Properties/`
   - Inherit from `PropertyHandler<TKey, TValue>`
   - Register in `UnitPropertyRegistry` or `StructurePropertyRegistry`
   - Add to TOML generator/loader

2. **Modifying Damage Calculation**:
   - Edit `Systems/DamageCalculator.cs`
   - Update `WeaponVsArmorTable` or `TagVsTagModifiers` if needed
   - Run verification tests to ensure accuracy

3. **Adding a New Multiplier Hook**:
   - Add `ConfigEntry` in `ConfigManagerBepinex`
   - Subscribe to appropriate event hook in `ApplyAllMultiplierConfigs()`
   - Modify event args in the subscription

### Testing

1. **Damage Verification**:
   - Run `DamageVerificationManager.VerifyAll()`
   - Check `CrusaderDETweaker_MeleeMismatches.csv` for discrepancies
   - Fix calculation logic if mismatches found

2. **Config Validation**:
   - All config systems implement `Validate()`
   - Check logs for validation errors

3. **Manual Testing**:
   - Load game with mod
   - Test specific unit interactions
   - Verify TOML changes are applied
   - Verify CSV overrides work

## Code Style and Conventions

### Naming Conventions

- **Classes**: PascalCase (e.g., `DamageCalculator`)
- **Methods**: PascalCase (e.g., `CalculateMeleeDamage`)
- **Private fields**: `_camelCase` (e.g., `_registry`)
- **Properties**: PascalCase (e.g., `BaseMeleeDamage`)
- **Constants**: PascalCase (e.g., `MinimumDamage`)

### Code Organization

- **One class per file**: File name matches class name
- **Namespaces**: Follow directory structure
- **XML Documentation**: All public/internal classes and methods should have `<summary>` tags
- **Comments**: Use comments for complex logic, especially in `DamageCalculator`

### Error Handling

- Use try-catch blocks for API calls (API may fail)
- Log errors with `Plugin.Logger.LogError()`
- Return safe defaults when possible (e.g., `1.0f` for multipliers)

### Performance Considerations

- Cache API instances in `Plugin` class
- Lazy initialization for registries
- Avoid repeated API calls in hot paths (damage calculation)

## Testing and Validation

### Damage Verification System

The verification system compares calculated damage to actual game values:

1. **MeleeDamageVerifier**: Verifies melee damage calculations
2. **RangedDamageVerifier**: Verifies ranged damage calculations
3. **EunuchAoeDamageVerifier**: Verifies area-of-effect damage

**Usage**:
```csharp
DamageVerificationManager.VerifyAll();
// Results written to CSV files
```

### Config Validation

Each config system can validate its data:

- **UnitConfigSystem**: Validates unit properties exist and are in valid ranges
- **StructureConfigSystem**: Validates structure properties
- **DamageMatrixConfigSystem**: Runs damage verification

### Debugging Tips

1. **Enable Debug Logging**: Set BepInEx log level to Debug
2. **Check Verification Results**: Review mismatch CSV files
3. **Use Diagnostic Methods**: `DamageCalculator.DiagnoseMeleeDamage()` for specific unit pairs
4. **Test Incrementally**: Test one property change at a time

## Common Tasks

### Adding a New Unit Property

1. Create property handler: `Config/Toml/Units/Properties/NewProperty.cs`
2. Inherit from `PropertyHandler<eChimps, TValue>`
3. Implement `TryGetFromAPI()` and `SetToAPI()`
4. Register in `UnitPropertyRegistry.Initialize()`
5. Add to TOML generator/loader

### Adding a New Damage Tag

1. Add tag to `UnitDamageData` in TOML
2. Update `DamageCalculator.GetWeaponCategory()` or `GetArmorCategory()` if needed
3. Add to `TagVsTagModifiers` if it needs special multiplier
4. Update documentation in `UnitDamageData.cs`

### Adding a New Multiplier Hook

1. Add `ConfigEntry` in `ConfigManagerBepinex.Initialize()`
2. Add subscription in `ConfigManagerBepinex.ApplyAllMultiplierConfigs()`
3. Modify event args in subscription
4. Test with different multiplier values

## Resources

- **BepInEx Documentation**: https://docs.bepinex.dev/
- **SHCDE-SE Repository**: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender
- **Tomlyn Documentation**: https://github.com/xoofx/Tomlyn

## Questions?

If you have questions about the codebase:
1. Check this document first
2. Review code comments (especially in `DamageCalculator.cs`)
3. Check existing implementations for examples
4. Open an issue on GitLab

