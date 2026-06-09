// Config/DamageMatrix/Core/MeleeDefaultsCapture.cs
//
// PURPOSE: Captures and caches original melee damage defaults from game API.
//
// USAGE:
// - Capture(): Called by OriginalDefaultsCapture to capture all melee damage values
// - Get(): Called by CsvMatrixReader to retrieve original default for comparison
//
// IMPORTANT FOR AI AGENTS:
// - Captures values directly from game API (Plugin.UnitApi.GetMeleeDamageFromTo)
// - Only captures modifiable units (non-modifiable units are skipped)
// - Caches values in dictionary for O(1) lookup performance
// - Returns -1 if value not found (indicates API call failed or unit not modifiable)
// - Used by CSV matrix loaders to determine if CSV value differs from original default
//
using System;
using System.Collections.Generic;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Captures and provides access to original melee damage defaults.
    /// 
    /// Reads damage values directly from game API before any mods are applied.
    /// Caches values for fast lookup during CSV matrix loading.
    /// </summary>
    internal static class MeleeDefaultsCapture
    {
        private static Dictionary<(eChimps attacker, eChimps defender), int> _cache;

        /// <summary>
        /// Capture original melee damage defaults from game API.
        /// </summary>
        public static void Capture(eChimps[] units)
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<(eChimps attacker, eChimps defender), int>();
            
            Plugin.Logger.LogInfo("Capturing original melee damage defaults from game API...");
            int captured = 0;
            int errors = 0;

            foreach (var attacker in units)
            {
                foreach (var defender in units)
                {
                    try
                    {
                        int damage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);
                        if (damage >= 0)
                        {
                            _cache[(attacker, defender)] = damage;
                            captured++;
                        }
                    }
                    catch (Exception)
                    {
                        errors++;
                    }
                }
            }

            Plugin.Logger.LogInfo($"Captured {captured} original melee damage defaults ({(errors > 0 ? $"{errors} errors" : "no errors")})");
        }

        /// <summary>
        /// Get original melee damage default. Returns -1 if not found.
        /// </summary>
        public static int Get(eChimps attacker, eChimps defender)
        {
            return _cache != null && _cache.TryGetValue((attacker, defender), out int damage) 
                ? damage 
                : -1;
        }
    }
}

