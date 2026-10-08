// Config/Toml/Units/Properties/GoodYieldMultiplierProperty.cs
//
// PURPOSE: Units TOML GoodYieldMultiplier for the 17 worker types (GitHub #1): goods carried back per work cycle,
//          as a multiple of the game's amount. 1 (or -1) = the game's yield.
//
// The value lives in this mod's own table (Config/Core/GoodYield.cs), applied by the Script Extender's yield hook;
// see that file for the game mechanics. TemplateBaseline captures 1 on the first write, so -1 restores the game.
//
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal class GoodYieldMultiplierProperty : PropertyHandler<eChimps, float>
    {
        internal const float Max = 100f;

        public GoodYieldMultiplierProperty() : base("GoodYieldMultiplier") { }

        internal override bool CanApplyTo(eChimps unit) => GoodYield.IsWorker(unit);

        protected override bool TryGetFromAPI(eChimps unit, out float value)
        {
            value = GoodYield.Get(unit);
            return true;
        }

        protected override bool TryGetOriginalValue(eChimps unit, out float defaultValue)
        {
            defaultValue = 1f;
            return true;
        }

        protected override void SetToAPI(eChimps unit, float value) => GoodYield.Set(unit, value);

        // 0 would leave the worker with nothing to carry; the effect of a zero carried count on the worker's
        // drop-off loop was not traced, so it is refused.
        internal override bool ValidateValue(float value) => value > 0f && value <= Max;
    }
}
