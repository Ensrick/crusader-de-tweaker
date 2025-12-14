// Config/Toml/Units/Properties/SpeedProperty.cs
using System;
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the Speed property for units.
    /// </summary>
    internal class SpeedProperty : PropertyHandler<eChimps, int>
    {
        public SpeedProperty() : base("Speed")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            try
            {
                value = Plugin.UnitApi.GetDefaultSpeed(unit);
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                // Some units don't have speed data in the game's array (animals, special units, etc.)
                // Return a default value so the property is still written to config
                value = 1;
                return true;
            }
            catch
            {
                // If API call fails for any other reason, use default value of 1 so property is still written
                value = 1;
                return true;
            }
        }

        protected override void SetToAPI(eChimps unit, int value)
        {
            try
            {
                Plugin.UnitApi.SetDefaultSpeed(unit, (ushort)value);
            }
            catch (IndexOutOfRangeException)
            {
                // Some units don't have speed data in the game's array
                // Log a warning but don't throw - the property was written to config but can't be applied
                Plugin.Logger.LogWarning($"Cannot set Speed for {unit}: Unit does not have speed data in game's array");
            }
            catch (Exception ex)
            {
                // Log other exceptions but don't throw
                Plugin.Logger.LogWarning($"Failed to set Speed for {unit}: {ex.Message}");
            }
        }

        internal override bool ValidateValue(int value)
        {
            // Speed is clamped between 0 and 6 by the API (0-6 range per API documentation)
            // Lower values mean faster movement
            return value >= 0 && value <= 6;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            // Most units have speed, but TryGetFromAPI will handle units that don't
            return true;
        }
    }
}