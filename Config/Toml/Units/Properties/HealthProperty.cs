// Config/Toml/Units/Properties/HealthProperty.cs
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the Health property for units.
    /// </summary>
    internal class HealthProperty : PropertyHandler<eChimps, uint>
    {
        public HealthProperty() : base("Health")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out uint value)
        {
            return ErrorHandlingHelper.TryGetValueWithResult(
                $"Get {Name}",
                unit.ToString(),
                () => Plugin.UnitApi.GetDefaultHealth(unit),
                out value,
                defaultValue: 0u
            );
        }

        protected override void SetToAPI(eChimps unit, uint value)
        {
            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                unit.ToString(),
                () => Plugin.UnitApi.SetDefaultHealth(unit, value)
            );
        }

        internal override bool ValidateValue(uint value)
        {
            // Health must be positive
            if (value == 0)
            {
                return false;
            }
            return true;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            // All units have health
            return true;
        }

        /// <summary>
        /// Try to get the original/default value from the API (captured before TOML modifications).
        /// For properties that read directly from API, we use the current API value as default when generating.
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out uint defaultValue)
        {
            // When generating config, current API value is the default
            // This is called during config generation, so API still has original values
            return TryGetFromAPI(unit, out defaultValue);
        }
    }
}