// Config/DamageMatrix/BedouinHeal/BedouinHealDefaultsCapture.cs
// AI DEV: Captures original Bedouin heal values per unit type before any mods are applied.
// Called by OriginalDefaultsCapture.CaptureAll() as part of the pre-config capture sequence.

using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BedouinHeal
{
    internal static class BedouinHealDefaultsCapture
    {
        private static bool _isCaptured = false;
        private static readonly Dictionary<eChimps, int> _originalDefaults = new Dictionary<eChimps, int>();

        public static void Capture(eChimps[] modifiableUnits)
        {
            if (_isCaptured) return;

            foreach (var unit in modifiableUnits)
            {
                try
                {
                    int val = Plugin.UnitApi.GetBedouinHeal(unit);
                    _originalDefaults[unit] = val;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogDebug($"Could not capture original Bedouin heal for {unit}: {ex.Message}");
                }
            }

            _isCaptured = true;
        }

        public static int Get(eChimps unit)
        {
            if (_originalDefaults.TryGetValue(unit, out int val))
                return val;
            return -1;
        }
    }
}
