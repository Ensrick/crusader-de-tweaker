# CrusaderDETweaker — TODO

Pending work lives in the issue trackers:
[GitLab](https://gitlab.com/ensrick7/crusader-de-tweaker/-/issues) (main) and
[GitHub](https://github.com/Ensrick/crusader-de-tweaker/issues) (mirror).

The former "Worker Good Yield" plan here (an `OnGoodsyardAddGood` hook, "4.5" default yields) was replaced by the
Units file `GoodYieldMultiplier` (GitHub #1, `Config/Core/GoodYield.cs`): every deposit is 1 good and names no
worker, so the yield is scaled where the game computes a worker's trip (RVA 0x18D940) instead.
