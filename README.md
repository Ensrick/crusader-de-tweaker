# Crusader DE Tweaker

Crusader DE Tweaker is a comprehensive balance mod for Stronghold Crusader: Definitive Edition that allows you to customize unit and structure properties through configuration files. It uses **BepInEx 5** alongside **Stronghold Crusader DE Script Extender (SHCDE-SE)** as an API.

**For Developers**: See [DEVELOPER_NOTES.md](DEVELOPER_NOTES.md) for critical information about plugin GUID, build paths, and configuration logic that MUST be followed.

## What is This?

Crusader DE Tweaker provides fine-grained control over game balance through multiple configuration methods:

- **Per-Unit Customization**: Modify health, speed, and costs for individual units
- **Structure Tweaking**: Adjust building health, costs, and housing capacity
- **Damage System**: Configure damage values via CSV matrix files
- **Real-Time Multipliers**: Apply global multipliers without restarting the game
- **CSV Damage Matrices**: Surgical damage adjustments via CSV files

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
   - Edit `CrusaderDETweaker_Units.toml` to customize units (generated on first run if missing)
   - Edit `CrusaderDETweaker_GlobalMultipliers.cfg` for real-time multipliers (no restart needed)
   - Restart the game to apply TOML changes
   - **Note:** Your config files are safe - the mod never overwrites your changes. Files are only generated if they don't exist.

See [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) for detailed instructions.

## Configuration Methods

This mod supports multiple configuration methods, each suited for different use cases:

### 1. TOML Files (Recommended - Primary Method)

The primary configuration method for most users:

- **`CrusaderDETweaker_Units.toml`** - Unit properties (health, speed, costs, tags)
- **`CrusaderDETweaker_Structures.toml`** - Building properties (health, costs, housing)

**When to use:** Most common use case. Edit these files to customize individual units and structures.

### 2. CSV Damage Matrices (Legacy - Reference Only)

Reference files for the original game damage values:

- **`CrusaderDETweaker_MeleeDamage.csv`** - Melee damage matrix (unit vs unit)
- **`CrusaderDETweaker_RangedDamage.csv`** - Ranged damage matrix (projectile types)
- **`CrusaderDETweaker_EunuchAoeDamage.csv`** - Area-of-effect damage

**When to use:** Reference files for original game damage values. Kept for reference only.

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

**When to use:** For quick balance adjustments during gameplay. Changes apply immediately without restarting.

### Load Order

Configurations are applied in this order:
```
Game Defaults → TOML Configs → CSV Overrides → Real-Time Multipliers
```

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
