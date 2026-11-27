# Author test card: 2.11.13 (covers 2.11.4 to 2.11.13) on two PCs

Windowless gate already passed: `test_headless.ps1 -Only recruit`, run selftest-20261127-143657 (recruit limits with
`[Compatibility] GameActionHook = Auto` and `Own`, identical outcomes). What only you can check is multiplayer and the
real UI. Detailed per-version cards are in `docs/test-cards/`; this is the short route through all of them.

## Before you start

- Both PCs: the same 2.11.13 build. Compare the DLL SHA-256 on both PCs before the match:
  `4D700F5F157DF507D78BB8BDD0644766154190FF1547BE4C68FFDD9645AB4780`. Same Script Extender (2.14.6) on both PCs.
  Same APIShared on both, or none on either.
- PC-A hosts with "Players who join use my configs" ON. PC-B joins.
- PC-A Units file: `[CHIMP_TYPE_CATAPULT] MaxCount = 2`, `[CHIMP_TYPE_KNIGHT] MaxCount = 5`,
  `[CHIMP_TYPE_ARAB_HORSEMAN] RequiresHorse = true`. GameplaySettings: `["Auto Trade".STORED_SWORDS] Enabled = true`,
  `BuyLevel = 10`. CFG: `UnitHealthMultiplier = 2.0`.
- After the match, keep both `BepInEx\LogOutput.log` files.

## Steps and what each result means

| # | Do | Expected | If not |
|---|----|----------|--------|
| 1 | PC-B joins with an older Tweaker (2.11.12 or less), then with 2.11.13 | Older: "missing mods!", no join. 2.11.13: joins | Joins with the older one: NetworkMode 1 (2.11.11) did not reach the Script Extender's lobby check |
| 2 | Look at PC-B's log after joining | `[ConfigSync] Applied the host's configs`, host version `2.11.13+sim2` | `REJECTED`: version or simulation-version check (2.11.9) |
| 3 | Both: place 2 Catapult tents, then a 3rd | 3rd refused, no gold spent | 3rd accepted: siege placement block (2.11.4) |
| 4 | Both: hire 5 Knights, then 1 more; Ctrl-click hire with fewer than 5 alive | 6th refused; Ctrl hires only up to 5 | Over the cap: recruit gate (2.11.12/13); check which hook the log names (`Recruit gate registered with APIShared` or `MakeTroop recruit-cap hook installed`) |
| 5 | Both: Stables + Mercenary Post, hire Horse Archers until no horse is free | Hire refused when the stables are full, on both PCs the same | Different horse counts between PCs: stable linking (2.11.6/2.11.7) |
| 6 | Hover a freshly recruited Spearman on both PCs | Same (doubled) health on both | Different health: recruit health on the game tick (2.11.7) |
| 7 | Build a Marketplace on both | Both logs: `[Auto Trade] ... are not applied in multiplayer`; market settings unchanged | Auto-trade changed: 2.11.5 multiplayer guard |
| 8 | PC-A edits its Units file during the lobby, then starts the match | PC-A log: `Hosting with config sync on ... you play the files you sent`; both PCs play the launch-time values | Host plays the edit: host snapshot (2.11.9) |
| 9 | Play 10 minutes with fighting and siege | No out-of-sync message; unit counts and gold match when compared at the same moment | Any desync: send both logs; note the minute it happened |
| 10 | Single player afterwards on PC-A | Everything as before; Catapult tent cap, Knight cap and Auto Trade work | Single-player regression |

PASS = all expected results. Nothing here is a release go; the per-build ship go names the version.
