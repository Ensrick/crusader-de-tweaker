// Config/Toml/Units/Properties/SpeedProperty.cs
//
// Speed = the game's per-type movement delay (gSpeedTable, low 16 bits of one uint32 per eChimps).
// Native semantics (CrusaderDE.dll, game 2.8.x; Ghidra, 2026-10-07, GitLab #6):
//   - c_game_unit_init (0x19A240) copies the table value into the unit's speed fields; nothing else
//     reads the table, so a change applies to units created afterwards. Units already on the map or
//     restored from a save keep the speed they were created with.
//   - The unit tick (0x182B00) takes a movement step when more than (speed + slope/terrain + speed bonus)
//     ticks have passed: smaller = faster, 0 = fastest. The value is signed 16-bit with no table lookup,
//     so values above 6 are safe; 6 is only the Script Extender's clamp in SetDefaultSpeed.
//   - Siege engines (catapult, siege tower, ram, portable shield) use the table value; their update code
//     only adjusts the speed bonus. Workers, animals, the lord, engineers and miners switch to hard-coded
//     per-task speeds, so for them Speed only sets the starting value.
// Values 0-6 go through SE's SetDefaultSpeed; 7..MaxSpeed through SE's own table object (its internal
// _speedDefaultsArray), so SE's map-unload reset (ClearOverrides) still covers them.
//
using System;
using System.Reflection;
using CrusaderDETweaker.Config.Toml.Core;
using RedBird.Core.Memory.Managed;
using SHCDESE.API;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the Speed property for units.
    /// </summary>
    internal class SpeedProperty : PropertyHandler<eChimps, int>
    {
        /// <summary>Highest value SE's SetDefaultSpeed accepts (it clamps above).</summary>
        private const int SeMaxSpeed = 6;

        /// <summary>Highest value this mod accepts (about 4x slower than 6; see the header comment).</summary>
        internal const int MaxSpeed = 30;

        private static readonly FieldInfo SpeedTableField =
            typeof(GameUnitManagerAPI).GetField("_speedDefaultsArray", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

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
                if (value <= SeMaxSpeed)
                    Plugin.UnitApi.SetDefaultSpeed(unit, (ushort)value);
                else if (!TrySetAboveSeClamp(unit, (ushort)value))
                {
                    Plugin.Logger.LogWarning($"[Set {Name}] {unit}: the Script Extender's speed table could not be reached, so {value} was applied as {SeMaxSpeed} (the Script Extender's maximum).");
                    Plugin.UnitApi.SetDefaultSpeed(unit, SeMaxSpeed);
                }
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

        /// <summary>
        /// Write a value above SE's clamp through SE's own table object, the same way SetDefaultSpeed does
        /// (upper 16 bits preserved), so SE still records the original and resets it on map unload.
        /// </summary>
        private static bool TrySetAboveSeClamp(eChimps unit, ushort value)
        {
            if (!(SpeedTableField?.GetValue(Plugin.UnitApi) is ManagedNativeArray<uint> table)) return false;
            int index = (int)unit;
            if (index < 0 || index >= table.Length) return false;
            uint current = table.GetValue(index);
            table.SetValue(index, (current & 0xFFFF0000u) | value);
            return true;
        }

        internal override bool ValidateValue(int value)
        {
            // Movement delay: 0 = fastest; the game has no upper limit, MaxSpeed keeps typos sane.
            return value >= 0 && value <= MaxSpeed;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            // Stationary siege weapons have no speed property
            if (unit == eChimps.CHIMP_TYPE_TREBUCHET ||
                unit == eChimps.CHIMP_TYPE_MANGONEL ||
                unit == eChimps.CHIMP_TYPE_BALLISTA)
                return false;

            return true;
        }

        /// <summary>
        /// Try to get the original/default value from the API (captured before TOML modifications).
        /// For properties that read directly from API, we use the current API value as default when generating.
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue)
        {
            // When generating config, current API value is the default
            // This is called during config generation, so API still has original values
            return TryGetFromAPI(unit, out defaultValue);
        }
    }
}
