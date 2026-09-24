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
public const string PLUGIN_VERSION = "2.7.0";

// Plugin.cs
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
```

`Properties/AssemblyInfo.cs` takes `AssemblyVersion`/`AssemblyFileVersion` from `PLUGIN_VERSION`, and
`scripts/build.ps1` stamps the **output** copy of `info.json` from it. The tracked `info.json` is never
rewritten by a build, so **bump `PluginInfo.cs` and `info.json` together**: `package_release.ps1` and
`ship.ps1` preflight refuse a mismatch, and packaging verifies the DLL's AssemblyVersion/FileVersion and
the shipped `info.json` against `PLUGIN_VERSION`.

**GUID**: `"CrusaderDETweaker"`

**DO NOT CHANGE** without updating:
1. `info.json` `"GUID"`
2. `scripts/_release_common.ps1` (`$PluginGuid`, payload whitelist) and `scripts/deploy.ps1`
3. All documentation

### Build Output vs Deploy

Building never writes into the game folder. Output goes to the repo:
```
bin\Release\   (or bin\Debug\)   - a complete plugin folder: DLL, Tomlyn.dll, info.json, Override\
```
`.\scripts\deploy.ps1` (or `build.ps1 -Deploy`) copies it into
`{GameDir}\BepInEx\plugins\CrusaderDETweaker\` - BepInEx loads plugins from the folder matching their
GUID. Deploy refuses while the game runs, backs up replaced files to `dist\deploy-backups\`, and never
touches `BepInEx\config`. Releases go through `.\scripts\ship.ps1` (see `scripts/README.md`).

---

## Config Loading Order

**The order is CRITICAL and must not change:**

```
1. Generate/migrate config files (TOML + CSV) — reads live API values for the "# Default:" comments
2. Load: register the session hooks (no game-table writes)
3. Bind the BepInEx multipliers, then ConfigLoader.ReapplyTemplateConfigs("launch"): restore game values
   (TemplateBaseline), TOML templates, CSV matrices (they override TOML for the matchups they cover), then
   the template multipliers. The same call runs after every SE unload reset and before every session start.
4. Defer all session-state writes to the map-load events
```

`ConfigManager.ConfigSystems` fixes this order: `GlobalConfigSystem`, `UnitConfigSystem`,
`StructureConfigSystem`, `DamageMatrixConfigSystem`. Each system's `GenerateDefaults()` runs for all
four, then each system's `Load()`.

### Why Order Matters

CSV is applied last of the file tiers, so it is the authority for per-matchup damage. Session-scoped
native state — anything through `GameGlobalsManager`, and the `PlayerApi` setters for gameplay options,
trade prices and auto-trade — must NOT be written during SHCDE-SE `LibraryLoaded`; that is an
ACCESS_VIOLATION. `GlobalConfigSystem.Load()` therefore only registers hooks, and
`ConfigLoader.ApplyAllGlobalConfigs()` runs from the `OnStartMap` / `OnLoadMap` / `OnLoadSave` **Post** handlers
(the last one covers loading a saved game, which SHCDE-SE raises as its own event; added in v2.6.5).

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

## Multiplayer Host Config Sync (2.7.0)

Full design: [docs/HOST_SYNC_DESIGN.md](docs/HOST_SYNC_DESIGN.md). Test plan: [docs/HOST_SYNC_TEST_PLAN.md](docs/HOST_SYNC_TEST_PLAN.md).
The rules that constrain other code:

1. **Template writes report to `TemplateBaseline.BeforeWrite` first, and all re-application goes through
   `ConfigLoader.ReapplyTemplateConfigs(reason)`** (SE unload resets, session starts, host sync apply and
   revert). It restores the game's own values, then re-runs the launch apply order
   (Units TOML, Structures TOML, matrices, `BepInExConfigManager.ReapplyTemplateMultipliers`). A writer
   that does not report its cell leaves one side's value behind wherever the other side has `-1`. Cells
   written by two writers (unit fire, building fire, Bedouin heal: matrix loader + fire/heal multiplier)
   must use the same key (`MatrixBaselineKeys`).
2. **Loaders must be re-runnable** and must not scale by a runtime multiplier at apply time (the hit-time
   hooks do that; `RangedDamageMatrixLoader` did until 2.7.0).
3. **Paths go through `ConfigPaths` / `MatrixPaths`.** While `ConfigPaths.UseHostSyncFiles` is set they
   resolve into `HostSync\`. The player's own files are never written by the sync.
4. **GameplaySettings stays session-hook-only.** The session hooks read `ConfigPaths.Globals`, which is the
   host's file during a synced session.
5. The ViewModel's `[SyncHostOnly]` setters call `CanEdit()` first. As a client, a setter call that passes
   it is the lobby owner's value (SE verifies the Steam sender); the manager relies on that.
6. Changing the wire layout means bumping `ConfigSyncCodec.FormatVersion`; adding a synced file means a new
   `SyncFileId` plus its fixed `RelativePathFor` name. Names never travel in the package.

---

## Common Mistakes

| Mistake | Consequence |
|---------|-------------|
| Change GUID without updating paths | Plugin won't load |
| Bump `PluginInfo.cs` without `info.json` (or vice versa) | Packaging/ship preflight refuses the mismatch |
| Write session state or touch `GameGlobalsManager` during `LibraryLoaded` | Native ACCESS_VIOLATION at startup |
| Add a global setting to a `Load()` body instead of `ApplyAllGlobalConfigs()` | Crash at startup, or setting silently reset by the map's own rules |
| Register hooks before configs are loaded | Hooks may observe incomplete settings |
| Treat `0` in a CSV as "no override" | `0` IS applied — it zeroes that matchup. `-1` is the skip value. |
| Add a template writer without `TemplateBaseline.BeforeWrite` | Multiplayer host config sync can no longer revert or mirror that value exactly |
| Write a config through `Paths.ConfigPath` instead of `ConfigPaths` | Bypasses the host-sync redirect; a client would read its own file during a synced match |

---

## Testing Checklist

Before submitting changes:

- [ ] Build succeeds: `.\scripts\build.ps1`, then `.\scripts\deploy.ps1` with the game closed
- [ ] Game loads plugin (check BepInEx console)
- [ ] Initializer order check passes: `.\scripts\test_initialization_order.ps1`
- [ ] Smoke launch passes: `.\scripts\launch_game.ps1`
- [ ] On-load test suites pass — `LogOutput.log` shows `ALL 5 TEST SUITES PASSED`
- [ ] TOML changes apply correctly
- [ ] CSV values apply for cells >= 0 and are skipped at `-1`

---

## Related Documentation

- [CLAUDE.md](CLAUDE.md) - Full architecture reference
- [API_REFERENCE.md](docs/shcde-se/API_REFERENCE.md) - SHCDE-SE API details
