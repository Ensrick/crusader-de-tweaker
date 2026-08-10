# Developer Notes - Critical Information

> **CRITICAL**: Read this before making changes to build paths, GUID, or initialization order.

---

## Plugin GUID and Build Paths

### Plugin GUID and version

Identity and version are single-sourced in `PluginInfo.cs`:

```csharp
// PluginInfo.cs
public const string PLUGIN_GUID    = "CrusaderDETweaker";
public const string PLUGIN_NAME    = "Crusader DE Tweaker";
public const string PLUGIN_VERSION = "2.5.0";

// Plugin.cs
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
```

`Properties/AssemblyInfo.cs` takes `AssemblyVersion`/`AssemblyFileVersion` from `PLUGIN_VERSION`, and
`scripts/build.ps1` stamps `info.json`'s `Version` from it at build time. **Bump the version in
`PluginInfo.cs` only** — nothing else needs editing.

**GUID**: `"CrusaderDETweaker"`

**DO NOT CHANGE** without updating:
1. `CrusaderDETweaker.csproj` OutputPath
2. `scripts/build.ps1` path references
3. All documentation

### Build Output Location

**Both Debug AND Release output to:**
```
{GameDir}\BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll
```

**Why?** BepInEx loads plugins from folders matching their GUID.

---

## Config Loading Order

**The order is CRITICAL and must not change:**

```
1. Generate/migrate config files (TOML + CSV) — reads live API values for the "# Default:" comments
2. Load TOML configuration (unit + structure templates)
3. Load CSV damage matrices — these override TOML for the matchups they cover
4. Register runtime hooks; defer all session-state writes to the map-load events
```

`ConfigManager.ConfigSystems` fixes this order: `GlobalConfigSystem`, `UnitConfigSystem`,
`StructureConfigSystem`, `DamageMatrixConfigSystem`. Each system's `GenerateDefaults()` runs for all
four, then each system's `Load()`.

### Why Order Matters

CSV is applied last of the file tiers, so it is the authority for per-matchup damage. Session-scoped
native state — anything through `GameGlobalsManager`, and the `PlayerApi` setters for gameplay options,
trade prices and auto-trade — must NOT be written during SHCDE-SE `LibraryLoaded`; that is an
ACCESS_VIOLATION. `GlobalConfigSystem.Load()` therefore only registers hooks, and
`ConfigLoader.ApplyAllGlobalConfigs()` runs from the `OnStartMap`/`OnLoadMap` **Post** handlers.

API *reads* during generation are a different case: they are performed on every launch (see
`PropertyHandler.TryGetOriginalValue` → `TryGetFromAPI`) and they work. So do the unit/structure
template writes done by `ConfigLoader.ApplyAllUnitConfigs` / `ApplyAllStructureConfigs`.

---

## CSV vs TOML Logic

Original-default capture was removed entirely (`CsvMatrixReader`, `CsvMatrixFallback`,
`OriginalDefaultsCapture`, all `*DefaultsCapture.cs`). There is no "compare against the captured
original" step. The loaders read the CSV via `CsvHelper.ReadMatrix` and apply **every value >= 0**
straight to the game; a **negative value (`-1`) skips the cell**, leaving the game's own value in place.
`-1` is also the fallback for an unparseable or missing cell, so corrupt input cannot silently zero
real damage (a non-empty cell that fails to parse is logged as a warning).

TOML property settings and the runtime BepInEx multipliers are separate layers on either side of this.

---

## Common Mistakes

| Mistake | Consequence |
|---------|-------------|
| Change GUID without updating paths | Plugin won't load |
| Hand-edit the version outside `PluginInfo.cs` | `info.json` / assembly version drift |
| Write session state or touch `GameGlobalsManager` during `LibraryLoaded` | Native ACCESS_VIOLATION at startup |
| Add a global setting to a `Load()` body instead of `ApplyAllGlobalConfigs()` | Crash at startup, or setting silently reset by the map's own rules |
| Register hooks before configs are loaded | Hooks may observe incomplete settings |
| Treat `0` in a CSV as "no override" | `0` IS applied — it zeroes that matchup. `-1` is the skip value. |

---

## Testing Checklist

Before submitting changes:

- [ ] Build succeeds: `.\scripts\build.ps1`
- [ ] Game loads plugin (check BepInEx console)
- [ ] Initializer order check passes: `.\scripts\test_initialization_order.ps1`
- [ ] Smoke launch passes: `.\scripts\launch_game.ps1`
- [ ] On-load test suites pass — `LogOutput.log` shows `ALL 3 TEST SUITES PASSED`
- [ ] TOML changes apply correctly
- [ ] CSV values apply for cells >= 0 and are skipped at `-1`

---

## Related Documentation

- [CLAUDE.MD](CLAUDE.MD) - Full architecture reference
- [API_REFERENCE.md](docs/shcde-se/API_REFERENCE.md) - SHCDE-SE API details
