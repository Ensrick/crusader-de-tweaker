# Scripts — CrusaderDETweaker

Development and release automation scripts.

---

## Release Pipeline

### `release.ps1` ⭐
**The main script. Runs the full release pipeline end-to-end.**

```powershell
.\scripts\release.ps1                        # auto-named backup
.\scripts\release.ps1 -BackupName "pre-v2.2" # named backup
```

**Steps (in order):**
1. `build.ps1` — compile plugin DLL
2. `backup_configs.ps1` — save your current configs (safety net only; nothing deletes them anymore)
3. `package_release.ps1` — stage the plugin folder, create zip, copy BBCode docs next to it

Aborts at any step on failure.

> **Changed in v2.4.1:** the zip is plugin-only. The old pipeline had three extra steps
> (`reset_configs.ps1` → `launch_game.ps1` → `restore_configs.ps1`) to put pristine configs in
> the zip — but users upgrading by extracting over their install would overwrite their
> personalized configs with those defaults. Configs are now generated on first launch and
> migrated on updates, so they don't ship at all. The reset/restore scripts remain available
> as standalone tools.

---

## Individual Scripts

### `build.ps1`
Compiles the plugin DLL using MSBuild (falls back to `dotnet build`).

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Debug
```

Output: `{GamePath}\BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll`

---

### `backup_configs.ps1`
Copies all config files and damage matrices to a named backup folder.

```powershell
.\scripts\backup_configs.ps1                  # timestamp name
.\scripts\backup_configs.ps1 -Name "pre-v2.2" # named backup
```

Backs up: `GlobalMultipliers.cfg`, `GameplaySettings.toml`, `Structures.toml`, `Units.toml`, `DamageMatrices\*.csv` (11 files total).

Destination: `{GamePath}\BepInEx\config\CrusaderDETweaker\Backups\<name>\`

---

### `reset_configs.ps1`
Deletes all live config files so the game regenerates defaults on next launch.

```powershell
.\scripts\reset_configs.ps1
```

---

### `restore_configs.ps1`
Restores configs from a previous backup.

```powershell
.\scripts\restore_configs.ps1                  # list available backups
.\scripts\restore_configs.ps1 -Name "pre-v2.2" # restore named backup
```

---

### `launch_game.ps1`
Launches Stronghold Crusader DE via Steam, forces windowed mode, waits for plugin initialization, then kills the game.

```powershell
.\scripts\launch_game.ps1
```

Used by `release.ps1` to generate fresh default config files. Also shared with CrusaderDEHandicap's release pipeline (both mods load in the same session).

---

### `package_release.ps1`
Copies the live plugin folder and config folder (excluding `Backups/`) to the staging directory and repacks the release zip.

```powershell
.\scripts\package_release.ps1
```

Output: `D:\Game Mods\Stronghold\Crusader DE Tweaker\Crusader DE Tweaker.zip`

---

### `check_game_running.ps1`
Checks whether Stronghold Crusader DE is currently running. Exit 0 = running, Exit 1 = not.

---

### `test_initialization_order.ps1`
Verifies config system initialization order (BepInEx cfg before TOML, damage matrix before property handlers).

---

### `test_reload_tracking.ps1`
Tests that reload tracking correctly detects config changes (change detection, unchanged values, reset detection).
