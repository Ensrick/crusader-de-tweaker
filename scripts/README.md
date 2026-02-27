# Scripts Directory - AI Agent Reference

> **Purpose**: Development automation scripts for building, launching, and testing.
> **Primary Audience**: AI coding agents and developers.

## Quick Reference

| Script | Status | Purpose |
|--------|--------|---------|
| `build.ps1` | **ACTIVE** | Build the plugin DLL |
| `launch_game.ps1` | **ACTIVE** | Build and launch game via Steam |
| `check_game_running.ps1` | **ACTIVE** | Check if Stronghold is running |
| `test_initialization_order.ps1` | **ACTIVE** | Test config system init order |
| `test_reload_tracking.ps1` | **ACTIVE** | Test reload tracking system |

---

## Core Scripts (ACTIVE)

### build.ps1
**Purpose**: Compiles the CrusaderDETweaker plugin using MSBuild.

**Usage**:
```powershell
.\scripts\build.ps1
```

**Behavior**:
- Locates MSBuild via vswhere
- Builds in Release configuration
- Copies DLL to BepInEx plugins folder
- Returns exit code 0 on success, 1 on failure

**Exit Codes**:
| Code | Meaning |
|------|---------|
| 0 | Build successful |
| 1 | Build failed |

---

### launch_game.ps1
**Purpose**: Builds the plugin and launches Stronghold: Crusader DE via Steam.

**Usage**:
```powershell
.\scripts\launch_game.ps1
```

**Behavior**:
1. Calls `build.ps1`
2. If build succeeds, launches `steam://run/3024040`
3. Waits 3 seconds for Steam to start the game

**Dependencies**: `build.ps1`, Steam

---

### check_game_running.ps1
**Purpose**: Checks if Stronghold Crusader DE is currently running.

**Usage**:
```powershell
.\scripts\check_game_running.ps1
```

**Exit Codes**:
| Code | Meaning |
|------|---------|
| 0 | Game is running |
| 1 | Game is not running |

---

## Test Scripts (ACTIVE)

### test_initialization_order.ps1
**Purpose**: Verifies config system initialization order is correct.

**Usage**:
```powershell
.\scripts\test_initialization_order.ps1
```

**What It Tests**:
- BepInEx config initialized before TOML
- Damage matrix initialized before property handlers
- All systems initialize without errors

---

### test_reload_tracking.ps1
**Purpose**: Tests that reload tracking correctly detects config changes.

**Usage**:
```powershell
.\scripts\test_reload_tracking.ps1
```

**What It Tests**:
- Change detection works for modified values
- Unchanged values are not flagged
- Reset detection works correctly

---

## AI Agent Usage Guide

### Common Workflows

**Building the Plugin**:
```powershell
.\scripts\build.ps1
# Check exit code: $LASTEXITCODE -eq 0 means success
```

**Build and Test in Game**:
```powershell
.\scripts\launch_game.ps1
# Then check game logs for test output
```

**Running Unit Tests**:
```powershell
# Unit tests are currently DISABLED in Plugin.cs
# To re-enable for development: uncomment CoreTestRunner.RunAllTests() in Plugin.cs
# Then check BepInEx console for: "ALL 4 TEST SUITES PASSED"
```

**Verifying Game State**:
```powershell
if (.\scripts\check_game_running.ps1) {
    Write-Host "Game is running"
} else {
    Write-Host "Game is not running"
}
```

### Exit Code Convention

All active scripts follow this convention:
- **Exit 0**: Success / Condition true
- **Exit 1**: Failure / Condition false

### Script Dependencies

```
launch_game.ps1
    └── build.ps1
            └── MSBuild (via vswhere)

check_game_running.ps1
    └── (standalone)

test_*.ps1
    └── Requires game running with plugin loaded
```

---

## Related Documentation

- [CLAUDE.MD](../CLAUDE.MD) - AI agent instructions
- [DEVELOPER_NOTES.md](../DEVELOPER_NOTES.md) - Architecture overview
- [CONTRIBUTING.md](../CONTRIBUTING.md) - Contribution guidelines
- [DAMAGE_NOTES.md](../DAMAGE_NOTES.md) - Damage system research notes
