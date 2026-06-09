// Config/DamageMatrix/BuildingFireDamage/BuildingFireDefaultsCapture.cs
// AI DEV: Captures original fire damage values per building type before any mods are applied.
// Called by OriginalDefaultsCapture.CaptureAll() as part of the pre-config capture sequence.

using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage
{
    internal static class BuildingFireDefaultsCapture
    {
        private static bool _isCaptured = false;
        private static readonly Dictionary<eStructs, int> _originalDefaults = new Dictionary<eStructs, int>();

        public static void Capture()
        {
            if (_isCaptured) return;

            var buildings = BuildingMatrixHelper.GetModifiableBuildings();
            foreach (var building in buildings)
            {
                try
                {
                    short val = Plugin.BuildingApi.GetBuildingFireDamage(building);
                    _originalDefaults[building] = val;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogDebug($"Could not capture original building fire damage for {building}: {ex.Message}");
                }
            }

            _isCaptured = true;
        }

        public static int Get(eStructs building)
        {
            if (_originalDefaults.TryGetValue(building, out int val))
                return val;
            return -1;
        }
    }
}
