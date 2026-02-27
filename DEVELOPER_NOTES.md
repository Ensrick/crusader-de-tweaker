# Developer Notes - Critical Information

> **CRITICAL**: Read this before making changes to build paths, GUID, or initialization order.

---

## Plugin GUID and Build Paths

### Plugin GUID

```csharp
[BepInPlugin("CrusaderDETweaker", "Crusader DE Tweaker", "1.0")]
```

**GUID**: `"CrusaderDETweaker"`

**DO NOT CHANGE** without updating:
1. `CrusaderDETweaker.csproj` OutputPath
2. `scripts/build.ps1` path references
3. All documentation

### Build Output Location

**Both Debug AND Release output to:**
```
{GameDir}\BepInEx\plugins\CrusaderDETweaker\CrusaderDETweaker.dll
```

**Why?** BepInEx loads plugins from folders matching their GUID.

---

## Config Loading Order

**The order is CRITICAL and must not change:**

```
1. Capture Original Defaults    ← OriginalDefaultsCapture.CaptureAll()
        ↓
2. Load TOML Configs           ← UnitConfigSystem, StructureConfigSystem
        ↓
3. Load CSV Damage Matrices    ← DamageMatrixConfigSystem
        ↓
4. BepInEx Hooks               ← Real-time multipliers
```

### Why Order Matters

1. **Original Defaults** - Captured from game API BEFORE any mods apply
2. **TOML Configs** - Modify properties using captured defaults as reference
3. **CSV Matrices** - Only apply values that DIFFER from original defaults
4. **BepInEx Hooks** - Apply real-time multipliers on top

**If order changes**: CSV comparison breaks, TOML changes get overwritten.

---

## CSV vs TOML Logic

### CSV Comparison

CSV values are ONLY applied if they differ from **original defaults**:

```
IF csv_value == original_default:
    SKIP (let TOML handle it)
ELSE:
    APPLY csv_value (user modified it)
```

### PREVIOUS MISTAKE (Fixed)

Old code compared CSV against calculated values instead of original defaults. This caused:
- CSV overriding TOML changes
- User TOML edits being ignored

### Current Approach

1. `OriginalDefaultsCapture.CaptureAll()` saves pristine game values
2. TOML applies formula-based modifications
3. CSV compares against original (not modified) values
4. Only user-modified CSV values are applied

---

## Common Mistakes

| Mistake | Consequence |
|---------|-------------|
| Change GUID without updating paths | Plugin won't load |
| Change config load order | TOML changes ignored |
| Compare CSV to calculated values | CSV overrides everything |
| Skip original defaults capture | Can't detect user CSV changes |

---

## Testing Checklist

Before submitting changes:

- [ ] Build succeeds: `.\scripts\build.ps1`
- [ ] Game loads plugin (check BepInEx console)
- [ ] Unit tests pass ("ALL 4 TEST SUITES PASSED")
- [ ] TOML changes apply correctly
- [ ] CSV overrides work for modified values only

---

## Related Documentation

- [CLAUDE.MD](CLAUDE.MD) - Full architecture reference
- [API_REFERENCE.md](API_REFERENCE.md) - SHCDE-SE API details
