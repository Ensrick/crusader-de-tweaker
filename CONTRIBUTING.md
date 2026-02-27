# Contributing to Crusader DE Tweaker

## Quick Start

1. **Read** [CLAUDE.MD](CLAUDE.MD) - Primary development reference
2. **Build**: `.\scripts\build.ps1`
3. **Test**: `.\scripts\launch_game.ps1` → Check console for test results

## Development Workflow

1. Make changes
2. Run `.\scripts\build.ps1`
3. Test in game with `.\scripts\launch_game.ps1`
4. Check BepInEx console for errors/test output

## Code Style

### File Headers
Every `.cs` file must have an AI dev comment at the top:
```csharp
// Config/Toml/Units/Properties/YourProperty.cs
//
// PURPOSE: Brief description of what this file does.
//
// USAGE:
// - How to use this code
// - Key methods/classes
//
// IMPORTANT FOR AI AGENTS:
// - Critical information about this code
// - Dependencies or order requirements
```

### Patterns
- **PropertyHandler**: Template Method pattern for all properties
- **Registry**: Central registration for property handlers
- **Error Handling**: Use `ErrorHandlingHelper` methods

### Logging
```csharp
Plugin.Logger.LogInfo("Summary");    // High-level status
Plugin.Logger.LogWarning("Issue");   // Potential problems
Plugin.Logger.LogError("Failure");   // Errors
Plugin.Logger.LogDebug("Details");   // Verbose (disabled by default)
```

## Key Documentation

| File | Purpose |
|------|---------|
| [CLAUDE.MD](CLAUDE.MD) | **Primary AI reference** - architecture, patterns, code examples |
| [DEVELOPER_NOTES.md](DEVELOPER_NOTES.md) | Critical build info, GUID, paths |
| [API_REFERENCE.md](API_REFERENCE.md) | SHCDE-SE API documentation |
| [scripts/README.md](scripts/README.md) | Build/test script documentation |

## Pull Requests

1. Keep changes focused and small
2. Test in-game before submitting
3. Update documentation if needed
4. Include clear commit messages

## Architecture Overview

See [CLAUDE.MD](CLAUDE.MD) for:
- Directory structure
- Configuration system tiers
- PropertyHandler system
- Initialization flow
- Key code locations

