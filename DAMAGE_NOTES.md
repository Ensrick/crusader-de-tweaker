# Damage System Analysis - Historical Notes

> ## ⚠ HISTORICAL DOCUMENT — pre-rework analysis, generated 2025-12-24
>
> This was a reverse-engineering study of the game's damage tables written **before** the formula and
> tag systems were removed. Read it for domain background only.
>
> **For the current, authoritative format, see [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md).**
>
> Two things in here are known to be wrong or dead:
> 1. **The melee CSV orientation below was recorded inverted.** The real orientation, for every matrix,
>    is **ROWS (first column) = DEFENDERS, COLUMNS (header row) = ATTACKERS** — confirmed by the header
>    comments `CsvHelper` stamps into each generated file and by `MatrixApplicationLoop.ApplyMatrix`,
>    which indexes `matrix[row = defender, col = attacker]`. The structural claims are corrected below;
>    any *number* in the worked examples was attributed under the old inverted reading, so treat which
>    side "deals" and which "receives" as unverified.
> 2. **The formula/armor-multiplier tier and the tag system no longer exist** — that code was deleted,
>    not deactivated. There are no `MeleeArmorMultiplier` / `RangedArmorMultipliers` TOML properties and
>    no tag interaction files. Damage is set per matchup in the CSVs and nowhere else.

---

## CSV Matrix Structure

### Melee Damage CSV (`CrusaderDETweaker_MeleeDamage.csv`)

**Structure**: Rows = Defenders, Columns = Attackers

```
             ,ARAB_ASSASSIN,ARAB_BALLISTA,...,KNIGHT,...
ARAB_ASSASSIN,     80      ,     80      ,...,  80  ,...
KNIGHT       ,     50      ,     10      ,...,  50  ,...
SWORDSMAN    ,     50      ,     10      ,...,  50  ,...
```

- **Row** = What damage this unit RECEIVES from each attacker
- **Column** = What damage this unit DEALS to each defender

(The cell values above are illustrative of the layout, not a verified extract.)

### Ranged Damage CSV (`CrusaderDETweaker_RangedDamage.csv`)

**Structure**: Rows = Defenders, Columns = Projectile Types

```
Defender        ,Arrow,Bolt ,Slinger,Javelin
KNIGHT          , 150 , 2500,  150  ,  200
ENGINEER        ,2000 ,10000, 2000  , 4000
ARAB_SWORDSMAN  ,1500 ,15000, 1500  , 2000
```

- **Row** = What damage this unit RECEIVES from each projectile type
- **Column** = Projectile type (Arrow, Bolt, Slinger, Javelin)

---

## Concrete Example: KNIGHT

> **Caveat:** this worked example was written against the inverted melee orientation. In the real file
> the Knight's **row** is damage the Knight **receives** and the Knight's **column** is damage it
> **deals** — the reverse of the headings below. The damage numbers are reproduced as originally
> recorded and have not been re-verified, so do not use this section to decide which value to edit.
> Open the CSV and follow its own header comments instead.

### Knight as ATTACKER (Melee)

**Knight's Row in Melee CSV** = Damage Knight DEALS when attacking

```
KNIGHT deals:
  150 damage to: LORD
  100 damage to: CAMEL, etc (4 units)
   80 damage to: ARAB_ASSASSIN
   50 damage to: KNIGHT, SWORDSMAN, ARAB_SWORDSMAN, etc (5 units)
   20 damage to: ARAB_HORSEMAN, etc (8 units)
   10 damage to: ARAB_BALLISTA, etc (9 units)
```

**Key Insight**: Knight deals DIFFERENT damage to different defenders based on their armor class.

### Knight as DEFENDER (Melee)

**Knight's Column in Melee CSV** = Damage Knight RECEIVES when attacked

```
Damage RECEIVED by Knight:
  160 damage from: ARAB_SLAVE, BEDOUIN_HEALER (worst case)
  120 damage from: ARAB_SLINGER, BEDOUIN_EUNUCH
   80 damage from: Most attackers (SWORDSMAN, ARCHER, etc)
   50 damage from: KNIGHT, SWORDSMAN (fellow heavy armor)
   30 damage from: TREBUCHET (special case)
```

**Key Insight**: Knight has GOOD melee armor - takes less damage from most attackers.

### Knight as DEFENDER (Ranged)

**Knight's Row in Ranged CSV** = Damage Knight RECEIVES from projectiles

```
Knight RECEIVES:
  Arrow:    150 damage  (multiplier: 150/2000 = 0.075x)
  Bolt:    2500 damage  (multiplier: 2500/10000 = 0.250x)
  Slinger:  150 damage  (multiplier: 150/2000 = 0.075x)
  Javelin:  200 damage  (multiplier: 200/4000 = 0.050x)
```

**Key Insight**: Knight has EXCELLENT ranged armor:
- 0.075x multiplier vs Arrows/Slingers
- 0.050x multiplier vs Javelins (BEST)
- 0.250x multiplier vs Bolts (armor-piercing, but still good)

**Lower multiplier = BETTER armor** (takes LESS damage)

### Knight as ATTACKER (Ranged)

**Knight does NOT appear in ranged attacking** - Knights don't have ranged attacks!

---

## Damage Formulas (REMOVED FROM THE CODE)

**Note**: The formula-based damage system (Armor Multipliers) was deactivated in February 2026 and the
code was **deleted** in the August 2026 overhaul. Damage is controlled exclusively via CSV matrices. The
following is retained only as a record of how the vanilla tables appear to have been built.

### Melee Damage Formula (no longer implemented)

```
Damage_Taken = Base_Weapon_Damage  Defender_Armor_Multiplier
```

- **Base_Weapon_Damage**: Varies by attacker's weapon type (estimated from max damage dealt)
- **Defender_Armor_Multiplier**: Defender's armor class (0.50x = heavy armor, 1.00x = light armor)
- **Fit Quality**: 93.5% perfect matches, avg error 1.33 damage

**Example**:
- Swordsman base damage: ~100
- Knight armor multiplier: ~0.50x
- Damage: 100 � 0.50 = 50  (matches CSV)

### Ranged Damage Formula

```
Damage_Taken = Base_Projectile_Damage � Defender_Armor_Multiplier
```

**Base Projectile Damage** (from Engineer, who has NO armor):
- Arrow: 2000
- Bolt: 10000
- Slinger: 2000
- Javelin: 4000

- **Fit Quality**: 100% perfect matches, avg error 0.00 damage

**Example**:
- Arrow base: 2000
- Knight armor vs Arrow: 0.075x
- Damage: 2000 � 0.075 = 150  (matches CSV)

**Key Discovery**: Engineer's damage values ARE the base projectile damage (multiplier = 1.0)

---

## Armor System Insights

### Armor Multiplier Interpretation

**CRITICAL**: Lower multiplier = BETTER armor

```
Multiplier   Armor Quality   Damage Taken
----------   ------------   ------------
0.05x        Excellent      Takes 5% of base damage
0.10x        Very Good      Takes 10% of base damage
0.50x        Good           Takes 50% of base damage
1.00x        None           Takes 100% of base damage (full damage)
2.00x        Negative       Takes 200% of base damage (weak point)
```

### Armor-Piercing Mechanics

**79 out of 80 units** have DIFFERENT armor multipliers for each projectile type.

**Example - Arab Swordsman**:
```
vs Arrow:   1500/2000 = 0.75x   (good armor)
vs Bolt:   15000/10000 = 1.50x  (ARMOR-PIERCING - Bolt ignores armor!)
vs Slinger: 1500/2000 = 0.75x   (good armor)
vs Javelin: 2000/4000 = 0.50x   (excellent armor)
```

**Bolt armor-piercing effect**: Bolts have higher multipliers against heavy armor units, effectively "piercing" their protection.

### Melee vs Ranged Armor Independence

**Correlation**: 0.358 (WEAK)

**Conclusion**: Melee and ranged armor are SEPARATE systems. A unit can have:
- Heavy melee armor (low multiplier)
- Light ranged armor (high multiplier)
- Or vice versa

**Example**:
- Knight: 0.50x melee, 0.075x ranged (heavy both)
- Ladderman: Higher melee multiplier, different ranged profile

---

## Armor Class Distribution

### Melee Armor Classes

Found **7 distinct armor classes** covering 73.8% of defenders:

```
Armor Class   Count   Example Units
-----------   -----   -------------
0.50x         ~30     Knight, Swordsman (heavy armor)
0.80x         ~25     Archer, Pikeman (medium armor)
1.00x         ~20     Engineer, Monk (light/no armor)
0.40x         ~15     Lord (very heavy armor)
1.20x         ~10     Ladderman, Horse Archer (vulnerable)
2.00x         ~8      Civilian units (very vulnerable)
0.01x         ~2      Special cases (civilian damage floor)
```

### Ranged Armor Classes

**Each projectile type has different armor classes**:

```
Arrow Armor Classes:   ~40 distinct values
Bolt Armor Classes:    ~45 distinct values (armor-piercing variation)
Slinger Armor Classes: ~38 distinct values
Javelin Armor Classes: ~42 distinct values
```

**Most units (79/80)** have unique armor profiles across projectile types.

---

## Special Cases & Exceptions

### Civilian Damage Floor

- Non-combatant units deal/receive **2 damage** (hardcoded floor)
- Examples: CHIMP_TYPE_CHILD, CHIMP_TYPE_MONK, etc
- This is NOT part of the armor formula

### Damage Caps

- **Ranged damage cap**: 15000 (max damage any unit can take from ranged)
- **Melee damage floor**: 2 (minimum for non-combatants)
- **Rounding**: All damage values rounded to nearest 5

### Formula Exceptions

**137 exceptions found** (4.6% of melee damage values):

Most common exception types:
1. **Same-type matchups**: Units attacking same unit type (e.g., Knight vs Knight)
2. **Special units**: Trebuchet, Assassin (unique mechanics)
3. **Mounted units**: Horse units have penalty modifiers
4. **Pikemen**: Special high-health unit (~50,000 HP)

---

## Summary

### Attacker vs Defender

Every matrix uses the same orientation:

**Melee CSV**:
- **Row** = Defender (damage unit RECEIVES)
- **Column** = Attacker (damage unit DEALS)

**Ranged CSV**:
- **Row** = Defender (damage unit RECEIVES)
- **Column** = Projectile type (the attacker)

### Armor Multiplier

- **Lower** = BETTER armor (takes LESS damage)
- **Higher** = WORSE armor (takes MORE damage)

### Formulas

```
Melee:  Damage = Base_Weapon � Defender_Armor  (93.5% fit)
Ranged: Damage = Base_Projectile � Defender_Armor  (100% fit)
```

### Base Values

```
Engineer Base Damage (Ranged):
  Arrow:   2000
  Bolt:   10000
  Slinger: 2000
  Javelin: 4000
```

---

## What replaced all of this

The armor-multiplier TOML properties proposed by this analysis (`MeleeArmorMultiplier`,
`RangedArmorMultipliers`) were built, then deactivated, then **deleted**. So was the tag system that
was meant to correct the formula's residual error (`UnitTagInteraction`, `UnitTagRegistry`, the
`UnitTags` config generator/loader and the `Tags` property). None of it exists in the codebase.

What the mod ships instead: the seven CSV matrices, which carry a value per attacker/defender pair
directly, so no formula and no correction layer is needed. The loaders apply every value >= 0 as-is,
and `-1` means "leave the game's value alone". The BepInEx global multipliers in
`CrusaderDETweaker_GlobalMultipliers.cfg` scale on top at runtime.

See [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md) for the file list and editing instructions, and
[CLAUDE.md](CLAUDE.md) for where the loaders live.

---

**Generated**: 2025-12-24 (superseded; retained as background)
