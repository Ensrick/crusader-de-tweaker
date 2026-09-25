Version 2.7.1

Updated for SHC DE v2.8.2 and Script Extender 2.8.0!

[b]2.7.1:[/b]
[list]
[*]Packaging only: every download now includes a detailed README.txt (install / update / uninstall, requirements, bug reports, the full configuration guide). Same mod code as 2.7.0.
[/list]

[b]2.7.0: Multiplayer host config sync (not yet tested in a real multiplayer match):[/b]
[list]
[*][b]Everyone in a lobby plays with the host's configs.[/b] Players who join receive the host's Units, Structures and GameplaySettings files, the 7 damage matrices and the multiplier values. No more copying the config folder before a match. The host turns it on or off in the lobby's Mod Options window, tab "Crusader DE Tweaker" (on by default).
[*][b]Your own config files are never changed.[/b] The host's copies are kept in BepInEx\config\CrusaderDETweaker\HostSync\, and your own configs apply again when you leave the lobby or the match ends, no restart needed.
[*]Host and players need the same 2.7.x version. The tab shows the sync status; every step is logged with [ConfigSync] in BepInEx\LogOutput.log.
[/list]

[b]2.6.8:[/b]
[list]
[*][b]Fix: unit and structure stats (Speed, Health, costs, shields, housing) and the damage CSVs now stay applied when you play.[/b] The Script Extender resets these game tables to vanilla every time a map is unloaded, which also happens while you move through the menus, and the mod only applied them once at launch. They are now re-applied after every reset and before every game starts. Reported on Nexus ("the Speed of any unit I change doesn't do anything").
[*][b]The fire, Bedouin-heal and wall-cost multipliers stay applied too[/b], and RangedDamageTakenMultiplier is applied exactly once. Every re-apply first puts the game's own values back, so nothing is ever scaled twice.
[/list]

[b]2.6.6:[/b]
[list]
[*][b]Rebuilt for Script Extender 2.8.0 - update SE first, then this mod.[/b] SE 2.0 replaced an internal library this mod depends on, so 2.6.5 and older do not work on SE 2.x, and 2.6.6 does not work on SE 1.x. With an older SE the mod now refuses to load and says so in BepInEx\LogOutput.log instead of failing mid-game.
[*]No gameplay or config change; your config files are untouched.
[/list]

[b]2.6.5:[/b]
[list]
[*][b]Fix: the GameplaySettings file (siege-engine stones, trade prices, stealth, gameplay options, auto-trade) now also applies when you load a saved game.[/b] It was only applied when starting a new game or map, so a session resumed from a save kept vanilla values for all of it. Reported on Nexus (weapon prices and siege-engine stones "stay vanilla").
[*][b]A syntax error in CrusaderDETweaker_GameplaySettings.toml is now reported clearly[/b] in BepInEx\LogOutput.log ("SYNTAX ERROR in ... NOTHING in this file is applied", with the line number). Everything after a # is a comment: "BuyPrice = 52  # default: 155" is right, "BuyPrice = 52 default: 155" (no #) breaks the whole file, and every setting in it stays vanilla.
[*]The log now shows every trade price it applied, with a read-back from the game ("STORED_BOWS: buy 155 -> 52, sell 75 -> 25 (read back 52/25)"), and the siege-engine stone values it wrote.
[/list]

[b]2.6.4:[/b]
[list]
[*][b]Fix: a Demolisher ShieldHealth above 65535 no longer resets to -1 every launch.[/b] The game stores shield health in 16 bits (default 40000, maximum 65535). A larger value failed to convert, and the config migration silently rewrote the line as "-1 = use default" on every game start. Too-large values are now clamped to the maximum and written back as such, with a warning in the log; the same applies to every other integer stat. Values within range were never affected.
[/list]

[b]2.6.3:[/b]
[list]
[*][b]Fix: damage CSVs saved from Excel / Google Sheets no longer lose their header row on launch.[/b] Spreadsheets save the blank line under the comment block as a line of commas; the mod took that as the header, turned the real header (Arrow, Bolt, ...) into a row of -1s and wrote it back, after which every value was skipped ("Could not parse header '' as ProjectileType", Applied=0). Such lines are now ignored, and a file the mod cannot parse is left untouched with a clear error. If yours is already broken: put the attacker names back on the header line and delete the -1 row, or delete the CSV to regenerate it.
[/list]

[b]2.6.2:[/b]
[list]
[*][b]Gold-only recruits: WeaponType = "NONE".[/b] Set a European unit's WeaponType (and ArmorType, if it has one) to "NONE" to remove the equipment requirement and hire it for gold only, like the Arabian mercenaries. Deleting the line never did this - a missing key means "use the game default" and the line comes back on the next launch.
[*][b]RequiresHorse now works for Arabian and Bedouin mercenaries[/b] (Horse Archer, Camel Lancer, Heavy Camel, or any Mercenary Post unit). The game itself only checks horses for the 7 Barracks units, so the mod now enforces it: the hire is refused when you have no free stable horse, a batch order is trimmed to the horses available, and the recruit occupies a stable slot until it dies. The Mercenary Post hover shows no horse icon (that UI is hard-wired to the Barracks).
[*][b]Fix: stable linking scans all of your stables[/b], not just the first, and never links a unit twice.
[*][b]Fix: a misspelled WeaponType / ArmorType no longer silently makes the unit free[/b] - it is logged and the game value is kept.
[*][b]Built for Script Extender 1.44.0[/b] - update SE first, then this mod. Gatehouse settings remain disabled in this build.
[/list]

[b]2.6.1:[/b]
[list]
[*][b]Fixes the crash when starting a game after the 11 Aug game update.[/b] Two causes: the 2.8.0.1 patch broke Script Extender 1.40.0 (this build is made for SE 1.41.0), and applying the [Gatehouse] settings at match start crashed even on the new SE.
[*][b]Requires Script Extender 1.41.0[/b] - update SE first, then this mod.
[*][b]Gatehouse settings are temporarily ignored.[/b] GateHouseCloseDistance / GateHouseReOpenDistance crash the game if written on 2.8.0.1, so the mod skips them (a log warning tells you if your override is affected). Everything else applies normally. They return once the Script Extender ships a fix.
[/list]

[b]2.6.0:[/b]
[list]
[*][b]UnitHealthMultiplier now works on recruited troops.[/b] The health multiplier only reached units spawned into the world, so soldiers recruited at the Barracks or Mercenary Post (and assigned workers, and disbanded peasants) never got it. They do now, applied once the unit finishes transforming - including enemy troops recruited during a match.
[*][b]Horse units recruited mid-match now link to a stable[/b] the same way map-placed cavalry always did.
[*][b]A building cost you set to 0 is kept.[/b] Setting a structure's GoldCost (or any cost) to 0 for a free building used to get wiped on the next launch. It now sticks; only -1 means "leave the game default alone".
[*]Large internal cleanup and rebuild against the latest Script Extender (game v2.8). No change to your configs - your existing values are migrated automatically.
[/list]

[b]2.5.0:[/b]
[list]
[*][b]Unit MaxCount is now enforced for recruited troops.[/b] Caps previously ignored units recruited from the Barracks / Mercenary Post. An over-cap recruit is now refused before any gold is spent, and a "recruit as many as possible" order is trimmed to the room left. MaxCount = 0 means a unit genuinely cannot be recruited.
[*][b]Fix: building MaxCount no longer locks up over time.[/b] The cap was also counting buildings pending deletion, so a capped building type could become unplaceable after a minute of normal play. Fixed.
[*][b]Fix: DiseaseDamageMultiplier now actually changes disease damage[/b] - it was being overwritten right after it was applied.
[*][b]Removed: EnemyHealthModifier[/b] (added in 2.4.0). It wrote the lobby "Enemy Health" value after the session had already been built, so it never affected live AI health. It is removed rather than left as a silent no-op; an orphan EnemyHealthModifier line in an old config is harmless and is cleaned up on the next config regeneration.
[/list]

[b]2.4.1:[/b]
[list]
[*]Fixed harmless [i]Unknown property 'MaxCount'[/i] warnings flooding the BepInEx console on launch (one per unit and structure). Caps always worked; the log noise is gone now.
[*]The download no longer includes config files, so updating by extracting over your install can never overwrite your personalized configs again. Configs are generated on first launch and your existing values are migrated automatically on every update.
[/list]

[b]2.4.0:[/b]
[list]
[*]New gameplay options: [b]GlobalImprovedSiegeBehaviour[/b] and [b]GlobalMoreAggressiveSiegeBehaviour[/b] — force the game's AI siege behaviour toggles on from the config, re-applied on every map load.
[*]New: [b]EnemyHealthModifier[/b] — the game's "Enemy Health" advanced option as a config value: [b]-1[/b] = don't override, [b]0[/b] = Weak (66%), [b]1[/b] = Normal (100%), [b]2[/b] = Strong (125%), [b]3[/b] = Very Strong (150%). The vanilla way to scale enemy (AI) troop health only — per-unit Health stats apply to both sides.
[*]New: [b]AdvancedOptionsEnabled[/b] / [b]AdvancedSkirmishOptionsEnabled[/b] — the game's advanced-options master switches, exposed in the config and auto-enabled whenever you override any advanced option, so sub-options can't be silently ignored.
[/list]

[b]2.3.1:[/b]
[list]
[*]Building count caps ([b]MaxCount[/b]) now block placement [b]before[/b] the building is built — no more paying for a building and watching it vanish a frame later. A disabled building simply can't be placed, and loading a save no longer removes over-cap buildings you already had.
[/list]

[b]2.3.0:[/b]
[list]
[*]Unit & structure stat properties now default to [b]-1[/b] = "use the game's default value". The mod only changes the stats you actually set, instead of re-applying every value on load — much better compatibility with other mods. Each property shows the current game default in its [i]# Default:[/i] comment, refreshed every launch. Existing configs migrate automatically (a value equal to its default becomes -1; genuine overrides are preserved).
[/list]

[b]2.2.1:[/b]
[list]
[*]Fixed: per-unit and per-building count caps (MaxCount) were never enforced — they now work.
[*]MaxCount values changed: [b]-1[/b] = unlimited (new default), [b]0[/b] = disabled, [b]>0[/b] = limit. Your existing configs migrate automatically (old "0 = no limit" becomes -1). Set MaxCount = 0 on any unit/building to disable it.
[/list]

[b]2.2.0:[/b]
[list]
[*]Per-unit and per-building count caps — set [b]MaxCount[/b] in the Units or Structures TOML to limit how many you can have at once.
[*]Camp Peasants Cap — control max peasants waiting at the campfire.
[/list]

[b]Previous features:[/b]
[list]
[*]Killing Pit Damage config.
[*]Config migration — updating the mod no longer requires deleting your config files! Your values are preserved and new properties are added automatically on launch.
[*]Zero-cost cleanup — cost properties with a value of 0 are excluded from TOML configs to reduce clutter. You can still manually add them.
[*]Units that have a horse cost are now tracked by Stables same as knights. Stables horse amount and regen rate can be configured.
[*]Disease, Fire Damage, and Bedouin Heal configs (CSV matrices).
[*]Goods buy/sale trade values and auto-trader setup so that you can have it configured prior to a game.
[*]Gameplay options — allows Strong Walls in single-player, and more.
[/list]

[b]Updating:[/b]
Just install the new version and launch the game — your existing config values are kept automatically.

Config location: [b]BepInEx/config/CrusaderDETweaker/[/b]
