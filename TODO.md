# CrusaderDETweaker — TODO

---

## Worker Good Yield (per unit type, in Units TOML)

**Feature**: Add a `GoodYield` property to worker unit types in `CrusaderDETweaker_Units.toml`, controlling how many units of their respective good are deposited per work cycle.

**Example config:**
```toml
[CHIMP_TYPE_FARMER_APPLE]
GoodYield = 4.5   # default (game value). increase to 9.0 for double yield

[CHIMP_TYPE_FARMER_CATTLE]
GoodYield = 4.5

[CHIMP_TYPE_WOODCUTTER]
GoodYield = 3.0
```

**Implementation approach:**

No native per-cycle yield API exists in SHCDE-SE — the value is hardcoded in the engine. The intercept point is `BuildingR3EventHooks.OnGoodsyardAddGood` (Pre phase), which fires when a worker deposits goods into a Granary, Goodsyard, or Armoury. `args.AddAmount` is writable.

```csharp
BuildingR3EventHooks.OnGoodsyardAddGood.Observable
    .Where(args => args.Phase == EventHookPhase.Pre && args.Add == true)
    .Subscribe(args =>
    {
        // Look up which worker type produces this good
        // Apply configured multiplier to args.AddAmount
        // args.AddAmount is int; game's 4.5 arrives rounded — multiply from there
    });
```

**Worker → good mapping needed** (to know which config entry to look up for a given `args.Good`):

| Unit type | Good (`eGoods`) |
|-----------|----------------|
| `CHIMP_TYPE_FARMER_APPLE`   | `STORED_FOOD_FRUIT`   |
| `CHIMP_TYPE_FARMER_CATTLE`  | `STORED_FOOD_CHEESE`  |
| `CHIMP_TYPE_FARMER_WHEAT`   | `STORED_RAW_WHEAT`    |
| `CHIMP_TYPE_FARMER_HOPS`    | `STORED_RAW_HOPS`     |
| `CHIMP_TYPE_HUNTER`         | `STORED_FOOD_MEAT`    |
| `CHIMP_TYPE_WOODCUTTER`     | `STORED_WOOD_PLANKS`  |
| `CHIMP_TYPE_QUARRY_MASON`   | `STORED_STONE`        |
| `CHIMP_TYPE_PITCHMAN`       | `STORED_PITCH`        |
| `CHIMP_TYPE_MILLER`         | `STORED_FLOUR`        |
| `CHIMP_TYPE_BAKER`          | `STORED_FOOD_BREAD`   |
| `CHIMP_TYPE_BREWER`         | `STORED_FOOD_ALE`     |
| `CHIMP_TYPE_POLETURNER`     | `STORED_SPEAR`        |
| `CHIMP_TYPE_BLACKSMITH`     | (iron goods — TBD)    |
| `CHIMP_TYPE_ARMOURER`       | (armour — TBD)        |
| `CHIMP_TYPE_TANNER`         | `STORED_LEATHER`      |
| `CHIMP_TYPE_FLETCHER`       | (bows/xbows — TBD, switchable production) |

**Caveats / open questions:**
- `OnGoodsyardAddGood` fires for deposits into Granary, Goodsyard, and Armoury. Buildings with purely internal production buffers (mill input → flour output) may not route through this event — needs testing.
- `args.AddAmount` is an `int`, so the game's 4.5 is already rounded before this event. Config values should be treated as multipliers on top of whatever int arrives, OR stored as absolute replacement values — decide before implementing.
- The event does not expose which worker deposited the goods, only `args.Good` and `args.BuildingId`. The mapping above (good → unit type) will need to handle goods produced by multiple unit types if any exist.
- Fletcher's Workshop is switchable (bows vs crossbows) — `args.Good` will differ depending on current production mode; handle both.
- Quarry has multiple worker types (`QUARRY_MASON`, `QUARRY_GRUNT`, `QUARRY_OX`) — confirm which one(s) trigger the deposit event.
