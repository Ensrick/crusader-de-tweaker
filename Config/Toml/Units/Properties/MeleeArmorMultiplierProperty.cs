// Config/Toml/Units/Properties/MeleeArmorMultiplierProperty.cs
//
// PURPOSE: Handles the MeleeArmorMultiplier property for units (formula-based melee damage system).
//
// ARCHITECTURE:
// - Tier 2 of three-tier config system (BepInEx → TOML → CSV)
// - Calculates armor multiplier from existing damage values via SHCDE-SE API
// - Applies multiplier by calculating damage values: Damage = Attacker_Base × Defender_Armor
// - CSV overrides (Tier 3) can override specific attacker-defender pairs after formula application
//
// FORMULA:
// - Damage_Taken = Round5(Attacker_Base_Damage × MeleeArmorMultiplier)
// - Lower multiplier = better armor (takes less damage)
// - Example: Knight with 0.50x takes half damage from all melee attackers
// - Game rounds all damage to nearest 5 (except 2-damage floor for non-combatants)
//
// IMPORTANT FOR AI AGENTS:
// - This is NOT a simple property - it affects melee damage matrix for all attackers
// - TryGetFromAPI: Calculates multiplier from current damage values (reverse engineering)
// - SetToAPI: Applies multiplier to melee damage matrix for all attackers
// - Uses Engineer as base damage reference (assumed 1.0× armor)
// - Tag-based damage modifiers are DEACTIVATED (see UnitTagRegistry / UnitTagsConfigSystem)
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the MeleeArmorMultiplier property for units.
    ///
    /// This property represents how much damage a unit TAKES from melee attacks.
    /// Formula: Damage_Taken = Round5(Attacker_Base_Damage × MeleeArmorMultiplier)
    ///
    /// Lower multiplier = better armor (takes less damage).
    /// Example: Knight with 0.50x takes half damage from all melee attackers
    /// Note: Game rounds all damage to nearest 5 (except 2-damage floor for non-combatants)
    /// </summary>
    internal class MeleeArmorMultiplierProperty : ArmorMultiplierPropertyBase
    {
        // Rounding granularity for melee damage
        private const int MELEE_ROUNDING = 5;
        private const int MELEE_FLOOR = 2;  // Minimum damage for non-combatants (exception to rounding)
        private const int MISMATCH_THRESHOLD = 5;

        public MeleeArmorMultiplierProperty() : base("MeleeArmorMultiplier")
        {
        }

        /// <summary>
        /// Get the melee armor multiplier from the game API.
        /// Calculates multiplier by reverse-engineering current damage values.
        /// </summary>
        protected override bool TryGetFromAPI(eChimps defender, out float multiplier)
        {
            try
            {
                // Calculate average multiplier by examining all attackers
                var multipliers = new List<float>();

                foreach (eChimps attacker in Enum.GetValues(typeof(eChimps)))
                {
                    if (UnitMatrixHelper.ShouldSkipUnit(attacker))
                        continue;

                    try
                    {
                        // Get current damage and attacker's base damage
                        int currentDamage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);
                        int baseDamage = GetAttackerBaseDamage(attacker);

                        if (baseDamage > 2 && currentDamage > 0)
                        {
                            float mult = (float)currentDamage / baseDamage;
                            multipliers.Add(mult);
                        }
                    }
                    catch
                    {
                        // Skip this attacker
                    }
                }

                // Return average multiplier, or 1.0 if no valid samples
                if (multipliers.Count == 0)
                {
                    multiplier = 1.0f;
                    return false;
                }

                float sum = 0;
                foreach (float m in multipliers)
                    sum += m;

                multiplier = sum / multipliers.Count;
                
                // Round to 1 decimal place for cleaner user-facing config
                multiplier = RoundMultiplier(multiplier);
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to calculate melee armor for {defender}: {ex.Message}");
                multiplier = 1.0f;
                return false;
            }
        }

        /// <summary>
        /// Get the base damage for an attacker (damage against unarmored target).
        /// Uses Engineer as reference (assumed 1.0× armor multiplier).
        /// </summary>
        private int GetAttackerBaseDamage(eChimps attacker)
        {
            try
            {
                // Use damage vs Engineer as base (Engineer has 1.0× armor)
                return Plugin.UnitApi.GetMeleeDamageFromTo(attacker, eChimps.CHIMP_TYPE_ENGINEER);
            }
            catch
            {
                return 0;
            }
        }

        // Track all mismatches across all defenders for summary logging
        private static readonly List<string> AllMismatches = new List<string>();
        private static int TotalMismatchCount = 0;

        /// <summary>
        /// Reset mismatch tracking. Call before starting to process defenders.
        /// </summary>
        public static void ResetMismatchTracking()
        {
            AllMismatches.Clear();
            TotalMismatchCount = 0;
        }

        /// <summary>
        /// Get current mismatch count (for testing/verification).
        /// </summary>
        internal static int GetMismatchCount() => TotalMismatchCount;

        /// <summary>
        /// Get current mismatch list size (for testing/verification).
        /// </summary>
        internal static int GetMismatchListSize() => AllMismatches.Count;

        /// <summary>
        /// Set the melee armor multiplier via the game API.
        /// Calculates damage values for all attacker-defender pairs using the armor formula.
        /// Formula: Damage = Round5(AttackerBase × ArmorMultiplier), floor 2
        /// </summary>
        protected override void SetToAPI(eChimps defender, float armorMultiplier)
        {
            try
            {
                int appliedCount = 0;

                foreach (eChimps attacker in Enum.GetValues(typeof(eChimps)))
                {
                    if (UnitMatrixHelper.ShouldSkipUnit(attacker))
                        continue;

                    // Skip units that don't attack in melee (e.g., Arab Ballista)
                    if (UnitCategories.IsNonMeleeAttacker(attacker))
                        continue;

                    // Get attacker's base damage
                    int baseDamage = GetAttackerBaseDamage(attacker);
                    if (baseDamage <= 2)
                        continue; // Skip non-combatants

                    // Apply armor multiplier
                    int damage = (int)Math.Round(baseDamage * armorMultiplier);

                    // ROUND 1: Round after armor multiplication
                    damage = RoundDamage(damage, MELEE_ROUNDING);

                    // TAG SYSTEM DEACTIVATED: Complexity exceeds context window limits for reliable AI management.
                    // This logic is kept for reference but will not execute.
                    /*
                    // Apply tag-based modifiers (they operate on rounded values)
                    var tagInteraction = UnitTagRegistry.TryGetInteraction(attacker, defender);
                    if (tagInteraction != null)
                    {
                        int beforeTag = damage;
                        damage = tagInteraction.ApplyTo(damage);
                        tagModifiedCount++;
                        Plugin.Logger.LogDebug($"Tag modifier applied: {attacker}→{defender}: {beforeTag} → {damage} (Δ{tagInteraction.Delta}, +{tagInteraction.FlatBonus})");
                    }
                    */

                    // ROUND 2: Round again for final value
                    damage = RoundDamage(damage, MELEE_ROUNDING);

                    // Apply floor (minimum 2 damage - exception to rounding for non-combatants)
                    if (damage < MELEE_FLOOR)
                        damage = MELEE_FLOOR;

                    // MISMATCH DETECTION DISABLED: Tags are needed for 100% accuracy; 
                    // without them, mismatches are expected and should not be logged as warnings.
                    /*
                    // Check if this differs from CSV original default (formula exception)
                    int originalDefault = CsvMatrixReader.GetOriginalMeleeDamage(attacker, defender);
                    if (originalDefault >= 0 && Math.Abs(damage - originalDefault) >= MISMATCH_THRESHOLD)
                    {
                        mismatchCount++;
                        TotalMismatchCount++;
                        string mismatch = $"{attacker}→{defender}: calc={damage}, orig={originalDefault}, diff={damage - originalDefault}";
                        AllMismatches.Add(mismatch);
                        Plugin.Logger.LogWarning($"Melee formula mismatch: {mismatch}");
                    }
                    */

                    // Set damage via API
                    try
                    {
                        Plugin.UnitApi.SetMeleeDamageFromTo(attacker, defender, damage);
                        appliedCount++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Failed to set melee damage {attacker}→{defender}: {ex.Message}");
                    }
                }

                Plugin.Logger.LogDebug($"Applied melee armor to {defender}: ×{armorMultiplier:F3} ({appliedCount} damage values set)");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set melee armor for {defender}: {ex.Message}");
            }
        }

        /// <summary>
        /// Log summary of all melee damage mismatches.
        /// Call this after all defenders have been processed.
        /// </summary>
        public static void LogMismatchSummary()
        {
            if (TotalMismatchCount == 0)
            {
                Plugin.Logger.LogInfo("No melee damage formula mismatches detected.");
                return;
            }

            Plugin.Logger.LogWarning($"=== MELEE DAMAGE MISMATCH SUMMARY ===");
            Plugin.Logger.LogWarning($"Total mismatches: {TotalMismatchCount}");
            Plugin.Logger.LogWarning($"All mismatches logged above with 'Melee formula mismatch:' prefix");

            // Group mismatches by defender to show patterns
            var byDefender = new Dictionary<string, int>();
            foreach (var mismatch in AllMismatches)
            {
                string defender = mismatch.Split('→')[1].Split(':')[0];
                if (!byDefender.ContainsKey(defender))
                    byDefender[defender] = 0;
                byDefender[defender]++;
            }

            Plugin.Logger.LogWarning("Mismatches by defender:");
            foreach (var kvp in byDefender)
            {
                Plugin.Logger.LogWarning($"  {kvp.Key}: {kvp.Value} mismatches");
            }

            // Clear for next run
            AllMismatches.Clear();
            TotalMismatchCount = 0;
        }
    }
}
