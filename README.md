# Crusader DE Tweaker

Crusader DE Tweaker is a comprehensive balance mod for Stronghold Crusader: Definitive Edition that allows you to customize unit and structure properties through configuration files. It uses **BepInEx 5** alongside **Stronghold Crusader DE Script Extender (SHCDE-SE)** as an API.

**For Developers**: See [DEVELOPER_NOTES.md](DEVELOPER_NOTES.md) for critical information about plugin GUID, build paths, and configuration logic that MUST be followed.

## What is This?

Crusader DE Tweaker provides fine-grained control over game balance through multiple configuration methods:

- **Per-Unit Customization**: Modify health, speed, and costs for individual units
- **Structure Tweaking**: Adjust building health, costs, and housing capacity
- **Damage System**: Set damage and healing per attacker/defender matchup in seven CSV matrices (melee, ranged, Eunuch AOE, ballista, unit fire, Bedouin heal, building fire)
- **Real-Time Multipliers**: Apply global multipliers without restarting the game
- **Unit & Building Count Caps**: Limit how many of each unit or building type a player can have (e.g., max 20 Knights, 3 Hovels)
- **Gameplay Globals**: Siege, stealth, gates, pathfinding, peasant spawning, trade prices and auto-trade

### Use Cases

- Balance mods for multiplayer
- Single-player difficulty adjustments
- Testing different unit configurations
- Creating custom scenarios
- Research and analysis of game mechanics

## Quick Start

1. **Install Prerequisites:**
   - Install **BepInEx 5** by following the instructions on its [official page](https://docs.bepinex.dev/).  
   - Install **SHCDE-SE** by following the instructions provided by the creator, Rawra. [official page](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender).  
   - Both are installed via simple drag-and-drop into the game directory.

2. **Install the Mod:**
   - Download or clone this repository.
   - Place the mod files into your game directory alongside BepInEx and SHCDE-SE.

3. **Configure:**
   - Config files live in the game directory, not `%APPDATA%`: `{GameDir}\BepInEx\config\CrusaderDETweaker\`
   - Edit `CrusaderDETweaker_Units.toml` to customize units (generated on first run if missing). Each numeric stat defaults to `-1` = "use the game default"; set a real number to override.
   - Edit `CrusaderDETweaker_GlobalMultipliers.cfg` for real-time multipliers and unit caps (no restart needed)
   - Edit `CrusaderDETweaker_GameplaySettings.toml` for gameplay globals (siege, stealth, peasant spawning, trade)
   - Restart the game to apply TOML changes
   - **Note:** Your config files are safe - the mod never overwrites your changes. Files are only generated if they don't exist.

See [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) for detailed instructions.

## Configuration Methods

This mod supports multiple configuration methods, each suited for different use cases:

### 1. TOML Files (Recommended - Primary Method)

The primary configuration method for most users:

- **`CrusaderDETweaker_Units.toml`** - Unit properties (health, speed, costs, max count caps)
- **`CrusaderDETweaker_Structures.toml`** - Building properties (health, costs, housing, max count caps)

**When to use:** Most common use case. Edit these files to customize individual units and structures.

### 2. CSV Damage Matrices (the damage system)

All combat damage and healing is configured here — the CSVs are the active, and only, place to set
per-matchup values. They live in `{GameDir}\BepInEx\config\CrusaderDETweaker\DamageMatrices\`:

- **`CrusaderDETweaker_MeleeDamage.csv`** - Melee damage, unit vs unit
- **`CrusaderDETweaker_RangedDamage.csv`** - Projectile damage per unit (Arrow, Bolt, Slinger, Javelin)
- **`CrusaderDETweaker_EunuchAoeDamage.csv`** - Eunuch area-of-effect damage per unit
- **`CrusaderDETweaker_BallistaDamage.csv`** - Ballista damage per unit
- **`CrusaderDETweaker_UnitFireDamage.csv`** - Fire damage per unit
- **`CrusaderDETweaker_BedouinHeal.csv`** - Bedouin healer amount per unit
- **`CrusaderDETweaker_BuildingFireDamage.csv`** - Fire damage per building type

**Format:** rows (first column) = **defenders**, columns (header row) = **attackers**. Every value of
0 or more is applied to the game as-is; a negative value (`-1`) leaves the game's own value unchanged.
Each generated file carries these instructions in its header comments.

**When to use:** any damage change. The global multipliers below then scale on top of whatever the
matrices set. Files are generated on first launch and never overwritten, so your edits are safe.

### 3. BepInEx Config (Real-Time Multipliers)

Global multipliers applied during gameplay (no restart needed):

- **`UnitMeleeDamageTakenMultiplier`**: Global multiplier for all melee damage to units
- **`UnitRangedDamageTakenMultiplier`**: Global multiplier for all ranged damage to units (Arrow, Bolt, Slinger, Javelin)
- **`UnitHealthMultiplier`**: Global multiplier for unit max health
- **`StructureDamageTakenMultiplier`**: Global multiplier for all damage to structures
- **`WallDamageTakenMultiplier`**: Multiplier for damage to walls
- **`TowerDamageTakenMultiplier`**: Multiplier for damage to towers
- **`CivilStructureDamageTakenMultiplier`**: Multiplier for damage to civilian structures
- **`LowWallCostMultiplier`**: Cost multiplier for low/short walls (default: 0.25)
- **`HighWallCostMultiplier`**: Cost multiplier for high walls and crenel walls (default: 0.5)
- **`DemolisherBuildingDamageMultiplier`** / **`SapperBuildingDamageMultiplier`**: Damage these Bedouin units deal to structures
- **`UnitFireDamageTakenMultiplier`** / **`StructureFireDamageTakenMultiplier`**: Fire damage taken
- **`BedouinHealMultiplier`**: Healing from Bedouin healers
- **`DiseaseDamageMultiplier`**: Scales all three disease damage tiers

**Debugging:**
- **`DebugLogging`** (`[Debug]` section, default: `false`): Enable step-by-step logging for all event hooks. Applies in real-time. Warning: very high log volume during gameplay — only enable when diagnosing hook issues.

**When to use:** For quick balance adjustments during gameplay. Changes apply immediately without restarting.

### Load Order

Configurations are applied in this order:
```
Game Defaults → TOML Configs → CSV Damage Matrices → Real-Time Multipliers
```

TOML and CSV are applied at launch (restart required); the CSV matrices apply after the TOML, so a CSV
value wins for the matchups it covers. The real-time multipliers scale whatever the earlier tiers set.

See [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) for detailed documentation.

## Contributing

Contributions are welcome!  
- Report issues via GitLab Issues.  
- For code contributions, submit a Merge Request with clear descriptions of your changes.  
- Include your GitLab username when contributing for proper credit.

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed contribution guidelines, architecture overview, and development workflow.

## Authors and Acknowledgments
- **Ensrick** – Creator of this mod.  
- **Rawra** – Created the Stronghold Crusader DE Script Extender API used by this mod.

## License
This project is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

## Project Status
Active development. New updates and features are planned for future releases.
