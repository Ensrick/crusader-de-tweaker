# CLAUDE.MD - AI Agent Reference

> **Purpose**: Primary reference for AI coding agents working on this codebase.
> **Last Updated**: April 2026

---

## Quick Start (30 Seconds)

1. **Build**: `.\scripts\build.ps1`
2. **Test**: `.\scripts\launch_game.ps1` → Check BepInEx console for test output
3. **Pattern**: PropertyHandler for properties, Template Method pattern
4. **Config**: Three tiers - BepInEx (real-time) → TOML (static) → CSV (overrides)

---

## Project Overview

**Crusader DE Tweaker** - BepInEx 5 plugin for Stronghold Crusader: Definitive Edition. Modifies unit/structure properties via configuration files using SHCDE-SE API.

| Technology | Purpose |
|------------|---------|
| C# .NET 4.8.1 | Plugin language |
| BepInEx 5 | Plugin framework |
| SHCDE-SE | Game API access |
| Tomlyn 0.19.0 | TOML parsing |

---

## Critical Rules

### DO
- Use PropertyHandler system for properties
- Follow Template Method pattern for handlers
- Add AI dev comments at top of every .cs file
- Test in-game after changes
- Keep solutions simple

### DO NOT
- Call `Plugin.UnitApi` directly (use PropertyHandler)
- Add complex lookup tables or ML systems
- Skip error handling
- Change Plugin GUID without updating all paths
- **Call any game API during `LibraryLoaded` / `ConfigManager.Initialize()`** — this causes a native ACCESS_VIOLATION crash. No game session exists yet. All API calls that read or write game state must be deferred to `OnStartMap` or `OnLoadMap` hooks.

---

## Directory Structure

```
Config/
├── BepInEx/           # Real-time multipliers (hooks)
│   └── Systems/       # Config systems + Handlers/
├── DamageMatrix/      # CSV damage matrices
│   ├── Core/          # Matrix loading utilities
│   ├── Melee/Ranged/EunuchAoe/
├── Toml/              # TOML property system
│   ├── Core/          # PropertyHandler base + helpers
│   │   ├── Handlers/  # PropertyHandler, PropertyRegistry, EntityProcessor
│   │   ├── Helpers/   # ConfigHelpers, TomlFormatter, TypeConverter
│   │   └── Logging/   # ErrorLogging
│   ├── Units/Properties/
│   └── Structures/Properties/
Data/                  # Enums, categories, data classes
Tests/                 # Unit test suites
scripts/               # Build/test automation
```

---

## Configuration System

### Three-Tier Priority

```
Game Defaults → TOML Properties → CSV Overrides → BepInEx Multipliers
```

| Tier | Files | Purpose | Restart? |
|------|-------|---------|----------|
| **BepInEx** | `CrusaderDETweaker_GlobalMultipliers.cfg` | Global multipliers | No |
| **TOML** | `*_Units.toml`, `*_Structures.toml` | Individual properties | Yes |
| **CSV** | `*_MeleeDamage.csv`, etc. | Granular damage overrides | Yes |

### Config Paths

All configs in: `{GameDir}\BepInEx\config\CrusaderDETweaker\`
CSV damage matrices in: `{GameDir}\BepInEx\config\CrusaderDETweaker\DamageMatrices\`

---

## PropertyHandler System

**Pattern**: Template Method - base class defines workflow, subclasses implement specifics

**Location**: `Config/Toml/Core/Handlers/PropertyHandler.cs`

### Creating a New Property

```csharp
// 1. Create handler in Config/Toml/Units/Properties/
internal class YourProperty : PropertyHandler<eChimps, int>
{
    public YourProperty() : base("PropertyName") { }
    
    protected override bool TryGetFromAPI(eChimps unit, out int value)
    {
        return ErrorHandlingHelper.TryGetValueWithResult(
            $"Get {Name}", unit.ToString(),
            () => Plugin.UnitApi.GetYourProperty(unit),
            out value, defaultValue: 0);
    }
    
    protected override void SetToAPI(eChimps unit, int value)
    {
        ErrorHandlingHelper.TryExecute(
            $"Set {Name}", unit.ToString(),
            () => Plugin.UnitApi.SetYourProperty(unit, value));
    }
}

// 2. Register in UnitPropertyRegistry.cs Initialize()
_instance.Register(new YourProperty());

// 3. Add to .csproj <Compile Include="..." />
```

---

## Initialization Flow

```
Plugin.Awake()
    ↓
CrusaderLibrary_LibraryLoaded()  ← SHCDE-SE API available
    ↓
ConfigManager.Initialize()
    ├─ GenerateDefaults() for each system  ← FILE I/O ONLY, no API calls
    ├─ Load() for each system              ← Registers hooks, reads TOML/CSV
    └─ (no direct API calls here)
    ↓
BepInExConfigManager.Initialize()  ← Real-time hooks
    ↓
OnStartMap / OnLoadMap hooks fire  ← SAFE to call game APIs here
// CoreTestRunner.RunAllTests()  ← DISABLED (re-enable for dev verification)
```

### CRITICAL: No Game API Calls During Init

**LibraryLoaded fires before any game session exists.** Calling game APIs at this point (writing to native memory via `GameGlobalsManager`, reading unit/structure properties, etc.) causes a native `ACCESS_VIOLATION` crash. The crash appears inside SHCDE-SE's own init sequence in the log — this is misleading. The actual cause is our code corrupting native state on the background init thread.

**Safe during init**: File I/O, TOML parsing, registering event hooks, BepInEx config binding.

**NOT safe during init**: Anything on `Plugin.UnitApi`, `Plugin.BuildingApi`, `Plugin.GlobalsApi`, `Plugin.PlayerApi`. Defer these to `OnStartMap`/`OnLoadMap`.

Past crashes caused by this:
- `GenerateDefaultConfigUnits/Structures` calling `TryGetFromAPI` for missing entries
- `CaptureOriginalDefaults` calling `GetMeleeDamageFromTo` for every unit pair
- `ApplyAllGlobalConfigs` calling `GameGlobalsManager.SetValue()` at load time

---

## Key Code Locations

| Task | File |
|------|------|
| Entry point | `Plugin.cs` |
| Config orchestration | `Config/ConfigManager.cs` |
| TOML loading | `Config/Toml/ConfigLoader.cs` |
| Property base class | `Config/Toml/Core/Handlers/PropertyHandler.cs` |
| Unit properties | `Config/Toml/Units/Properties/*.cs` |
| Unit registry | `Config/Toml/Units/UnitPropertyRegistry.cs` |
| Unit categories | `Data/UnitCategories.cs` |
| Test runner | `Tests/CoreTestRunner.cs` |

---

## Tag System (DEACTIVATED)

The Tag system was intended to provide 100% accuracy for damage modifiers, but has been **DEACTIVATED** due to high complexity and context window performance issues for automated management. 

- **Status**: Code kept for reference, but calls are commented out.
- **Replacement**: Use **CSV damage matrices** for specific attacker-defender overrides.
- **Impact**: Default TOML properties (Armor multipliers) apply a generic formula; surgical corrections must be done via CSV files.

---

## API Access

```csharp
// Static instances (available after library load)
Plugin.UnitApi        // GameUnitManagerAPI
Plugin.BuildingApi    // GameBuildingManagerAPI
Plugin.Logger         // BepInEx logging
```

**Important**: Use PropertyHandler, not direct API calls for properties.

---

## Logging

```csharp
Plugin.Logger.LogInfo("Message");     // General info
Plugin.Logger.LogWarning("Warning");  // Potential issues
Plugin.Logger.LogError("Error");      // Failures
Plugin.Logger.LogDebug("Debug");      // Verbose (disabled by default)
```

Enable debug: `BepInEx\config\BepInEx.cfg` → `LogLevels = ..., Debug`

---

## Unit Tests

Tests run automatically on game load. Check console for:
```
=== CrusaderDETweaker Core Tests ===
ALL 4 TEST SUITES PASSED: PropertyHandler, PropertyRegistry, EntityProcessor, UnitTagRegistry
```

Test files: `Tests/*.cs`, `Data/UnitTagInteraction.Test.cs`

---

## Common Tasks

### Building
```powershell
.\scripts\build.ps1
```

### Testing in Game
```powershell
.\scripts\launch_game.ps1
```

### Checking Unit Categories
```csharp
if (UnitCategories.IsNonModifiable(unit)) return;
if (UnitCategories.IsRecruitable(unit)) { /* ... */ }
```

### Error Handling Pattern
```csharp
return ErrorHandlingHelper.TryGetValueWithResult(
    "Operation Name", entityName,
    () => apiCall(),
    out value, defaultValue);
```

---

## Related Documentation

| File | Purpose |
|------|---------|
| [README.md](README.md) | User overview |
| [DEVELOPER_NOTES.md](DEVELOPER_NOTES.md) | Critical build info |
| [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) | User config guide (BBCode) |
| [DAMAGE_NOTES.md](DAMAGE_NOTES.md) | Damage system analysis |
| [scripts/README.md](scripts/README.md) | Script documentation |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

### SHCDE-SE API References (`docs/shcde-se/`)

| File | Purpose |
|------|---------|
| [API_REFERENCE.md](docs/shcde-se/API_REFERENCE.md) | SHCDE-SE API usage guide with examples |
| [GameUnitManagerAPI.md](docs/shcde-se/GameUnitManagerAPI.md) | Unit management API |
| [GameBuildingManagerAPI.md](docs/shcde-se/GameBuildingManagerAPI.md) | Building management API |
| [GamePlayerManagerAPI.md](docs/shcde-se/GamePlayerManagerAPI.md) | Player/resource management API |
| [GameGlobalsManager.md](docs/shcde-se/GameGlobalsManager.md) | Global constants and session settings |

---

## Plugin GUID

```csharp
[BepInPlugin("CrusaderDETweaker", ...)]
```

**Critical**: GUID must match folder `BepInEx/plugins/CrusaderDETweaker/`

---

## Summary

1. **PropertyHandler** for all property access
2. **Template Method** pattern for handlers
3. **Three-tier config**: BepInEx → TOML → CSV
4. **Build**: `.\scripts\build.ps1`
5. **Test**: Launch game, check console
6. **AI dev comments** at top of every .cs file
