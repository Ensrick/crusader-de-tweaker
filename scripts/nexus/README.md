# Nexus publisher (`Publish-NexusMod.ps1`)

Local, ToS-sanctioned Nexus Mods release tool. A faithful PowerShell port of the
official [`Nexus-Mods/upload-action`](https://github.com/Nexus-Mods/upload-action),
using the official **Upload API v3** (`api.nexusmods.com/v3`) with a single
`apikey` header — no cookies, no browser scraping.

## What it can and cannot do

| Task | Supported | Mechanism |
|------|-----------|-----------|
| Upload a file | Yes | multipart, direct-to-S3 presigned PUTs |
| Add a version to an existing file slot | Yes | `POST /mod-files/{FileId}/versions` |
| Set the file version string | Yes | `version` field |
| Bump the mod page version | Yes | `-UpdateModVersion` (default on) |
| Archive the previous file version | Yes | `-ArchiveExisting` (default on) |
| Post a changelog entry | Yes | `POST /mods/{ModId}/changelogs` |
| **Edit the mod page description** | **No** | no v3 endpoint exists — do it by hand |

The mod **description** is the one piece the API cannot touch. Refresh it
manually on the website when it changes; `NEXUS_DESCRIPTION.md` is the source of
truth to paste from.

## API key (never committed)

Resolved in order:

1. `-ApiKey` parameter
2. `NEXUS_API_KEY` environment variable
3. `nexus.local.json` beside the script — `{ "ApiKey": "..." }` (gitignored)

Get or generate the key at <https://www.nexusmods.com/users/myaccount?tab=api>.
Copy `nexus.local.json.example` to `nexus.local.json` and paste it, or set the
env var. The key is never printed to the console or logs.

## Usage

```powershell
# 1. Confirm the key works and see remaining quota
./Publish-NexusMod.ps1 -Action validate

# 2. Find the FileId of the slot to update (FileId != ModId)
./Publish-NexusMod.ps1 -Action list-files -ModId 38

# 3. Dry-run the publish (no network writes), then publish for real
./Publish-NexusMod.ps1 -FileId <slot> -ModId 38 `
    -FilePath "D:\Game Mods\Stronghold\Crusader DE Tweaker\Crusader DE Tweaker.zip" `
    -Version 2.6.0 `
    -Changelog "Rebuilt against SHCDE-SE 1.40.0 (game 2.8)." -WhatIf
# remove -WhatIf to publish
```

## Known mods (game domain `strongholdcrusaderdefinitiveedition`)

| Mod | ModId | Notes |
|-----|-------|-------|
| Unit Stat Editor (= CrusaderDETweaker) | 38 | this repo |
| SHCDE-SE (Script Extender) | 35 | Rawra's, not ours to publish |
| BepInEx 5 Bootstrapper | 36 | not ours |

Use `-Action list-files -ModId 38` to discover the current FileId before a release.

## Gotchas

- `FileId` is the **file slot**, found on the Files tab → Manage Files, or via
  `list-files`. `ModId` (38) is the mod page id, needed only for the changelog.
- Changelog silently no-ops without `-ModId`.
- `-UpdateModVersion` must be on for the mod page version to move (default on here).
- Rate limits: 20,000/day, 500/hour (reset 00:00 GMT / top of hour). A publish is
  ~10 calls, so limits are a non-issue.
- Verified against the live v3 API and the upload-action source on 2026-08-10.
