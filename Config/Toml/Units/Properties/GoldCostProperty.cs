// Config/Toml/Units/Properties/GoldCostProperty.cs
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the GoldCost property for units.
    /// Only applies to military units that can be recruited.
    /// </summary>
    internal class GoldCostProperty : PropertyHandler<eChimps, int>
    {
        public GoldCostProperty() : base("GoldCost")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            return ErrorHandlingHelper.TryGetValueWithResult(
                $"Get {Name}",
                unit.ToString(),
                () => Plugin.UnitApi.GetUnitGoldCost(unit),
                out value,
                defaultValue: 0
            );
        }

        protected override void SetToAPI(eChimps unit, int value)
        {
            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                unit.ToString(),
                () => Plugin.UnitApi.SetUnitGoldCost(unit, value)
            );
        }

        internal override bool ValidateValue(int value)
        {
            return value >= 0;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            return UnitCategories.IsRecruitable(unit);
        }

        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue)
        {
            return TryGetFromAPI(unit, out defaultValue);
        }
    }
}