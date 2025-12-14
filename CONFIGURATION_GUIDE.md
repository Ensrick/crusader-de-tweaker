[center][size=6][b]CrusaderDETweaker - Configuration Guide[/b][/size][/center]


[size=5][b]Config Files Location[/b][/size]
All files are in [b]BepInEx/config/[/b]


[size=5][b]1. ensrick.crusaderdetweaker.cfg[/b][/size]
[b]Real-time global multipliers[/b] - affect all units/structures instantly

[code]
[Multipliers]
# Unit Multipliers
UnitHealthMultiplier = 1.0                    # Global multiplier for unit max health
UnitMeleeDamageTakenMultiplier = 1.0          # Global multiplier for all melee damage taken by units (minimum damage is always 1)
UnitRangedDamageTakenMultiplier = 1.0          # Global multiplier for all ranged damage taken by units (affects Arrow, Bolt, Slinger, Javelin; minimum damage is always 1)

# Structure Damage Multipliers
StructureDamageTakenMultiplier = 1.0          # Global multiplier for damage taken by all structures
WallDamageTakenMultiplier = 1.0               # Multiplier for damage taken by walls (stone, crenel walls)
TowerDamageTakenMultiplier = 1.0               # Multiplier for damage taken by towers (tower levels 1-5)
CivilStructureDamageTakenMultiplier = 1.0     # Multiplier for damage taken by civilian structures (non-towers, non-gatehouses)

# Wall Cost Multipliers
LowWallCostMultiplier = 0.25                  # Cost multiplier for low/short walls (stone walls at low height, stairs). Game default: 0.25
HighWallCostMultiplier = 0.5                  # Cost multiplier for high walls (stone walls at high height, crenel walls). Game default: 0.5
[/code]

[b]Note:[/b] Multipliers stack! For example, if a wall has `WallDamageTakenMultiplier = 0.5` and `StructureDamageTakenMultiplier = 2.0`, the final multiplier is `0.5 × 2.0 = 1.0`.


[size=5][b]2. CrusaderDETweaker_Units.toml[/b][/size]
[b]Per-unit stats[/b] - modify individual unit properties

[code]
[CHIMP_TYPE_KNIGHT]
Health = 20000              # Default: 20000
Speed = 1                   # Default: 1
BaseMeleeDamage = 50        # Default: 50
ArmorValue = 3              # Default: 3
GoldCost = 40               # Default: 40
Tags = ["Armor_Heavy", "Weapon_Sword", "Ranged_None"]
# Default: ["Armor_Heavy", "Weapon_Sword", "Ranged_None"]
Resource1Type = "STORED_SWORDS"
Resource1Amount = 1
Resource2Type = "STORED_METAL_ARMOUR"
Resource2Amount = 1
Resource4Type = "_SE_REQUIRE_HORSE"
Resource4Amount = 1

[CHIMP_TYPE_ARCHER]
Health = 2500               # Default: 2500
Speed = 1                  # Default: 1
BaseMeleeDamage = 10       # Default: 10
ArmorValue = 1             # Default: 1
GoldCost = 12              # Default: 12
Tags = ["Armor_Light", "Weapon_Unarmed", "Ranged_Bow"]
# Default: ["Armor_Light", "Weapon_Unarmed", "Ranged_Bow"]
Resource1Type = "STORED_BOWS"
Resource1Amount = 1
[/code]

[b]Available Properties:[/b] Health, Speed, BaseMeleeDamage, ArmorValue, GoldCost, Tags, Resource1-4Type, Resource1-4Amount

[b]Note:[/b] Not all units have GoldCost (only recruitable units). Tags are arrays of strings that define armor type, weapon type, and ranged weapon type.


[size=5][b]3. CrusaderDETweaker_Structures.toml[/b][/size]
[b]Per-structure stats[/b] - modify building properties

[code]
[STRUCT_BARRACKS]
Health = 400                # Default: 400
GoldCost = 15               # Default: 15
WoodCost = 0                # Default: 0
StoneCost = 0              # Default: 0
IronCost = 0                # Default: 0
PitchCost = 0               # Default: 0
HousingPopulationSpace = 0  # Default: 0

[STRUCT_HOVEL]
Health = 100                # Default: 100
GoldCost = 0                # Default: 0
WoodCost = 6                # Default: 6
StoneCost = 0               # Default: 0
IronCost = 0                # Default: 0
PitchCost = 0               # Default: 0
HousingPopulationSpace = 8  # Default: 8 (population capacity)

[STRUCT_ARMOURY]
Health = 300                # Default: 300
GoldCost = 100              # Default: 100
WoodCost = 0                # Default: 0
StoneCost = 0               # Default: 0
IronCost = 5                # Default: 5
PitchCost = 0               # Default: 0
HousingPopulationSpace = 0  # Default: 0
[/code]

[b]Available Properties:[/b] Health, GoldCost, WoodCost, StoneCost, IronCost, PitchCost, HousingPopulationSpace

[b]Note:[/b] Walls are not included in this file - their costs are controlled by `LowWallCostMultiplier` and `HighWallCostMultiplier` in the BepInEx config file.


[size=5][b]4. Damage Matrix CSV Files[/b][/size]
[b]Surgical damage overrides[/b] - only edit specific matchups you want to change. These are difficult to read unless you use a spreadsheet editor like Excel or Google Sheets. You'll import them and export them as CSV files.

[b]CrusaderDETweaker_MeleeDamage.csv[/b] - Unit vs Unit melee damage
[code]
,CHIMP_TYPE_KNIGHT,CHIMP_TYPE_SWORDSMAN,CHIMP_TYPE_ARCHER
CHIMP_TYPE_KNIGHT,50,80,80
CHIMP_TYPE_SWORDSMAN,50,100,100
CHIMP_TYPE_ARCHER,25,25,25
[/code]
[i]Row = Defender, Column = Attacker. Change specific values to override damage.[/i]

[b]CrusaderDETweaker_RangedDamage.csv[/b] - Projectile damage by unit
[code]
,Arrow,Bolt,Slinger,Javelin
CHIMP_TYPE_KNIGHT,150,2500,150,200
CHIMP_TYPE_ARCHER,2500,2500,2500,2500
CHIMP_TYPE_PEASANT,2500,2500,2500,2500
[/code]
[i]Change values to modify how much damage each unit takes from projectiles. Base projectile damage is 2500.[/i]

[b]CrusaderDETweaker_EunuchAoeDamage.csv[/b] - Eunuch AOE damage matrix
[i]Controls area-of-effect damage for eunuch units.[/i]

[b]Note:[/b] CSV files only apply values that [b]differ from current game state[/b]. Unchanged values won't override TOML configs.


[size=5][b]Load Order[/b][/size]
[code]
Game Defaults → TOML Configs → CSV Overrides → Real-time Multipliers
[/code]

[b]Example:[/b]
[list=1]
[*]Set Knight health to 25000 in TOML
[*]Set UnitHealthMultiplier = 2.0 in CFG
[*]Result: Knight has 50000 health (25000 × 2.0)
[/list]

[b]Structure Damage Example:[/b]
[list=1]
[*]Set WallDamageTakenMultiplier = 0.5 in CFG (walls take half damage)
[*]Set StructureDamageTakenMultiplier = 2.0 in CFG (all structures take double damage)
[*]Result: Walls take 1.0× damage (0.5 × 2.0), other structures take 2.0× damage
[/list]


[size=5][b]Quick Tips[/b][/size]
[list]
[*][b]Requires game restart[/b] for TOML/CSV changes
[*][b]Real-time multipliers[/b] (CFG file) work during gameplay - no restart needed
[*][b]Delete configs to reset[/b] - they regenerate with vanilla values
[*][b]Check BepInEx/LogOutput.log[/b] for errors
[*][b]Minimum damage is always 1[/b] - setting multipliers to 0 will result in 1 damage (game uses default values if damage is 0)
[*][b]Wall costs[/b] are controlled by multipliers in CFG, not TOML (walls are treated as tiles, not buildings)
[/list]

