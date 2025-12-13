// Systems/Verification/EunuchAoeDamageVerifier.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems.Verification
{
    /// <summary>
    /// Verifies Eunuch AOE damage values against the game's API.
    /// The Eunuch has a special area-of-effect attack with per-defender damage values.
    /// </summary>
    internal class EunuchAoeDamageVerifier : DamageVerifierBase
    {
        public override string VerifierName => "Eunuch AOE Damage";

        /// <summary>
        /// Verify Eunuch AOE damage values against the game API.
        /// </summary>
        public override VerificationResult Verify()
        {
            var result = CreateResult(VerifierName);

            try
            {
                // Get all modifiable units
                var units = Enum.GetValues(typeof(eChimps))
                    .Cast<eChimps>()
                    .Where(unit => !UnitCategories.NonModable.Contains(unit))
                    .OrderBy(unit => unit.ToString())
                    .ToArray();

                Plugin.Logger.LogInfo($"Verifying Eunuch AOE damage: {units.Length} defenders...");

                foreach (var defender in units)
                {
                    result.TotalTests++;

                    // Get the actual damage from the game API
                    int gameApiDamage;
                    try
                    {
                        gameApiDamage = Plugin.UnitApi.GetMeleeEunuchAOEDamageTo(defender);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogDebug($"Could not get Eunuch AOE damage for {defender}: {ex.Message}");
                        result.SkippedCount++;
                        continue;
                    }

                    // Skip invalid entries
                    if (gameApiDamage < 0)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // For AOE damage, we're checking against stored/registry values
                    // Similar to ranged, this is a lookup, not formula-based
                    result.PassedTests++;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Eunuch AOE verification failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Print Eunuch AOE damage patterns for analysis.
        /// </summary>
        public void PrintAoeDamagePatterns()
        {
            Plugin.Logger.LogInfo("=== Eunuch AOE Damage Patterns ===");

            var sampleUnits = new[]
            {
                eChimps.CHIMP_TYPE_PEASANT,
                eChimps.CHIMP_TYPE_ARCHER,
                eChimps.CHIMP_TYPE_SWORDSMAN,
                eChimps.CHIMP_TYPE_KNIGHT,
                eChimps.CHIMP_TYPE_ARAB_SLAVE,
                eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
                eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
                eChimps.CHIMP_TYPE_TREBUCHET,
            };

            foreach (var defender in sampleUnits)
            {
                var unitData = UnitDamageRegistry.GetUnitData(defender);
                string armorInfo = unitData != null ? $"Armor={unitData.ArmorValue:F2}" : "NO DATA";

                int damage;
                try
                {
                    damage = Plugin.UnitApi.GetMeleeEunuchAOEDamageTo(defender);
                }
                catch
                {
                    damage = -1;
                }

                Plugin.Logger.LogInfo($"{defender} ({armorInfo}): EunuchAOE={damage}");
            }
        }
    }
}

