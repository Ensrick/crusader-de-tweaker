Version 2.3.1

Updated for SHC DE v2.7!

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
