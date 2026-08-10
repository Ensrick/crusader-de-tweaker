# Developer Notes - Critical Information

> **CRITICAL**: Read this before making changes to build paths, GUID, or initialization order.

---

## Plugin GUID and Build Paths

### Plugin GUID

```csharp
[BepInPlugin("CrusaderDETweaker", "Crusader DE Tweaker", PluginInfo.PLUGIN_VERSION)]
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
1. Register configuration systems
2. Load TOML configuration
3. Load CSV damage matrices
4. Register runtime hooks and defer session APIs until map-load events
```

### Why Order Matters

Native game/session APIs must not run during SHCDE-SE `LibraryLoaded`; they are safe only
after map startup. Keep global/session application in the registered map hooks. TOML and CSV
systems must initialize before runtime hooks consume their values.

---

## CSV vs TOML Logic

### CSV Comparison

Original-default capture was removed because reading native globals during `LibraryLoaded`
could crash the game. CSV loaders now apply configured matrix values directly; TOML property
settings and runtime multipliers are separate layers.

---

## Common Mistakes

| Mistake | Consequence |
|---------|-------------|
| Change GUID without updating paths | Plugin won't load |
| Call native globals during `LibraryLoaded` | Game may crash during startup |
| Register hooks before configs are loaded | Hooks may observe incomplete settings |

---

## Testing Checklist

Before submitting changes:

- [ ] Build succeeds: `.\scripts\build.ps1`
- [ ] Game loads plugin (check BepInEx console)
- [ ] Initializer order check passes: `.\scripts\test_initialization_order.ps1`
- [ ] Smoke launch passes: `.\scripts\launch_game.ps1`
- [ ] TOML changes apply correctly
- [ ] CSV overrides work for modified values only

---

## Related Documentation

- [CLAUDE.MD](CLAUDE.MD) - Full architecture reference
- [API_REFERENCE.md](docs/shcde-se/API_REFERENCE.md) - SHCDE-SE API details
