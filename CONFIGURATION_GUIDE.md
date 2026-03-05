[center][size=6][b]CrusaderDETweaker - Configuration Guide[/b][/size][/center]


[size=5][b]Config Files Location[/b][/size]

All config files are in the [b]game directory[/b] (not %APPDATA%):
[b]{GameDir}\BepInEx\config\CrusaderDETweaker\[/b]

Example: [b]C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker\[/b]

Files:
[list]
[*]CrusaderDETweaker_GlobalMultipliers.cfg - Real-time global multipliers
[*]CrusaderDETweaker_GameplaySettings.toml - Gameplay flags, siege, stealth, trade prices, auto-trade
[*]CrusaderDETweaker_Units.toml - Per-unit stats (health, speed, cost, weapon/armor requirements)
[*]CrusaderDETweaker_Structures.toml - Per-structure stats (health, cost, housing)
[*]DamageMatrices\CrusaderDETweaker_MeleeDamage.csv - Unit vs unit melee damage
[*]DamageMatrices\CrusaderDETweaker_RangedDamage.csv - Projectile (arrow/bolt/slinger/javelin) damage per unit
[*]DamageMatrices\CrusaderDETweaker_EunuchAoeDamage.csv - Eunuch AOE damage per unit
[*]DamageMatrices\CrusaderDETweaker_BallistaDamage.csv - Ballista damage per unit
[*]DamageMatrices\CrusaderDETweaker_UnitFireDamage.csv - Fire damage per unit
[*]DamageMatrices\CrusaderDETweaker_BedouinHeal.csv - Bedouin healer amount per unit
[*]DamageMatrices\CrusaderDETweaker_BuildingFireDamage.csv - Fire damage per building type
[/list]

[b]Note:[/b] Config files are only generated if they don't exist. Your changes are never overwritten. Delete a file to reset it to defaults.


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
# Override map restrictions — only takes effect when set to true
AllBuildingsAvailable = false
AllUnitsAllowed = false
AllTradeGoodsAllowed = false
AllProductionGoodsAllowed = false
[/code]

[b]Trade Prices[/b]

Override the market buy/sell price for individual goods. [b]0 = use the game's default price[/b] (no override). Requires a Trade Post. Re-applied on each map load.
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
[b]Per-unit stats[/b] — one section per unit type. Values are read from the game on first launch and written as defaults.

[code]
[CHIMP_TYPE_KNIGHT]
Health = 20000                    # Max health
Speed = 1                         # Movement speed
GoldCost = 40                     # Recruitment gold cost (Crusader units only)
WeaponType = "STORED_SWORDS"      # Required weapon resource
ArmorType = "STORED_METAL_ARMOUR" # Required armor resource
RequiresHorse = true              # Links unit to a stable slot on spawn

[CHIMP_TYPE_ARCHER]
Health = 2500
Speed = 1
GoldCost = 12
WeaponType = "STORED_BOWS"
[/code]

[b]Available Properties:[/b]
[list]
[*][b]Health[/b] — Unit max health
[*][b]Speed[/b] — Movement speed (some special units cannot be modified)
[*][b]GoldCost[/b] — Recruitment cost (Crusader recruitable units only)
[*][b]WeaponType[/b] — Weapon resource: STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS, STORED_PIKES, STORED_MACES, STORED_SPEARS
[*][b]ArmorType[/b] — Armor resource: STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR
[*][b]RequiresHorse[/b] — Cavalry units only. When true, hooks unit spawn to link it to a stable slot.
[/list]

[b]Note:[/b] Damage values are not set here. All combat damage is controlled by the CSV matrices below.


[size=5][b]4. CrusaderDETweaker_Structures.toml[/b][/size]
[b]Per-structure stats[/b] — one section per building type.

[code]
[STRUCT_BARRACKS]
Health = 400
GoldCost = 15
WoodCost = 0
StoneCost = 0
IronCost = 0
PitchCost = 0
HousingPopulationSpace = 0

[STRUCT_HOVEL]
Health = 100
GoldCost = 0
WoodCost = 6
StoneCost = 0
IronCost = 0
PitchCost = 0
HousingPopulationSpace = 8
[/code]

[b]Available Properties:[/b] Health, GoldCost, WoodCost, StoneCost, IronCost, PitchCost, HousingPopulationSpace

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


[size=5][b]Quick Tips[/b][/size]
[list]
[*][b]TOML and CSV changes require a game restart[/b]
[*][b]Most CFG multipliers apply in real-time[/b] — no restart needed (fire/heal multipliers are the exception)
[*][b]Delete a config file to reset it[/b] — it will be regenerated with defaults on next launch
[*][b]Multipliers stack[/b] — UnitMeleeDamageTakenMultiplier and StructureDamageTakenMultiplier both apply independently
[*][b]Minimum damage is always 1[/b] — setting multipliers to 0 still results in 1 damage
[*][b]Wall costs[/b] use multipliers in the CFG, not values in the TOML
[*][b]Auto Trade[/b] requires a Trade Post and is re-applied on each map load
[*][b]Check BepInEx\LogOutput.log[/b] in the game directory for errors
[/list]
