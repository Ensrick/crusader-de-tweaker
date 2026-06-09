// Config/Toml/Units/Properties/ShieldHealthProperty.cs
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the ShieldHealth property for Bedouin Demolisher units.
    /// Uses GameUnitManagerAPI.GetDefaultShieldHealth / SetDefaultShieldHealth.
    /// </summary>
    internal class ShieldHealthProperty : PropertyHandler<eChimps, ushort>
    {
        public ShieldHealthProperty() : base("ShieldHealth")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out ushort value)
        {
            if (unit == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER && Plugin.UnitApi != null)
            {
                value = Plugin.UnitApi.GetDefaultShieldHealth();
                return true;
            }

            value = 0;
            return false;
        }

        protected override void SetToAPI(eChimps unit, ushort value)
        {
            if (unit == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)
                Plugin.UnitApi?.SetDefaultShieldHealth(value);
        }

        protected override bool TryGetOriginalValue(eChimps unit, out ushort defaultValue)
        {
            if (unit == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER && Plugin.UnitApi != null)
            {
                defaultValue = Plugin.UnitApi.GetDefaultShieldHealth();
                return true;
            }
            defaultValue = 0;
            return false;
        }

        internal override bool ValidateValue(ushort value) => value > 0;

        internal override bool CanApplyTo(eChimps unit) => unit == eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER;
    }
}
