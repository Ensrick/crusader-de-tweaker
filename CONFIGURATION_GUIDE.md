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
[*]Install [url=https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/35]Script Extender[/url] [b]2.10.1 or newer[/b]: extract SHCDESE.zip into the game folder.
[*]Extract this mod's zip into the game folder (it contains a BepInEx folder; merge it with the existing one).
[*]Launch the game once and go to the main menu, then quit. This creates the config files in [b]{GameDir}\BepInEx\config\CrusaderDETweaker\[/b].
[/list]
Check it worked: [b]BepInEx\LogOutput.log[/b] contains "Loading [Crusader DE Tweaker ...]" and "ALL 10 TEST SUITES PASSED". "missing dependencies: 000shcdese" means the Script Extender is not installed.

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

[b]6b. Unit and building limits in the lobby[/b]
The same tab has a box for each unit and building: type a MaxCount there instead of editing the Units / Structures files, in single-player skirmish too. See "Limits in the lobby (MaxCount)" below.

[b]7. Ranged units: range[/b]
Archers, crossbowmen, slingers, siege engines and the other ranged units have [b]AttackRange[/b] (the farthest they pick a target and shoot, in tiles) and [b]EngageRange[/b] (how far away an idle unit notices an enemy) in the Units file, for example [b]AttackRange = 80[/b] under [b][CHIMP_TYPE_ARCHER][/b]. Raising AttackRange alone is enough to make them shoot from farther away: with EngageRange left at -1, the distance at which idle units react grows with it (2.9.1; measured in game: an idle Archer with AttackRange 80 first shot and hit an enemy 72 tiles away, 48 with the game's values). See the Units section below.

The sections below list every config file and setting.


[size=5][b]Config Files Location[/b][/size]

All config files are in the [b]game directory[/b] (not %APPDATA%):
[b]{GameDir}\BepInEx\config\CrusaderDETweaker\[/b]

Example: [b]C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker\[/b]

Files:
[list]
[*]CrusaderDETweaker_GlobalMultipliers.cfg - Real-time global multipliers
[*]CrusaderDETweaker_GameplaySettings.toml - Gameplay flags, siege, stealth, team colors, trade prices, auto-trade
[*]CrusaderDETweaker_Units.toml - Per-unit stats (health, speed, cost, weapon/armor requirements, max count)
[*]CrusaderDETweaker_Structures.toml - Per-structure stats (health, cost, housing, max count)
[*]DamageMatrices\CrusaderDETweaker_MeleeDamage.csv - Unit vs unit melee damage
[*]DamageMatrices\CrusaderDETweaker_RangedDamage.csv - Projectile (arrow/bolt/slinger/javelin) damage per unit
[*]DamageMatrices\CrusaderDETweaker_EunuchAoeDamage.csv - Eunuch AOE damage per unit
[*]DamageMatrices\CrusaderDETweaker_BallistaDamage.csv - Ballista damage: one Default value for every unit not listed, plus siege engines and portable shields
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
BedouinHealMultiplier = 1.0                # Healing amount from Bedouin healers. 0 = they heal nothing (no healing in battle)
DiseaseDamageMultiplier = 1.0              # Scales all three disease damage tiers (stacks with [Disease] TOML values)
[/code]

[b]Multipliers stack.[/b] Example: WallDamageTakenMultiplier = 0.5 and StructureDamageTakenMultiplier = 2.0 → walls take 1.0× damage (0.5 × 2.0).

[size=5][b]2. CrusaderDETweaker_GameplaySettings.toml[/b][/size]
[b]Gameplay-wide settings[/b] — covers siege engines, stealth, gates, flags, team colors, skirmish starting troops, trade, and auto-trade.

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
[list]
[*][b]Where they apply:[/b] skirmish, trails, the campaign and loaded saves. The game counts an advanced option (BetterHealers, FasterPeasants, ImprovedArabSwordsman, ImprovedFletchers, ImprovedLadderman, ImprovedSpearman, NerfEunuchs, RebalancedHorseArchers, UncappedPeasants) only while that mode's master switch is on: [b]AdvancedSkirmishOptionsEnabled[/b] in skirmish and trails, [b]AdvancedOptionsEnabled[/b] in the campaign. The game turns both off at the start of every campaign and trail mission (only the skirmish lobby and coop trails turn them on), so the mod turns both on itself, after the game's reset, whenever one of these options is true. You do not need to set them.
[*]Each session start logs the switches, e.g. [b][GameplayOptions] ImprovedSpearman (session start): master switches AdvancedOptions (campaign, editor) = True, AdvancedSkirmishOptions (skirmish, trails) = True (raised: the game had turned them off)[/b].
[*]Measured in game (a custom skirmish started by the windowless self-test): after the mod's session start both switches were on, and a Spearman with ImprovedSpearman ordered to attack used the game's improved run (running animation, speed bonus 1). With AdvancedSkirmishOptions off it did not (the situation in a regular trail before the mod turned the switch on); with the game set to the campaign rule, AdvancedOptions on made it run.
[/list]

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

[b]Army Size[/b] (v2.10.0+)

How many units the whole map can hold: one shared pool for every player's troops, workers and animals. [b]-1 = the game's value: 3000[/b], or 10000 when Extreme troops is on. Allowed values: 1000 to 10000 (10000 is the size of the game's unit table; a higher value is lowered to 10000 with a warning).
[code]
["Army Size"]
UnitLimit = 10000  # default: 3000
[/code]
[list]
[*][b]Each player's troop limit comes from it.[/b] In skirmish and trails: (UnitLimit - 100) / number of players - 40. With the game's 3000 that is 322 troops per player with 8 players and 1410 with 2 players; with 10000 it is 1197 and 4910. In the campaign, free build and invasion the player's limit is UnitLimit itself. The game recomputes these limits every moment from UnitLimit, so they follow it.
[*]The troop limit blocks the human player's recruiting (barracks, mercenary post, engineers guild, tunnelers, Bedouin units). The AI is held back only by the shared pool, except in online multiplayer.
[*][b]Side effects above 3000:[/b] the game uses "more than 3000" as its own Extreme troops test, so some Extreme troops rules also apply: outposts use their Extreme timing, and a per-player meter (probably the Extreme power bar) fills three times faster.
[*]Applied at every session start (new game, trail / campaign map, loaded save) and put back to the game's value when the map is unloaded. A game saved with a raised limit keeps that limit when loaded.
[*]Lowering it below the number of units already on the map does not remove any unit; new units simply cannot appear until enough are gone.
[*]Multiplayer: every player needs the same value (host config sync sends the host's file to everyone). The value is part of the game's own state check, so a different value very likely ends in a desync.
[*]Not possible yet: a per-player troop limit beyond the formula above (it would need a hook on the game's per-tick limit function).
[*]The log line [b][UnitLimit] Unit limit 10000 (game 3000) (session start); read back 10000.[/b] confirms it.
[*]Measured in game (a custom skirmish with 2 players, started by the windowless self-test): UnitLimit = 4000 gave each player a troop limit of 1910 (1410 with the game's 3000), and the game did not switch to Extreme troops. In the map editor, with the unit limit set so that only 3 unit slots were free, 3 of 6 new units were created and the other 3 were refused.
[/list]

[b]Team Colors[/b] (v2.9.0+)

Changes a player colour: the team colour on units, lords, siege engines and flags, and the player's colour in chat, player names and the allies / score panels. One value per lobby colour (Red, Blue, Orange, Yellow, Purple, Grey, LightBlue, Green), not per player: whoever picks Blue in the lobby gets the Blue value. [b]-1 = keep the game's colour.[/b]
[code]
["Team Colors"]
Red = [255, 0, 0]   # [Red, Green, Blue], each 0-255
Blue = "#2050FF"    # or a hex string, in quotes
Yellow = -1         # default: [255, 210, 35] on units, [198, 195, 0] in the interface
[/code]
[list]
[*]The game uses different colours for units and for the interface (both are in each line's "# default:" note); your value is used for both.
[*]On units the game multiplies this colour into the cloth's shading, so units and chat can show the same value differently.
[*]Not changed: the minimap and the colour shields in the lobby.
[*]Applied in the menus and at every session start, no restart needed after editing; setting a colour back to -1 restores the game's colour at the next session start.
[*]A value outside 0-255 is clamped with a warning; an unreadable value is logged ([b][TeamColors] Red: ...[/b]) and that colour stays the game's. The log line [b][TeamColors] Applied (session start): Red=[255, 0, 0], ...[/b] confirms it.
[*]In multiplayer with host config sync, the host's colours are used by everyone in the lobby.
[/list]

[b]Apothecary Healing[/b] (UCP-style healing; v2.10.0+)

Every apothecary heals its owner's wounded soldiers and Lord standing near it, out of combat, like the Unofficial Crusader Patch's "Healer heals casualties". [b]Off by default:[/b] while HealPercent and HealHitPoints are both -1 the apothecary does what it does in the game (it only cures plague). Example with healing on:
[code]
["Apothecary Healing"]
HealPercent = 5         # -1 = off; % of the unit's max health per heal (up to 100, decimals allowed)
HealHitPoints = -1      # -1 = off; health points per heal (added to HealPercent if both are set)
RadiusTiles = 10        # how far from the apothecary's walls a unit is healed, in tiles
IntervalTicks = 100     # one heal every this many game ticks (minimum 1)
OutOfCombatTicks = 200  # a unit that hit or was hit (melee or projectile) waits this many ticks; 0 = heals during fights too
NeedsWorker = true      # true = only an apothecary that has its worker heals
[/code]
[list]
[*]Healed: soldiers (every unit you can recruit, mercenaries included) and the Lord, of the apothecary's owner only, standing within RadiusTiles of the building. Not healed: workers, siege engines, animals, allies' and enemies' units.
[*]Each heal adds HealPercent of the unit's max health plus HealHitPoints, never above max health; the health bar updates the way the game's own healing updates it. A unit near two apothecaries still heals once per interval.
[*]There is no walking healer: the Script Extender cannot drive the apothecary worker's own behaviour, so the building heals everyone in range at the same moment.
[*]"Out of combat" means: no melee hit and no projectile hit taken or dealt for OutOfCombatTicks. It counts from the last hit, so a unit in a long fight waits until the fight is over. Fire, disease, Eunuch fire pots and other damage do not count as combat.
[*]A switched-off apothecary never heals. With NeedsWorker = true an apothecary without its worker does not heal either.
[*]To stop healing in battle altogether, also set [b]BedouinHealMultiplier = 0[/b] in CrusaderDETweaker_GlobalMultipliers.cfg: Bedouin healers then heal nothing (or set single units to 0 in DamageMatrices\CrusaderDETweaker_BedouinHeal.csv).
[*]Applied at every session start (new game, trail / campaign map, loaded save). The log shows [b][ApothecaryHealing] On (session start): 5% of max health every 100 ticks within 10 tiles, ...[/b] and the first heal of each map ([b][ApothecaryHealing] First heal this map: ...[/b]).
[*]In multiplayer with host config sync, the host's values are used by everyone; every player's game heals the same units on the same ticks.
[/list]

[b]Skirmish Starting Troops[/b] (v2.10.0+)

The soldiers each player receives at the start of a custom skirmish, single player or multiplayer. The game delivers them in groups of up to 9 every 200 game ticks. One table per start option of the lobby: [b]Normal[/b], [b]Crusader[/b], [b]Deathmatch[/b]. [b]-1 = the game's number[/b] of that unit; 0 or more = exactly that many for every human player, whatever the lord (0 removes the unit). Maximum 1000.
[code]
["Skirmish Starting Troops"]
ApplyToAI = false   # true = AI lords get these numbers too, instead of the starting troops of their AI file

["Skirmish Starting Troops".Normal]
# Game (before the AI advantage scaling): European lord 5 Archer + 7 Spearman; Arabian lord 6 ArabianBow + 6 Slave; Bedouin lord 10 Skirmisher
Archer = 20         # 20 Archers for every human player
Spearman = -1       # the game's number (7 for a European lord, 0 for the others)
Knight = 4
[/code]
[list]
[*]Units: Archer, Crossbowman, Spearman, Pikeman, Maceman, Swordsman, Knight, Engineer, Monk, ArabianBow, Slave, Slinger, Assassin, HorseArcher, ArabianSwordsman, FireThrower, ArabianBallista, CamelLancer, BedouinHealer, Eunuch, Ambusher, Skirmisher, HeavyCamel, Sapper, Demolisher (the 25 kinds the game's own delivery can hand out).
[*]The comment above each table lists the game's own numbers, read from the game at launch. The game scales them with the lobby's AI advantage setting; a number you set is used as it is.
[*]Not changed: trails and their missions, campaign missions, and loading a saved game.
[*]Multiplayer: everyone needs the same values; host config sync sends this file to the players.
[*]The log shows every skirmish start: [b][Skirmish Starting Troops] Normal start: player 1 (human): Archer 5 -> 20; player 2 (AI) unchanged (ApplyToAI = false)[/b], or why nothing was changed. If a game update moves the troop table the setting switches itself off and the log says so.
[*]How it works: at every skirmish start the game fills a troop queue for each player from its table (humans) or the AI file (AI lords); the mod replaces the numbers you set in that queue right after the game filled it. Measured in a real skirmish: with Archer = 3, Spearman = 0, Knight = 2 the human player received exactly 3 Archers, no Spearmen and 2 Knights (the game: 5 Archers + 7 Spearmen), while the AI lord kept its own troops.
[/list]

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
AttackRange = -1 # Default: <game value>   # tiles; ranged units only (2.8.0)
EngageRange = -1 # Default: <game value>   # tiles; ranged units only
MaxCount = -1                      # -1 = unlimited (default)
[/code]

[b]Note:[/b] [b]-1[/b] means "don't touch this stat" — only stats you give a real number are overridden. (Existing configs migrate automatically: a value that equals its default becomes -1; your genuine overrides are kept.) This is the same idea as Trade Prices' [b]0 = use default[/b], using -1 because 0 is a valid stat value.

[b]Available Properties:[/b]
[list]
[*][b]Health[/b] — Unit max health
[*][b]Speed[/b] — Movement delay, [b]0 to 30, smaller = faster[/b] (0 = fastest). It is the number of game ticks a unit waits between steps (the Catapult's 6 is the slowest value in the unmodded game). Values above 6 need 2.9.0 or newer (the Script Extender's own limit is 6; the game has none). A change applies to units created after the game starts: units already on the map or in a saved game keep the speed they were created with. Workers, animals, the lord, engineers and miners switch to the game's own speeds while working, so for them Speed only sets the starting value. Siege engines (Catapult, Siege Tower, Battering Ram, Portable Shield) use it while they move. Trebuchet, Mangonel and Ballista do not move and have no Speed.
[*][b]GoldCost[/b] — Recruitment cost (Crusader recruitable units only)
[*][b]WeaponType[/b] — Weapon resource: STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS, STORED_PIKES, STORED_MACES, STORED_SPEARS, or [b]"NONE"[/b] for no weapon requirement (gold-only hire, like the Arabian mercenaries). Deleting the line does NOT remove the requirement: a missing key means "use the game default" and the line is re-added on the next launch.
[*][b]ArmorType[/b] — Armor resource: STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR, or [b]"NONE"[/b].
[*][b]KnightRunSpeedBonus / ArabHorsemanRunSpeedBonus / BedouinCamelLancerRunSpeedBonus / BedouinHeavyCamelRunSpeedBonus[/b] (only in that unit's section) — how much faster the mounted unit is while it runs. [b]Higher = faster[/b], the opposite of Speed; the game's value is 2 for all four. 0 = walking pace. Gains get smaller as the value rises (traced in the game code). Values of 32768 and above count as negative and stop the unit while it runs, so stay well below that. It does not change a normal move order: a Knight walked 20 tiles in the same time at 0, 2 and 8 (311-315 ticks, measured in game); the game applies the bonus only in one AI state of the unit.
[*][b]ShieldHealth[/b] — Bedouin Demolisher only: shield durability (game default 40000). The game stores it in 16 bits, so [b]65535 is the maximum[/b]; a larger value is clamped to 65535 and a warning is logged.
[*][b]RequiresHorse[/b] — When true, hiring needs a free stable horse and the unit occupies that stable slot until it dies. Add [b]RequiresHorse = true[/b] to any recruitable unit's section: Barracks units are checked by the game itself, Mercenary Post units (Horse Archer, Camel Lancer, Heavy Camel, ...) by the mod at hire time (the hire is refused when no horse is free; a batch request is trimmed to the free horses). The Mercenary Post hover shows no horse icon - that UI is hard-wired to the Barracks.
[*][b]GoodYieldMultiplier[/b] (workers only, new in 2.10.0) - how many goods a worker carries back per work cycle, as a multiple of the game's amount: [b]2[/b] = twice as much, [b]0.5[/b] = half, [b]-1[/b] or [b]1[/b] = the game's amount. Greater than 0, at most 100. Fractions add up over the trips: 1.25 on a trip of 3 gives 3, 4, 4, 4. The game's productivity bonus is added on top of the multiplied amount, and one trip is at most 32767. Workers: Woodcutter, Fletcher, Hunter, Quarry Grunt (stone), Pitchman, Wheat / Hops / Apple / Dairy farmer, Miller, Baker, Brewer, Poleturner, Blacksmith, Armourer, Tanner, Miner (iron; [b]CHIMP_TYPE_MINER2[/b]). The Quarry Mason, the quarry Ox and [b]CHIMP_TYPE_MINER1[/b] never deliver goods themselves, so they do not get the setting. Measured in game: a Woodcutter with GoodYieldMultiplier = 2 carried 24 planks instead of 12, and the Stockpile gained exactly 24 wood.
[*][b]MaxCount[/b] — Limit on units of this type alive at once for the local player. [b]-1[/b] = unlimited (default), [b]0[/b] = disabled (cannot be recruited at all), [b]>0[/b] = max alive at once. Recruiting at/over the cap is refused up front (no gold spent); a request for more than the remaining room is trimmed to fit. A value typed into the lobby tab replaces this one (see "Limits in the lobby (MaxCount)").
[*][b]AttackRange[/b] (ranged units only, new in 2.8.0) - how far the unit picks targets and, for normal arrows, bolts, stones and siege shots, how far the projectile flies, in [b]map tiles[/b]. The [b]# Default:[/b] comment shows the game's current range. Maximum 32767 (larger values are clamped with a warning). Units: Archer, Arabian Bow (Arab archer), Horse Archer, Crossbowman, Slinger, Fire Thrower (grenadier), Bedouin Ambusher, Bedouin Skirmisher, Catapult, Trebuchet, Mangonel, Ballista, Arabian Ballista. While [b]EngageRange[/b] is -1, the distance at which idle units react is scaled with AttackRange (game value x your AttackRange / game AttackRange), so a higher AttackRange really makes idle soldiers start shooting from farther away (2.9.1).
[*][b]EngageRange[/b] (ranged units only, new in 2.8.0, reworked in 2.9.1) - in [b]map tiles[/b]: how far away an enemy can be when an idle unit notices it and starts to engage. The [b]# Default:[/b] comment shows the game's value: 50 tiles for archers, crossbowmen, Arabian bows, slingers, fire throwers and skirmishers, 54 for horse archers and Bedouin heavy camels, 85 for ballistas. [b]-1[/b] = the game's value, scaled with AttackRange when you set one. EngageRange does not change how far the unit can shoot: an enemy noticed beyond AttackRange is only shot once it comes within AttackRange (measured: EngageRange 80 with the game's AttackRange 54 still first shot at 48 tiles). Maximum 4000 tiles. Units: Archer, Arabian Bow, Horse Archer, Crossbowman, Slinger, Fire Thrower, Bedouin Skirmisher, Bedouin Heavy Camel, Ballista, Arabian Ballista. The Catapult, Trebuchet and Mangonel have no engage check in the game, so they do not get the setting.
[/list]

[b]Range notes (AttackRange / EngageRange):[/b]
[list]
[*]They apply to the listed ranged unit types only; melee units do not get the settings.
[*]Each applied range is logged, e.g. [b][AttackRange] CHIMP_TYPE_ARCHER: 80 tiles (game default 54); Script Extender reports 80.[/b] and [b][EngageRange] CHIMP_TYPE_ARCHER: idle units notice enemies within 80 tiles (game 50; from EngageRange 80)[/b].
[*]How it works (2.9.1): AttackRange goes through the Script Extender. EngageRange is written into the game's own distance checks for that unit type, because the Script Extender's engage-range function missed the check idle units use (they kept reacting at 50 tiles) and crashed the game for the Crossbowman and Bedouin Heavy Camel. The game's small gap between engaging and giving up is kept.
[*]Special projectile modes and some unusual unit behaviour can ignore AttackRange in part (Script Extender limitation).
[*]Not exposed on purpose: the Script Extender's "interact range" (it only changes the player's click/UI check, not what units do, and large values overflow inside the Script Extender).
[*]Like every other stat, they follow the host in multiplayer (host config sync) and are put back to your own values, or the game default, when you leave.
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

[b]Stockpile[/b] ([b][STRUCT_GOODS_YARD][/b], new): free in the game. Its section has only the five costs ([b]GoldCost[/b], [b]WoodCost[/b], [b]StoneCost[/b], [b]IronCost[/b], [b]PitchCost[/b], each [b]-1 # Default: 0[/b]); no Health, housing or MaxCount. With a cost set, placing a Stockpile costs it like any other building, and the build menu shows the cost. Measured in game: with GoldCost = 20 and WoodCost = 5, placing a Stockpile took exactly 20 gold and 5 wood.

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
[*][b]BallistaDamage.csv[/b] — Ballista projectile damage. The game has no per-unit value here: the [b]Default[/b] row applies to every unit not listed (all soldiers and engineers), and the other rows are siege engines and portable shields.
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


[size=5][b]Limits in the lobby (MaxCount)[/b][/size]
[b]New in 2.10.0.[/b] The [b]Crusader DE Tweaker[/b] tab of the [b]Mod Options[/b] window (the Script Extender's Mod Options button on the skirmish and multiplayer lobby screens) lists every recruitable unit and siege engine and every building, each with a box:
[list]
[*][b]empty[/b] = use the MaxCount of the Units / Structures file
[*][b]-1[/b] = no limit in this lobby, even if the file has one
[*][b]0[/b] = not allowed
[*][b]a number[/b] = at most that many at once (units alive, buildings placed)
[/list]
[list]
[*]A value applies as soon as you leave the box, and works exactly like the file's MaxCount: recruiting over the limit is refused, building placement over the limit is blocked. Only your own (human) player is limited, as with the file values.
[*]In multiplayer only the host can type; players see the host's values (greyed out) and every player is limited by them, whether "Players who join use my configs" is on or off. When you leave the lobby your own values return.
[*]Your values are kept for the next game (the Script Extender stores them in [b]BepInEx\plugins\CrusaderDETweaker\LobbyModSettings\[/b]); the config files are not changed. Clear a box to go back to the file's value.
[*]The log shows [b][LobbyMaxCounts] Lobby MaxCount values (you): CHIMP_TYPE_KNIGHT=10, ...[/b] whenever they change.
[/list]


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
