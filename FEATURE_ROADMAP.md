# Feature Roadmap

This document organizes and elaborates on planned features for Crusader DE Tweaker. Features are organized by priority and implementation complexity.

## Overview

This roadmap includes:
1. User-requested features
2. Implementation details and considerations
3. Dependencies and prerequisites
4. Estimated effort and priority

## Feature List

### 1. Custom Tag System for Damage/Armor Modifiers

**Status**: 🔴 **Not Started**  
**Priority**: **HIGH**  
**Effort**: **MEDIUM-HIGH**

#### Description
Allow users to create custom tags that modify how armor and damage work. Tags should be configurable via TOML, allowing users to define:
- Custom weapon categories
- Custom armor categories
- Custom tag vs tag multipliers
- Custom armor values for tags

#### Current State
- Tags are hardcoded in `DamageCalculator.cs`
- Tag definitions are in `UnitDamageData.cs` comments
- Tag vs tag multipliers are in `TagVsTagModifiers` dictionary

#### Implementation Plan

**Phase 1: Tag Definition System**
- [ ] Create `TagDefinition` class to hold tag properties
  ```csharp
  class TagDefinition {
      string Name;
      TagType Type; // Weapon, Armor, Modifier, etc.
      float ArmorValue; // For armor tags
      Dictionary<string, float> Multipliers; // Tag vs tag multipliers
  }
  ```
- [ ] Create `TagConfigSystem` implementing `IConfigSystem`
- [ ] Add `CrusaderDETweaker_Tags.toml` config file
- [ ] Load tag definitions from TOML on startup

**Phase 2: Dynamic Tag System**
- [ ] Replace hardcoded `TagVsTagModifiers` with dynamic lookup
- [ ] Update `DamageCalculator` to use dynamic tag system
- [ ] Add validation for tag definitions
- [ ] Support tag inheritance/composition

**Phase 3: User Interface**
- [ ] Generate default tag config file with examples
- [ ] Add documentation for tag system
- [ ] Add validation errors for invalid tag combinations

#### TOML Structure Example
```toml
[Tags.CustomWeapon_Flail]
Type = "Weapon"
Description = "Custom flail weapon type"

[Tags.CustomArmor_Reinforced]
Type = "Armor"
ArmorValue = 0.3  # Takes 30% damage (very heavy armor)
Description = "Reinforced armor"

[Tags.CustomModifier_Vampire]
Type = "Modifier"
Multipliers = { "Undead" = 2.0 }  # 2x damage vs undead
Description = "Vampire modifier"
```

#### Dependencies
- ✅ Refactored `DamageCalculator` (COMPLETED in Version 1.3.0 - now uses Strategy pattern)
- Tag system should be loaded before unit configs

#### Considerations
- Need to ensure backward compatibility with existing tags
- Tag names should be validated (no conflicts with existing tags)
- Performance: Dynamic lookups may be slower than hardcoded (consider caching)

#### Estimated Effort
- **Phase 1**: 4-6 hours
- **Phase 2**: 6-8 hours
- **Phase 3**: 2-3 hours
- **Total**: 12-17 hours

---

### 2. Additional BepInEx Multipliers

**Status**: 🔴 **Not Started**  
**Priority**: **HIGH**  
**Effort**: **LOW**

#### Description
Add more real-time multiplier hooks to BepInEx config for fine-grained control:
- Wall damage multiplier
- Tower damage multiplier
- Wooden structure damage multiplier
- (Future: Unit speed multiplier, unit cost multiplier, etc.)

#### Current State
- Only 3 multipliers exist:
  - `UnitMeleeDamageTakenMultiplier`
  - `StructureDamageTakenMultiplier`
  - `UnitHealthMultiplier`

#### Implementation Plan

**Step 1: Identify Structure Types**
- [ ] Create helper methods to identify structure types:
  - `IsWall(eStructs structure)` - Check if structure is a wall
  - `IsTower(eStructs structure)` - Check if structure is a tower
  - `IsWoodenStructure(eStructs structure)` - Check if structure is wooden
- [ ] Use `Data.StructureCategories.NonModable` to filter

**Step 2: Add Config Entries**
- [ ] Add to `ConfigManagerBepinex.Initialize()`:
  ```csharp
  WallDamageTakenMultiplier = config.Bind(
      "Multipliers",
      "WallDamageTakenMultiplier",
      1.0f,
      "Multiplier for damage taken by walls"
  );
  
  TowerDamageTakenMultiplier = config.Bind(
      "Multipliers",
      "TowerDamageTakenMultiplier",
      1.0f,
      "Multiplier for damage taken by towers"
  );
  
  WoodenStructureDamageTakenMultiplier = config.Bind(
      "Multipliers",
      "WoodenStructureDamageTakenMultiplier",
      1.0f,
      "Multiplier for damage taken by wooden structures"
  );
  ```

**Step 3: Add Event Hooks**
- [ ] Update `ApplyAllMultiplierConfigs()`:
  ```csharp
  BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
      .Where(args => args.Phase == EventHookPhase.Pre)
      .Subscribe(args =>
      {
          eStructs structureType = GetStructureType(args.BuildingId);
          
          // Apply wall multiplier
          if (IsWall(structureType))
          {
              args.Damage = (int)(args.Damage * WallDamageTakenMultiplier.Value);
          }
          
          // Apply tower multiplier (towers are not walls)
          if (IsTower(structureType))
          {
              args.Damage = (int)(args.Damage * TowerDamageTakenMultiplier.Value);
          }
          
          // Apply wooden structure multiplier
          if (IsWoodenStructure(structureType))
          {
              args.Damage = (int)(args.Damage * WoodenStructureDamageTakenMultiplier.Value);
          }
          
          // Apply general structure multiplier (existing)
          args.Damage = (int)(args.Damage * StructureDamageTakenMultiplier.Value);
      });
  ```

**Step 4: Structure Type Detection**
- [ ] Implement `GetStructureType(int buildingId)`:
  ```csharp
  private static eStructs GetStructureType(int buildingId)
  {
      // Use BuildingApi to get structure type
      return Plugin.BuildingApi.GetType(buildingId);
  }
  ```
- [ ] Implement structure type checks:
  ```csharp
  private static bool IsWall(eStructs structure)
  {
      return structure == eStructs.STRUCT_STONE_WALL ||
             structure == eStructs.STRUCT_CRENAL_WALL ||
             structure == eStructs.STRUCT_WOOD_WALL;
  }
  
  private static bool IsTower(eStructs structure)
  {
      return structure == eStructs.STRUCT_TOWER1 ||
             structure == eStructs.STRUCT_TOWER2 ||
             structure == eStructs.STRUCT_TOWER3 ||
             structure == eStructs.STRUCT_TOWER4;
  }
  
  private static bool IsWoodenStructure(eStructs structure)
  {
      // Check if structure uses wood as primary material
      // This may require checking costs or structure properties
      var cost = Plugin.BuildingApi.GetDefaultCost(structure);
      return cost.Wood > 0 && cost.Stone == 0;
  }
  ```

#### Dependencies
- SHCDE-SE API for structure type detection
- Understanding of structure enum values

#### Considerations
- Multipliers should stack (wall multiplier × general structure multiplier)
- Need to handle edge cases (structures that are both walls and towers? Probably not)
- Should multipliers be applied in a specific order? (Specific → General)

#### Estimated Effort
- **Total**: 3-4 hours

---

### 3. Wall Cost Modifier

**Status**: 🔴 **Not Started**  
**Priority**: **HIGH**  
**Effort**: **LOW-MEDIUM**

#### Description
Add support for modifying wall costs. Currently, walls are in `NonModableStructures` because their stats are handled differently. The API now supports wall cost modification, so we should:
- Remove walls from `NonModableStructures` (or handle them separately)
- Add wall cost properties to TOML config
- Ensure only cost is modifiable (not health, which may be handled differently)

#### Current State
- Walls (`STRUCT_STONE_WALL`, `STRUCT_CRENAL_WALL`) are in `NonModableStructures`
- Comment says "Wall stats are somewhere else"
- Structure cost system exists and works for other structures

#### Implementation Plan

**Step 1: Research Wall Cost API**
- [ ] Verify SHCDE-SE API supports wall cost modification
- [ ] Test wall cost modification manually
- [ ] Document any limitations or special cases

**Step 2: Update NonModableStructures**
- [ ] Option A: Remove walls from `NonModableStructures` entirely
- [ ] Option B: Create separate list `NonModableStructureProperties` that excludes cost
- [ ] Option B is safer - allows cost modification but prevents health modification

**Step 3: Add Wall Cost Handling**
- [ ] Update `StructureConfigSystem` to handle walls specially
- [ ] Only allow cost properties for walls (skip health)
- [ ] Add validation to prevent health modification for walls

**Step 4: Update TOML Generator**
- [ ] Include walls in structure TOML generation
- [ ] Only generate cost properties for walls
- [ ] Add comment explaining wall limitations

**Step 5: Test**
- [ ] Test wall cost modification in-game
- [ ] Verify walls still function correctly
- [ ] Test edge cases (zero cost, negative cost?)

#### Implementation Details

**Option A: Remove from NonModableStructures**
```csharp
// In Data/StructureCategories.cs
internal static readonly eStructs[] NonModable = {
    // ... other structures ...
    // STRUCT_STONE_WALL removed - now modifiable
    // STRUCT_CRENAL_WALL removed - now modifiable
};
```

**Option B: Separate Lists (Recommended)**
```csharp
// Structures that cannot be modified at all
internal static readonly eStructs[] NonModableStructures = {
    // ... existing list ...
};

// Structures that can only have cost modified (not health)
internal static readonly eStructs[] CostOnlyStructures = {
    eStructs.STRUCT_STONE_WALL,
    eStructs.STRUCT_CRENAL_WALL,
    eStructs.STRUCT_WOOD_WALL,  // If exists
};
```

Then in `StructureConfigSystem`:
```csharp
if (CostOnlyStructures.Contains(structure))
{
    // Only allow cost properties
    // Skip health property
}
```

#### Dependencies
- SHCDE-SE API confirmation for wall cost support
- Understanding of wall structure handling

#### Considerations
- Walls may have special health handling (per-tile health?)
- Cost modification should be safe, but test thoroughly
- May need to handle `STRUCT_WOOD_WALL` if it exists

#### Estimated Effort
- **Total**: 2-4 hours (depending on API complexity)

---

### 4. Structure Damage Analysis and Multipliers

**Status**: 🔴 **Not Started**  
**Priority**: **MEDIUM**  
**Effort**: **MEDIUM**

#### Description
Investigate how different units deal damage to structures and implement structure-specific damage multipliers. This will help users balance structure durability.

#### Current State
- `StructureDamageTakenMultiplier` exists but applies to all structures
- No understanding of how unit damage to structures works
- No structure-specific damage calculations

#### Implementation Plan

**Phase 1: Research and Analysis**
- [ ] Investigate how units deal damage to structures
  - Check if melee damage applies to structures
  - Check if ranged damage applies to structures
  - Check if there are special multipliers for structures
- [ ] Test different unit types vs different structures
- [ ] Document findings

**Phase 2: Structure Damage System**
- [ ] Create `StructureDamageCalculator` similar to `DamageCalculator`
- [ ] Map unit types to structure damage multipliers
- [ ] Add structure-specific damage rules (e.g., siege weapons vs walls)

**Phase 3: TOML Configuration**
- [ ] Add structure damage multipliers to TOML
- [ ] Allow per-unit-type multipliers for structures
- [ ] Example:
  ```toml
  [StructureDamage.SWORDSMAN]
  vs_Walls = 1.0
  vs_Towers = 0.8
  vs_Wooden = 1.5
  ```

**Phase 4: Integration**
- [ ] Integrate structure damage system with existing multipliers
- [ ] Apply structure damage multipliers in event hooks
- [ ] Test and validate

#### Research Questions
1. Do units use their melee damage vs structures?
2. Are there special multipliers for structures?
3. Do siege weapons have different damage vs structures?
4. How does wall damage work (per-tile or per-structure)?

#### Dependencies
- Understanding of game's structure damage system
- May require reverse engineering or API exploration

#### Considerations
- This is exploratory - may discover limitations
- May need to work with SHCDE-SE maintainer for API additions
- Structure damage may be more complex than unit damage

#### Estimated Effort
- **Phase 1**: 4-6 hours (research)
- **Phase 2**: 6-8 hours (implementation)
- **Phase 3**: 3-4 hours (TOML)
- **Phase 4**: 2-3 hours (integration)
- **Total**: 15-21 hours

---

## Feature Priority Summary

### High Priority (Do First)
1. **Additional BepInEx Multipliers** (3-4 hours) - Quick win, user requested
2. **Wall Cost Modifier** (2-4 hours) - User requested, API available
3. **Custom Tag System** (12-17 hours) - High value, but complex

### Medium Priority (Do When Time Permits)
4. **Structure Damage Analysis** (15-21 hours) - Exploratory, may have limitations

## Implementation Order

**Recommended Order**:
1. Additional BepInEx Multipliers (quick win)
2. Wall Cost Modifier (user requested, straightforward)
3. Custom Tag System (high value, but requires refactoring first)
4. Structure Damage Analysis (exploratory, do after core features)

## Dependencies Between Features

```
Additional BepInEx Multipliers
    ↓ (no dependencies)
Wall Cost Modifier
    ↓ (no dependencies)
Custom Tag System
    ↓ (✅ DamageCalculator refactoring COMPLETED in Version 1.3.0)
Structure Damage Analysis
    ↓ (may inform Custom Tag System)
```

## Notes

- ✅ **Custom Tag System** can now be implemented - `DamageCalculator` refactoring is COMPLETE (Version 1.3.0)
- **Structure Damage Analysis** is exploratory and may reveal that the game handles structure damage differently than expected
- All features should include:
  - TOML configuration files
  - Documentation updates
  - Validation and error handling
  - Testing

## Future Considerations

Potential future features (not in current roadmap):
- Unit speed multipliers
- Unit cost multipliers
- Projectile damage modifiers
- Range modifiers
- Attack speed modifiers
- Unit AI behavior modifiers

These can be added incrementally using the same patterns established by the current multiplier system.

