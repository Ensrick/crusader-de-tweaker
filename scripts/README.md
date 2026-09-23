# Scripts — CrusaderDETweaker

Development and release automation scripts. Building never writes into the game folder;
`deploy.ps1` is the only script that installs plugin files there, and none of them touch
`BepInEx\config` except the explicit config tools (`backup_` / `reset_` / `restore_configs.ps1`).

---

## Shipping: `ship.ps1` ⭐

**The one end-to-end ship path.** Named stages, each idempotent, each verifying its own result,
each re-runnable alone with `-Stage`.

```powershell
.\scripts\ship.ps1                                        # full DRY RUN (default)
.\scripts\ship.ps1 -Version 2.6.6 -Publish                # the real ship
.\scripts\ship.ps1 -Stage preflight,build,package         # local stages only
.\scripts\ship.ps1 -Stage workshop -Version 2.6.6 -Publish  # redo one stage
```

| Stage | Kind | Does | Verified by |
|-------|------|------|-------------|
| `preflight` | local | clean tree; `-Version` = `PluginInfo.cs` = `info.json`; CHANGELOG entry exists; tag `v<ver>` not on origin/github (or already at HEAD); tools present; Workshop description <= 8000 chars and names the version | the checks |
| `build` | local | Release rebuild into `dist\stage\<ver>\build\` | payload whitelist + DLL AssemblyVersion/FileVersion + info.json = version |
| `package` | local | `dist\stage\<ver>\Crusader DE Tweaker.zip`; with `-Publish` also copied (+ BBCode txt) to `D:\Game Mods\Stronghold\Crusader DE Tweaker` | zip entry list = whitelist |
| `tag` | public | annotated `v<ver>` at HEAD, pushed to `origin` (GitLab) and `github` | `git ls-remote` on both = HEAD |
| `github` | public | `gh release create` with the zip | `gh release view` asset size |
| `gitlab` | public | `glab release create` with the zip | `glab release view` |
| `workshop` | public | stage (`workshop\steam-preview.png` + built plugin) -> `SHCDESE.WorkshopPackager.exe` (.map, local) -> `pdengine.steamugc.tool.exe -u` item 3726034964 with `-z workshop\workshop-description.txt` | `Steam\logs\workshop_log.txt` "Uploaded new content" + Web API `file_size` = .map size |
| `nexus` | public | `nexus\Publish-NexusMod.ps1` (mod 38) | script completes; receipt |
| `deploy` | local | `deploy.ps1` from the packaged tree, LAST | deploy.ps1 hash check |

- **Dry run is the default.** Without `-Publish` the local stages build and package into `dist\` only;
  every public stage, the release-folder copy and the game-folder deploy print what they would do.
- `-Publish` requires `-Version <x.y.z>` equal to `PluginInfo.PLUGIN_VERSION`.
- Idempotent: a stage whose result already exists reports `SKIP` (receipts for Workshop / Nexus live in
  `dist\receipts\<ver>\`; `-Force` redoes them).
- Tool paths (`-PackagerExe`, `-UploaderExe`, `-SteamDir`), repos and IDs are parameters with defaults.
- Before shipping: bump `PluginInfo.cs` + `info.json`, add the CHANGELOG entry, and update the first
  line (and version block) of `workshop\workshop-description.txt`. Nexus's mod description is not
  API-editable; refresh it by hand. After a Workshop upload, restart Steam fully before testing.

### `release.ps1`
Local rehearsal: `backup_configs.ps1`, then `ship.ps1 -Stage preflight,build,package`.

---

## Build and deploy

### `build.ps1`
Compiles the plugin with MSBuild (falls back to `dotnet build`) into `bin\<Configuration>\`, a complete
plugin folder (DLL, `Tomlyn.dll`, `info.json`, `Override\`). Stamps the OUTPUT `info.json` with
`PLUGIN_VERSION`; the tracked `info.json` is never rewritten (a mismatch is warned about).

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Debug
.\scripts\build.ps1 -Deploy                      # then deploy.ps1
.\scripts\build.ps1 -ShcdeseDir <extracted SE>\BepInEx\plugins\000shcdese -OutputPath dist\x -Rebuild
```

### `deploy.ps1`
Copies a built plugin folder (default `bin\Release\`) into
`{GamePath}\BepInEx\plugins\CrusaderDETweaker\`. Refuses while the game is running
(`check_game_running.ps1`), backs up every replaced file to `dist\deploy-backups\<timestamp>\`
(outside `plugins\`, so BepInEx never loads a backup), hash-verifies the result, never touches
`BepInEx\config`. `-WhatIf` lists what it would do.

### `package_release.ps1`
Builds a Release from a **clean** tree (refuses a dirty one unless `-AllowDirty`) into
`dist\stage\<ver>\build\`, verifies it (exact file whitelist in `_release_common.ps1`, DLL
AssemblyVersion/FileVersion and info.json = `PLUGIN_VERSION`), zips it with forward-slash entries, and
copies the zip + `NEXUS_DESCRIPTION.txt` / `CONFIGURATION_GUIDE.txt` to the release folder (an older zip
there is renamed `*.zip.bak.v<old>`). `-NoReleaseDirCopy` keeps everything in `dist\`. It never reads
the game's plugin folder (before 2.6.6 it zipped whatever was deployed there).

### `_release_common.ps1`
Shared helpers (dot-sourced): version reads, payload whitelist, zip, CHANGELOG entry parsing.

---

## Config tools (these DO touch `BepInEx\config\CrusaderDETweaker\`)

### `backup_configs.ps1`
Copies all config files and damage matrices to a named backup folder.

```powershell
.\scripts\backup_configs.ps1                  # timestamp name
.\scripts\backup_configs.ps1 -Name "pre-v2.2" # named backup
```

Backs up: `GlobalMultipliers.cfg`, `GameplaySettings.toml`, `Structures.toml`, `Units.toml`, `DamageMatrices\*.csv` (11 files total).

Destination: `{GamePath}\BepInEx\config\CrusaderDETweaker\Backups\<name>\`

### `reset_configs.ps1`
Deletes all live config files so the game regenerates defaults on next launch. Always runs
`backup_configs.ps1` first (aborts if that fails) and asks for confirmation unless `-Force`.

```powershell
.\scripts\reset_configs.ps1            # back up, confirm, delete
.\scripts\reset_configs.ps1 -WhatIf    # show what would happen
.\scripts\reset_configs.ps1 -Force     # back up, delete without the prompt
```

### `restore_configs.ps1`
Restores configs from a previous backup.

```powershell
.\scripts\restore_configs.ps1                  # list available backups
.\scripts\restore_configs.ps1 -Name "pre-v2.2" # restore named backup
```

---

## Game helpers

### `launch_game.ps1`
Launches Stronghold Crusader DE via Steam, forces windowed mode, waits for plugin initialization, then
kills the game. Shared with CrusaderDEHandicap's release pipeline (both mods load in the same session).

### `check_game_running.ps1`
Checks whether Stronghold Crusader DE is currently running. Exit 0 = running, Exit 1 = not.
Used by `deploy.ps1`.

### `test_initialization_order.ps1`
Verifies config system initialization order (BepInEx cfg before TOML, damage matrix before property handlers).

---

### `nexus/`
Nexus Mods upload tooling — see [nexus/README.md](nexus/README.md). Called by `ship.ps1`'s `nexus` stage.
