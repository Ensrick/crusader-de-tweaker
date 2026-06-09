// Config/DamageMatrix/Core/EunuchAoeDefaultsCapture.cs
//
// PURPOSE: Captures and caches original Eunuch AOE damage defaults from game API.
//
// USAGE:
// - Capture(): Called by OriginalDefaultsCapture to capture all Eunuch AOE damage values
// - Get(): Called by CsvMatrixReader to retrieve original default for comparison
//
// IMPORTANT FOR AI AGENTS:
// - Captures AOE damage for Bedouin Eunuch unit's area-of-effect attack
// - Only one attacker type (Eunuch), so simpler than melee/ranged (no attacker loop)
// - Only captures modifiable units (non-modifiable units are skipped)
// - Caches values in dictionary for O(1) lookup performance
// - Returns -1 if value not found (indicates API call failed or unit not modifiable)
//
using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Captures and provides access to original Eunuch AOE damage defaults.
    /// 
    /// Reads damage values directly from game API before any mods are applied.
    /// Captures AOE damage for Bedouin Eunuch's area-of-effect attack against all modifiable units.
    /// Caches values for fast lookup during CSV matrix loading.
    /// </summary>
    internal static class EunuchAoeDefaultsCapture
    {
        private static Dictionary<eChimps, int> _cache;

        /// <summary>
        /// Capture original Eunuch AOE damage defaults from game API.
        /// </summary>
        public static void Capture(eChimps[] units)
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<eChimps, int>();
            
            Plugin.Logger.LogInfo("Capturing original Eunuch AOE damage defaults from game API...");
            int captured = 0;
            int errors = 0;

            foreach (var defender in units)
            {
                try
                {
                    int damage = Plugin.UnitApi.GetMeleeEunuchAOEDamageTo(defender);
                    if (damage >= 0)
                    {
                        _cache[defender] = damage;
                        captured++;
                    }
                }
                catch (Exception)
                {
                    errors++;
                }
            }

            Plugin.Logger.LogInfo($"Captured {captured} original Eunuch AOE damage defaults ({(errors > 0 ? $"{errors} errors" : "no errors")})");
        }

        /// <summary>
        /// Get original Eunuch AOE damage default. Returns -1 if not found.
        /// </summary>
        public static int Get(eChimps defender)
        {
            return _cache != null && _cache.TryGetValue(defender, out int damage) 
                ? damage 
                : -1;
        }
    }
}

