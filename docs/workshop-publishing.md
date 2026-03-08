# Workshop Mod Publishing Guide

This guide explains how to package BepInEx mods for distribution via Steam Workshop using the Map Archive System provided by SHCDE-SE.

---

## Overview

Workshop mods use the Map Archive System to distribute mods through Steam Workshop, providing:

- **Automatic Updates** — players receive updates when you publish changes
- **Easy Installation** — subscribe = install
- **Version Management** — the Script Extender handles updates automatically
- **One-Click Uninstall** — unsubscribe = remove

> **Note:** Workshop mods are NOT playable maps. The `.map` file acts purely as a container for mod files, enabling Steam Workshop distribution and automatic update detection.

Both **Asset Mods** (Manifest: 0) and **BepInEx Mods** (Manifest: 1) can be published via Workshop.

---

## Prerequisites

- A working mod (tested locally)
- `SHCDESE.WorkshopPackager.exe` — Workshop Packager tool
- `pdengine.steamugc.tool.exe` (or any Steam UGC uploader)
- Steam account with Stronghold Crusader DE ownership

---

## Step 1: Prepare Your Mod

Develop and test your mod locally first. Ensure it loads without exceptions.

**Asset Mod structure:**
```
BepInEx/plugins/YourMod/
  info.json
  Override/
  Patches/
info.json  (Manifest: 0)
```

**BepInEx Mod structure:**
```
BepInEx/plugins/YourMod/
  YourMod.dll
  info.json
  Override/
  Patches/
info.json  (Manifest: 1)
```

---

## Step 2: Create Workshop Package Structure

Your workshop package must mirror the exact BepInEx folder structure.

**Asset Mod (Manifest: 0):**
```
workshop-package/
  BepInEx/
    plugins/
      YourMod/
        info.json
        Override/
          Assets/GUI/Sprites/
            YourMod.png        (64x64 logo, filename = GUID)
        Patches/
  info.json                    (Manifest: 0, required!)
  steam-preview.png            (Workshop thumbnail)
```

**BepInEx Mod (Manifest: 1):**
```
workshop-package/
  BepInEx/
    plugins/
      YourMod/
        YourMod.dll
        info.json
        Override/
          Assets/GUI/Sprites/
            YourMod.png        (64x64 logo, filename = GUID)
        Patches/
  info.json                    (Manifest: 1, required!)
  steam-preview.png
```

### Critical Rules

**1. Exact path mirroring**

If your mod normally installs to `C:/Game/BepInEx/plugins/MyMod/MyMod.dll`, your workshop package must contain `BepInEx/plugins/MyMod/MyMod.dll`.

**2. Correct Manifest value**

| Manifest | Use for |
|----------|---------|
| `0` | Asset mods (no DLL) |
| `1` | BepInEx mods (with DLL) |

A mismatch means the mod will not load:
- Asset mod with `Manifest: 1` → rejected (no DLL found)
- BepInEx mod with `Manifest: 0` → code won't execute

**3. In-game logo (optional but recommended)**

Create a 64×64 PNG named exactly after your GUID and place it at:
```
BepInEx/plugins/YourMod/Override/Assets/GUI/Sprites/{YourGUID}.png
```

> Use only ASCII characters in your GUID. BepInEx does not handle non-ASCII GUIDs well.

---

## Step 3: Create info.json

**Asset Mod (Manifest: 0):**
```json
{
  "GUID": "myassetmod",
  "Author": "Your Name",
  "Name": "My Asset Pack",
  "Description": "Custom textures and sounds",
  "Version": "1.0.0",
  "Website": "https://gitlab.com/yourname/your-mod",
  "Manifest": 0
}
```

**BepInEx Mod (Manifest: 1):**
```json
{
  "GUID": "mymod",
  "Author": "Your Name",
  "Name": "My Mod",
  "Description": "What it does",
  "Version": "1.0.0",
  "Website": "https://gitlab.com/yourname/your-mod",
  "Manifest": 1
}
```

Use semantic versioning (`MAJOR.MINOR.PATCH`). The Script Extender uses this to detect when an update is available.

---

## Step 4: Create Workshop Thumbnail

Create `steam-preview.png` for the Steam Workshop listing.

- Format: PNG or JPG
- Recommended size: **636×358** (Steam's internal crop target)

Place it anywhere in your workshop package — just note the path for the upload command.

---

## Step 5: Package the Mod

Use the Workshop Packager to create the `.map` container:

```
SHCDESE.WorkshopPackager.exe -s "path/to/workshop-package" -o "path/to/output/mymod.map"
```

Example:
```
SHCDESE.WorkshopPackager.exe -s "E:/Mods/MyMod-Workshop" -o "E:/Mods/Releases/mymod.map"
```

---

## Step 6: Upload to Steam Workshop

Using `pdengine.steamugc.tool`:

**Initial upload:**
```
pdengine.steamugc.tool.exe -v -a 3024040 -n -k "My Mod" -d "Description" -s "E:/Mods/Releases" -o "E:/Mods/steam-preview.png" -h Public
```

| Flag | Meaning |
|------|---------|
| `-v` | Verbose output |
| `-a 3024040` | Stronghold Crusader DE App ID |
| `-n` | New item |
| `-k` | Title |
| `-d` | Description |
| `-s` | Source folder (containing the `.map` file) |
| `-o` | Preview image path |
| `-h` | Visibility: `Public`, `FriendsOnly`, `Private`, `Unlisted` |

**Save the Workshop Item ID** shown after upload — you need it for future updates.

> If the Item ID isn't shown in the output, check your Steam profile's Workshop items and note the latest one.

**Updating an existing item:**
```
pdengine.steamugc.tool.exe -v -a 3024040 -i YOUR_ITEM_ID -u -s "E:/Mods/Releases"
```

Before updating: increment the version in `info.json` and re-package the `.map` file.

---

## Step 7: Verify Installation

1. Subscribe to your item on Steam Workshop.
2. Launch the game.
3. If an update is detected, the game shows:
   > *"Mods have been updated or removed. The game will restart to apply changes."*
4. Click OK — the game restarts and applies the mod.
5. Verify `BepInEx/plugins/YourMod/` exists and the mod works.

> Steamworks CDN can be slow. Subscription and unsubscription events may take a couple of minutes to fully register with your Steam client.

---

## Update Mechanism

The Script Extender compares the version in `_SE/YourMod.json` (local) against the version in the workshop `.map` archive (remote). When `remoteVersion > localVersion`, it stages the update, and a PowerShell script applies it after the game exits.

**Version comparison uses Semantic Versioning:**

| Change | Example | Updates? |
|--------|---------|----------|
| Patch | `1.0.0` → `1.0.1` | Yes |
| Minor | `1.0.0` → `1.1.0` | Yes |
| Major | `1.0.0` → `2.0.0` | Yes |
| Downgrade | `1.1.0` → `1.0.9` | No |
| Same | `1.0.0` → `1.0.0` | No |

**Uninstallation:** When a player unsubscribes, the Script Extender detects the missing subscription and stages deletion of the mod folder on next restart.

---

## Best Practices

**Versioning:**
- `0.0.X` — bug fixes, minor tweaks
- `0.X.0` — new features, backwards compatible
- `X.0.0` — breaking changes, major overhaul

**Folder naming:** Avoid spaces and special characters (`@#$%^&*()`). Keep names under 50 characters.

**File size:** Keep total package under 100 MB. Use `.ogg` not `.wav` for audio. Optimize textures. Exclude build artifacts and debug files.

**Testing updates:** Always test the new version locally, increment the version, re-package from scratch (don't reuse old `.map` files), then subscribe to your own item to verify the update triggers correctly.
