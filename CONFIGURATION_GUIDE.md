[center][size=6][b]Crusader DE Tweaker (Unit Stat Editor)[/b][/size][/center]


[size=5][b]Reporting a Problem or Requesting a Feature[/b][/size]
Please use the issue tracker, not the comments: a comment cannot hold a log file, and without the log almost nothing can be investigated. Either site works, use the one you already have an account on. Each link opens a form that walks you through it step by step.
[list]
[*][b]Report a bug:[/b] [url=https://gitlab.com/ensrick7/crusader-de-tweaker/-/issues/new?issuable_template=Bug%20report]on GitLab[/url] (main repository) or [url=https://github.com/Ensrick/crusader-de-tweaker/issues/new?template=bug_report.yml]on GitHub[/url] (mirror)
[*][b]Request a feature:[/b] [url=https://gitlab.com/ensrick7/crusader-de-tweaker/-/issues/new?issuable_template=Feature%20request]on GitLab[/url] or [url=https://github.com/Ensrick/crusader-de-tweaker/issues/new?template=feature_request.yml]on GitHub[/url]
[*][b]Source code:[/b] [url=https://gitlab.com/ensrick7/crusader-de-tweaker]gitlab.com/ensrick7/crusader-de-tweaker[/url] (mirror: [url=https://github.com/Ensrick/crusader-de-tweaker]github.com/Ensrick/crusader-de-tweaker[/url]). Open source, MIT license.
[/list]
For a bug report, have these ready:
[list=1]
[*][b]The log.[/b] Launch the game, do the thing that fails once (for example: start a new skirmish, build a catapult, press restock, open the market), then quit and copy [b]BepInEx\LogOutput.log[/b] from the game folder. It is overwritten on every launch, so copy it right after that session. Attach it by dragging it into a text box of the form.
[*][b]The config file you edited[/b], the whole file (for example CrusaderDETweaker_GameplaySettings.toml).
[*][b]Versions:[/b] mod version (log line "Loading [Crusader DE Tweaker x.y.z]"), Script Extender version (log line "Loading [SHCDE-SE x.y.z]"), game version (bottom of the main menu).
[*][b]Game mode:[/b] new skirmish, loaded save, trail / campaign mission, or multiplayer. The mod applies settings at different moments in each.
[*][b]What you changed, what you expected, what happened[/b], with the exact keys and values.
[/list]
The log records every value the mod writes and, for trade prices, reads back from the game, so one log from one session usually shows exactly where a setting stops working.


[size=5][b]Getting Started (tutorial)[/b][/size]
[b]1. Install[/b]
[list=1]
[*]Install [url=https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/36]BepInEx 5 Bootstrapper[/url].
[*]Install [url=https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/35]Script Extender[/url] [b]2.8.0 or newer[/b]: extract SHCDESE.zip into the game folder.
[*]Extract this mod's zip into the game folder (it contains a BepInEx folder; merge it with the existing one).
[*]Launch the game once and go to the main menu, then quit. This creates the config files in [b]{GameDir}\BepInEx\config\CrusaderDETweaker\[/b].
[/list]
Check it worked: [b]BepInEx\LogOutput.log[/b] contains "Loading [Crusader DE Tweaker ...]" and "ALL 5 TEST SUITES PASSED". "missing dependencies: 000shcdese" means the Script Extender is not installed.

[b]2. Your first change: make Knights cost 5000 gold[/b]
[list=1]
[*]Open [b]CrusaderDETweaker_Units.toml[/b] in a text editor (Notepad works).
[*]Find the [b][CHIMP_TYPE_KNIGHT][/b] section and the line [b]GoldCost = -1 # Default: 40[/b].
[*]Change only the number: [b]GoldCost = 5000 # Default: 40[/b]. Keep the [b]#[/b]: everything after it is a comment, and a line without it (for example "GoldCost = 5000 Default: 40") is a syntax error that makes the game ignore the whole file.
[*]Save, then restart the game. TOML and CSV files are read at launch.
[*]Start a skirmish and recruit a Knight: it now costs 5000 gold.
[/list]
[b]-1[/b] means "use the game's value". Put -1 back to undo a change, or delete a config file to regenerate it with defaults.

[b]3. Change something without restarting[/b]
Open [b]CrusaderDETweaker_GlobalMultipliers.cfg[/b] and set, for example, [b]UnitHealthMultiplier = 1.5[/b]. Most multipliers apply while the game is running, no restart needed.

[b]4. Gameplay settings (siege, stealth, trade prices)[/b]
[b]CrusaderDETweaker_GameplaySettings.toml[/b] is applied at every session start: new game, trail / campaign map, and loading a saved game. Example, cheaper bows at the market:
[code]
["Trade Prices".STORED_BOWS]
BuyPrice = 52  # default: 155
SellPrice = 25  # default: 75
[/code]
In the log, look for "[Trade Prices] STORED_BOWS: buy 155 -> 52" to confirm it applied.

[b]5. Updating the mod[/b]
Replace only the [b]BepInEx\plugins\CrusaderDETweaker[/b] folder. Keep your config files: the mod never resets your values, it only adds new settings on launch. A syntax error is reported in the log as "SYNTAX ERROR" with the line number.

[b]6. Multiplayer[/b]
The lobby host's configs are sent to everyone who joins (tab [b]Crusader DE Tweaker[/b] in the lobby's Mod Options window); your own files are not changed and apply again when you leave. See "Multiplayer: host config sync" below.

The sections below list every config file and setting.


[size=5][b]Config Files Location[/b][/size]

All config files are in the [b]game directory[/b] (not %APPDATA%):
[b]{GameDir}\BepInEx\config\CrusaderDETweaker\[/b]

Example: [b]C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker\[/b]

Files:
[list]
[*]CrusaderDETweaker_GlobalMultipliers.cfg - Real-time global multipliers
[*]CrusaderDETweaker_GameplaySettings.toml - Gameplay flags, siege, stealth, trade prices, auto-trade
[*]CrusaderDETweaker_Units.toml - Per-unit stats (health, speed, cost, weapon/armor requirements, max count)
[*]CrusaderDETweaker_Structures.toml - Per-structure stats (health, cost, housing, max count)
[*]DamageMatrices\CrusaderDETweaker_MeleeDamage.csv - Unit vs unit melee damage
[*]DamageMatrices\CrusaderDETweaker_RangedDamage.csv - Projectile (arrow/bolt/slinger/javelin) damage per unit
[*]DamageMatrices\CrusaderDETweaker_EunuchAoeDamage.csv - Eunuch AOE damage per unit
[*]DamageMatrices\CrusaderDETweaker_BallistaDamage.csv - Ballista damage per unit
[*]DamageMatrices\CrusaderDETweaker_UnitFireDamage.csv - Fire damage per unit
[*]DamageMatrices\CrusaderDETweaker_BedouinHeal.csv - Bedouin healer amount per unit
[*]DamageMatrices\CrusaderDETweaker_BuildingFireDamage.csv - Fire damage per building type
[/list]

[b]Note:[/b] Config files are generated on first launch and migrated on updates — your values are preserved, and new properties are added automatically. Delete a file to reset it to defaults.

[b]Comments:[/b] everything after a [b]#[/b] is a comment. When you change a value, keep the [b]#[/b] in front of the "default:" note: [b]BuyPrice = 52  # default: 155[/b] is correct, [b]BuyPrice = 52 default: 155[/b] is a syntax error, and one syntax error makes the game ignore the [b]whole[/b] file (every setting in it stays vanilla). The log then shows [b]SYNTAX ERROR in ...[/b] with the line number.


[size=5][b]Load Order[/b][/size]
[code]
Game Defaults → TOML Configs → CSV Matrices → BepInEx Multipliers
[/code]
[list=1]
[*][b]Game Defaults[/b] - Base values from the game engine
[*][b]TOML Configs[/b] - Override unit/structure stats and gameplay settings (requires restart)
[*][b]CSV Matrices[/b] - Override specific damage matchups (requires restart)
[*][b]BepInEx Multipliers[/b] - Global scaling applied on top of everything else
[/list]


[size=5][b]1. CrusaderDETweaker_GlobalMultipliers.cfg[/b][/size]
[b]Global multipliers[/b] - most apply in real-time without a game restart.

[code]
[Debug]
DebugLogging = false   # Enable step-by-step logging for all event hooks. Real-time. High log volume — use only when diagnosing issues.

[Multipliers]
# Unit Multipliers
UnitHealthMultiplier = 1.0                 # All unit max health
UnitMeleeDamageTakenMultiplier = 1.0       # All melee damage taken by units (min 1)
UnitRangedDamageTakenMultiplier = 1.0      # Arrow/Bolt/Slinger/Javelin damage taken by units (min 1)

# Structure Multipliers
StructureDamageTakenMultiplier = 1.0       # All structure damage taken
WallDamageTakenMultiplier = 1.0            # Stone wall damage taken (stacks with StructureDamageTakenMultiplier)
TowerDamageTakenMultiplier = 1.0           # Tower and gatehouse damage taken (stacks with StructureDamageTakenMultiplier)
CivilStructureDamageTakenMultiplier = 1.0  # Civilian structure damage taken (non-tower, non-gatehouse)
DemolisherBuildingDamageMultiplier = 1.0   # Damage dealt by Bedouin Demolishers to structures
SapperBuildingDamageMultiplier = 1.0       # Damage dealt by Bedouin Sappers to structures

# Wall Cost Multipliers
LowWallCostMultiplier = 0.25               # Cost multiplier for short walls. Game default: 0.25
HighWallCostMultiplier = 0.5               # Cost multiplier for high/crenel walls. Game default: 0.5

# Fire, Heal & Disease Multipliers
UnitFireDamageTakenMultiplier = 1.0        # Fire damage taken by all units
StructureFireDamageTakenMultiplier = 1.0   # Fire damage taken by all structures
BedouinHealMultiplier = 1.0                # Healing amount from Bedouin healers
DiseaseDamageMultiplier = 1.0              # Scales all three disease damage tiers (stacks with [Disease] TOML values)
[/code]

[b]Multipliers stack.[/b] Example: WallDamageTakenMultiplier = 0.5 and StructureDamageTakenMultiplier = 2.0 → walls take 1.0× damage (0.5 × 2.0).

[size=5][b]2. CrusaderDETweaker_GameplaySettings.toml[/b][/size]
[b]Gameplay-wide settings[/b] — covers siege engines, stealth, gates, flags, trade, and auto-trade.

[b]Siege Engines[/b]
[code]
["Siege Engines"]
SiegeEngineRestockStoneAmount = 20  # Stone units added per restock
SiegeEngineRestockStoneCost = 10    # Gold cost per restock
[/code]

[b]Stealth[/b]
[code]
[Stealth]
StealthDetectionRange = 160           # Range at which assassins are detected
StealthTransparencyThreshold = 120    # Transparency level below which assassins are hidden
[/code]

[b]Stables[/b]
[code]
[Stables]
StablesHorseRegenTickTarget = 550     # Ticks before a horse charge regenerates. Lower = faster regen.
StablesHorsesCap = 4                  # Max horses tracked by the stable. WARNING: values above 4 break horse-link tracking.
[/code]

[b]Gatehouse[/b]
[code]
[Gatehouse]
GateHouseCloseDistance = 200          # Distance at which gates close to enemies
GateHouseReOpenDistance = 1200        # Distance at which gates re-open after threat passes
[/code]

[b]Disease[/b]
[code]
[Disease]
DiseaseDamage1 = 150    # Damage tier 1 (lowest)
DiseaseDamage2 = 200    # Damage tier 2
DiseaseDamage3 = 400    # Damage tier 3 (highest)
[/code]

[b]Gameplay Options[/b]

[b]All options only take effect when set to true.[/b] false means "leave the game default unchanged" — it does not actively disable anything.
[code]
["Gameplay Options"]
BetterHealers = false           # Improve healer unit effectiveness
FasterPeasants = false          # Increase peasant movement speed
ImprovedArabSwordsman = false   # Buff Arab Swordsman
ImprovedFletchers = false       # Buff Fletcher units
ImprovedLadderman = false       # Buff Ladderman
ImprovedSpearman = false        # Buff Spearman
NerfEunuchs = false             # Reduce Eunuch effectiveness
NoKnockdownWalls = false        # Walls cannot be knocked down (re-applied on each map load)
RebalancedHorseArchers = false  # Rebalance Horse Archer stats
UncappedPeasants = false        # Remove peasant population cap
# AI siege behaviour toggles — applied on each map load
GlobalImprovedSiegeBehaviour = false
GlobalMoreAggressiveSiegeBehaviour = false
# Master switches for the game's advanced options. Auto-enabled whenever any advanced
# option above is set, so you normally don't need to touch these.
AdvancedOptionsEnabled = false
AdvancedSkirmishOptionsEnabled = false
# Override map restrictions — only takes effect when set to true
AllBuildingsAvailable = false
AllUnitsAllowed = false
AllTradeGoodsAllowed = false
AllProductionGoodsAllowed = false
[/code]

[b]Pathfinding[/b]
[code]
[Pathfinding]
PathfindingMaxTilesConstraint = 2000  # Max tiles a unit can pathfind through. Higher = longer paths allowed.
[/code]

[b]Food Consumption[/b]
[code]
["Food Consumption"]
FoodConsumptionTickThreshold = 15000  # Ticks between food consumption evaluations. Higher = slower eating.
FoodConsumptionRate = 3               # Base rate multiplier for food consumption per tick.
[/code]

[b]Peasant Spawning[/b]
[code]
["Peasant Spawning"]
PeasantRespawnTickTargetValue = 4000  # Ticks before a new peasant spawns. Lower = faster spawning.
PeasantRespawnTickResetValue = 2000   # Tick counter reset value after a spawn.
CampPeasantsCap = 24                  # Max peasants waiting at the campfire. 0 = no cap.
[/code]

[b]Trade Prices[/b]

Override the market buy/sell price for individual goods. [b]0 = use the game's default price[/b] (no override). Requires a Trade Post. Re-applied at every session start: new game, trail / campaign map, and loading a saved game (v2.6.5+; earlier versions skipped loaded saves). Every applied price is logged with a read-back from the game, e.g. [b][Trade Prices] STORED_BOWS: buy 155 -> 52, sell 75 -> 25 (read back 52/25)[/b].
[code]
["Trade Prices".STORED_WOOD_PLANKS]
BuyPrice = 0   # default: 20  — gold paid when buying
SellPrice = 0  # default: 5   — gold received when selling
[/code]
Buy and sell prices are configured independently. Setting only one leaves the other at the game default.

[b]Game default prices:[/b]
[code]
Good                   Buy   Sell
STORED_WOOD_PLANKS      20      5
STORED_RAW_HOPS         75     40
STORED_STONE_BLOCKS     70     35
STORED_IRON_INGOTS     225    115
STORED_PITCH_REFINED   100     50
STORED_RAW_WHEAT       115     40
STORED_FOOD_BREAD       40     20
STORED_FOOD_CHEESE      40     20
STORED_FOOD_MEAT        40     20
STORED_FOOD_FRUIT       40     20
STORED_FOOD_ALE        100     50
STORED_FLOUR           160     50
STORED_BOWS            155     75
STORED_CROSSBOWS       290    150
STORED_SPEARS          100     50
STORED_PIKES           180     90
STORED_MACES           290    150
STORED_SWORDS          290    150
STORED_LEATHER_ARMOUR  125     60
STORED_METAL_ARMOUR    290    150
[/code]

[b]Auto Trade[/b]

Automatically buy/sell goods via the market at map start. Requires a Trade Post.
[list]
[*][b]BuyLevel[/b] — auto-buy when stock falls BELOW this amount (0 = disabled)
[*][b]SellLevel[/b] — auto-sell when stock rises ABOVE this amount (0 = disabled)
[/list]
[code]
["Auto Trade".STORED_WOOD_PLANKS]
Enabled = false
BuyLevel = 0
SellLevel = 0

["Auto Trade".STORED_SWORDS]
Enabled = true
BuyLevel = 10   # buy swords when you have fewer than 10
SellLevel = 0
[/code]


[size=5][b]3. CrusaderDETweaker_Units.toml[/b][/size]
[b]Per-unit stats[/b] — one section per unit type. Each numeric stat defaults to [b]-1[/b] = "use the game default": the mod leaves that stat unchanged. The game's current value is shown in the [b]# Default:[/b] comment, refreshed every launch. Set any real number to override.

[code]
[CHIMP_TYPE_KNIGHT]
Health = -1 # Default: 20000       # -1 = use game default; set a number to override
Speed = -1 # Default: 1
GoldCost = 5000 # Default: 40       # example override: 5000-gold Knights
WeaponType = "STORED_SWORDS"       # equipment requirements are set directly (no -1); "NONE" = no weapon needed
ArmorType = "STORED_METAL_ARMOUR"  # "NONE" = no armour needed (gold-only hire, like Arabian mercenaries)
RequiresHorse = true               # hiring needs a free stable horse; the unit occupies that slot
MaxCount = 20                      # cap (separate convention: -1 = unlimited, 0 = disabled)

[CHIMP_TYPE_ARCHER]
Health = -1 # Default: 2500
Speed = -1 # Default: 1
GoldCost = -1 # Default: 12
WeaponType = "STORED_BOWS"
MaxCount = -1                      # -1 = unlimited (default)
[/code]

[b]Note:[/b] [b]-1[/b] means "don't touch this stat" — only stats you give a real number are overridden. (Existing configs migrate automatically: a value that equals its default becomes -1; your genuine overrides are kept.) This is the same idea as Trade Prices' [b]0 = use default[/b], using -1 because 0 is a valid stat value.

[b]Available Properties:[/b]
[list]
[*][b]Health[/b] — Unit max health
[*][b]Speed[/b] — Movement speed (some special units cannot be modified)
[*][b]GoldCost[/b] — Recruitment cost (Crusader recruitable units only)
[*][b]WeaponType[/b] — Weapon resource: STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS, STORED_PIKES, STORED_MACES, STORED_SPEARS, or [b]"NONE"[/b] for no weapon requirement (gold-only hire, like the Arabian mercenaries). Deleting the line does NOT remove the requirement: a missing key means "use the game default" and the line is re-added on the next launch.
[*][b]ArmorType[/b] — Armor resource: STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR, or [b]"NONE"[/b].
[*][b]ShieldHealth[/b] — Bedouin Demolisher only: shield durability (game default 40000). The game stores it in 16 bits, so [b]65535 is the maximum[/b]; a larger value is clamped to 65535 and a warning is logged.
[*][b]RequiresHorse[/b] — When true, hiring needs a free stable horse and the unit occupies that stable slot until it dies. Add [b]RequiresHorse = true[/b] to any recruitable unit's section: Barracks units are checked by the game itself, Mercenary Post units (Horse Archer, Camel Lancer, Heavy Camel, ...) by the mod at hire time (the hire is refused when no horse is free; a batch request is trimmed to the free horses). The Mercenary Post hover shows no horse icon - that UI is hard-wired to the Barracks.
[*][b]MaxCount[/b] — Limit on units of this type alive at once for the local player. [b]-1[/b] = unlimited (default), [b]0[/b] = disabled (cannot be recruited at all), [b]>0[/b] = max alive at once. Recruiting at/over the cap is refused up front (no gold spent); a request for more than the remaining room is trimmed to fit.
[/list]

[b]Note:[/b] Damage values are not set here. All combat damage is controlled by the CSV matrices below.


[size=5][b]4. CrusaderDETweaker_Structures.toml[/b][/size]
[b]Per-structure stats[/b] — one section per building type.

[code]
[STRUCT_BARRACKS]
Health = -1 # Default: 400        # -1 = use game default; set a number to override
GoldCost = -1 # Default: 15
MaxCount = -1                     # -1 = unlimited (default)

[STRUCT_HOVEL]
Health = -1 # Default: 100
WoodCost = 3 # Default: 6          # example override: cheaper Hovels
HousingPopulationSpace = -1 # Default: 8
MaxCount = 3                      # cap: limit Hovels to 3
[/code]

Same as units: each numeric stat defaults to [b]-1[/b] = "use the game default" (unchanged); set a real number to override. Cost properties with a default of 0 are omitted for cleanliness — you can add them manually (e.g. [b]StoneCost = 10[/b]) and the mod will apply them.

[b]Available Properties:[/b] Health, GoldCost, WoodCost, StoneCost, IronCost, PitchCost, HousingPopulationSpace, MaxCount

[b]MaxCount[/b] for buildings: [b]-1[/b] = unlimited (default), [b]0[/b] = disabled, [b]>0[/b] = max placed at once. Unlike units, building placement is refused [b]up front[/b] (like the game's own placement rules) — no resources are spent, and loading a save never removes buildings you already had.

[b]Note:[/b] Wall costs are controlled by [b]LowWallCostMultiplier[/b] and [b]HighWallCostMultiplier[/b] in the CFG file, not here.


[size=5][b]5. CSV Damage Matrices[/b][/size]
[b]Surgical damage overrides[/b] — override specific attacker vs. defender pairs.

[b]Tip:[/b] Open CSV files in Excel or Google Sheets — they're hard to read as plain text.

[b]MeleeDamage.csv[/b] — Melee damage: rows = defender, columns = attacker
[code]
,CHIMP_TYPE_KNIGHT,CHIMP_TYPE_SWORDSMAN,CHIMP_TYPE_ARCHER
CHIMP_TYPE_KNIGHT,50,80,80
CHIMP_TYPE_SWORDSMAN,50,100,100
CHIMP_TYPE_ARCHER,25,25,25
[/code]

[b]RangedDamage.csv[/b] — Projectile damage: rows = defender, columns = projectile type (Arrow, Bolt, Slinger, Javelin)
[code]
,Arrow,Bolt,Slinger,Javelin
CHIMP_TYPE_KNIGHT,150,2500,150,200
CHIMP_TYPE_ARCHER,2500,2500,2500,2500
[/code]

[b]Other matrices[/b] follow the same row/column format:
[list]
[*][b]EunuchAoeDamage.csv[/b] — Eunuch AOE damage per defender unit
[*][b]BallistaDamage.csv[/b] — Ballista projectile damage per defender unit
[*][b]UnitFireDamage.csv[/b] — Fire damage per unit type
[*][b]BedouinHeal.csv[/b] — Heal amount from Bedouin healers per unit type
[*][b]BuildingFireDamage.csv[/b] — Fire damage per building type
[/list]

[b]Note:[/b] CSV files are only generated if missing. Existing files are never overwritten — your edits are safe.


[size=5][b]Multiplayer: host config sync[/b][/size]
[b]New in 2.7.0, not yet tested in a real multiplayer match.[/b] In a multiplayer lobby, everyone plays with the [b]host's[/b] Crusader DE Tweaker configs. Nobody has to copy config files before a match.

[b]What is sent:[/b] the host's Units, Structures and GameplaySettings files, all 7 damage matrices, and the [b][Multipliers][/b] values of the CFG file ([b][Debug][/b] stays local). The host's files as they were when the host started the game: after editing configs, the host restarts the game before opening the lobby.

[b]The switch:[/b] open the lobby's [b]Mod Options[/b] window, tab [b]Crusader DE Tweaker[/b]. [b]Players who join use my configs[/b] is on by default and only the host can change it; players see it greyed out. Turning it off (or on) in the lobby takes effect for everyone immediately.

[b]Your own files are never changed.[/b] A player's copy of the host's files is stored in [b]{GameDir}\BepInEx\config\CrusaderDETweaker\HostSync\[/b] and used only for that lobby's matches. The multiplier values are applied in memory, the CFG file is not saved. When you leave the lobby, the match ends, or the host turns sync off, your own configs apply again, no restart needed. One exception: a GameplaySettings value your own file leaves at "no override" (a [b]false[/b] gameplay option, a [b]0[/b] trade price) may keep the host's value until you restart the game.

[b]Versions:[/b] host and players need the same major.minor version (for example 2.7.0 and 2.7.1 work together; 2.7 and 2.8 do not). A host on an older version, or without the mod, sends nothing: everyone keeps their own configs.

[b]Status line[/b] in the tab:
[list]
[*][b]Using the host's configs for this lobby.[/b] - synced; the line below shows a short hash, the same one the host's log shows.
[*][b]Host has config sync turned off[/b] - everyone uses their own configs.
[*][b]Nothing received from the host[/b] - the host runs an older version (or no Crusader DE Tweaker), or has sync off.
[*][b]... were rejected (reason)[/b] / [b]incompatible Crusader DE Tweaker[/b] - your own configs apply; the reason is in the log.
[/list]
[b]In the log[/b] ([b]BepInEx\LogOutput.log[/b]) every step starts with [b][ConfigSync][/b]: "Packed your configs" (host, at launch), "Received the host's configs ... Verified OK", "Applied the host's configs", "match starting with the host's configs", "Reverted to your own configs". Include these lines in a bug report.

Single player and skirmish are not affected.


[size=5][b]Quick Tips[/b][/size]
[list]
[*][b]TOML and CSV changes require a game restart[/b]
[*][b]Most CFG settings apply in real-time[/b] — multipliers require no restart (fire/heal multipliers are the exception)
[*][b]Delete a config file to reset it[/b] — it will be regenerated with defaults on next launch
[*][b]Multipliers stack[/b] — UnitMeleeDamageTakenMultiplier and StructureDamageTakenMultiplier both apply independently
[*][b]Minimum damage is always 1[/b] — setting multipliers to 0 still results in 1 damage
[*][b]Wall costs[/b] use multipliers in the CFG, not values in the TOML
[*][b]Auto Trade[/b] requires a Trade Post and is re-applied at every session start (including a loaded save)
[*][b]Check BepInEx\LogOutput.log[/b] in the game directory for errors
[/list]
