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
            // Use specialized handler for IndexOutOfRangeException (common for units without speed data)
            bool success = ErrorHandlingHelper.TryGetValueWithIndexCheck(
                $"Get {Name}",
                unit.ToString(),
                () => Plugin.UnitApi.GetDefaultSpeed(unit),
                defaultValue: 1,
                out value
            );

            // Always return true so property is written to config even if API fails
            return true;
        }

        protected override void SetToAPI(eChimps unit, int value)
        {
            // Use specialized handler for IndexOutOfRangeException
            try
            {
                Plugin.UnitApi.SetDefaultSpeed(unit, (ushort)value);
            }
            catch (IndexOutOfRangeException)
            {
                // Expected for units that don't have speed data in the game's array
                Plugin.Logger.LogWarning($"[Set {Name}] {unit} does not have speed data in game's array (IndexOutOfRangeException). Property written to config but cannot be applied.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Set {Name}] Failed for {unit}: {ex.GetType().Name} - {ex.Message}");
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