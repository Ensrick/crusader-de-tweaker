1.0.0 - Initial Release
1.1.0 - Added structures. Updated to include damage multipliers per unit, and for global melee damage. Added unit and structure costs.
1.1.1 - Bugfix for structure cost.
1.2.0 - Added additional BepInEx multipliers:
       - WallDamageTakenMultiplier: Multiplier for damage to walls
       - TowerDamageTakenMultiplier: Multiplier for damage to towers
       - WoodenStructureDamageTakenMultiplier: Multiplier for damage to wooden structures
       - Improved error handling and validation for all multipliers
       - Added structure type helper methods (IsWall, IsTower, IsWoodenStructure)
1.2.1 - Added wall cost modification support:
       - Walls (stone, crenel, wood) can now have their costs modified via TOML
       - Walls are cost-only structures (health and other properties cannot be modified)
       - Added CostOnlyStructures list to handle structures with limited modifiable properties