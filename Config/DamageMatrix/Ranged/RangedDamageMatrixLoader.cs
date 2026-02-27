// Config/DamageMatrix/Ranged/RangedDamageMatrixLoader.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.BepInEx;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.DamageMatrix.Ranged
{
    /// <summary>
    /// Loads and applies the ranged damage matrix from CSV file.
    /// Matrix format: Rows = Defenders, Columns = Projectile types (Arrow, Bolt, Slinger, Javelin)
    /// </summary>
    internal class RangedDamageMatrixLoader : MatrixLoader<ProjectileType, eChimps>
    {
        protected override string FilePath => MatrixPaths.RangedDamage;

        /// <summary>
        /// Post-load validation: Log damage anomalies by comparing current game state
        /// against original CSV defaults.
        /// </summary>
        protected override void OnLoadComplete(int appliedCount, int skippedCount)
        {
            base.OnLoadComplete(appliedCount, skippedCount);
            LogDamageAnomalies();
        }

        /// <summary>
        /// Parse projectile type headers from CSV columns.
        /// </summary>
        protected override Nullable<ProjectileType>[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<ProjectileType>(headers);
        }

        /// <summary>
        /// Parse defender unit types from CSV rows.
        /// </summary>
        protected override Nullable<eChimps>[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        /// <summary>
        /// Apply a ranged damage value to the game (from CSV override).
        /// Applies the BepInEx ranged damage multiplier if configured.
        /// </summary>
        protected override bool ApplyDamageValue(ProjectileType projectile, eChimps defender, int damage)
        {
            // Note: Skip checks are now done in base class Load() method before calling this
            // This method assumes valid, modifiable entities
            
            // Apply BepInEx ranged damage multiplier
            var unitMultipliers = BepInExConfigManager.UnitMultipliers;
            if (unitMultipliers?.RangedDamageTakenMultiplier != null && 
                !Mathf.Approximately(unitMultipliers.RangedDamageTakenMultiplier.Value, 1.0f))
            {
                damage = (int)(damage * unitMultipliers.RangedDamageTakenMultiplier.Value);
            }

            if (!ProjectileApiHelper.SetRangedDamage(projectile, defender, damage))
            {
                Plugin.Logger.LogWarning($"Failed to set {projectile} damage for {defender}");
                return false;
            }


            return true;
        }


        /// <summary>
        /// Check if a defender should be skipped.
        /// </summary>
        protected override bool ShouldSkipDefender(eChimps defender)
        {
            return UnitMatrixHelper.ShouldSkipUnit(defender);
        }

        /// <summary>
        /// Get the ORIGINAL DEFAULT ranged damage value (from game before any mods).
        /// This is used to determine if CSV value was modified by user.
        /// </summary>
        protected override int GetOriginalDefaultValue(ProjectileType projectile, eChimps defender)
        {
            return Core.CsvMatrixReader.GetOriginalRangedDamage(projectile, defender);
        }

        /// <summary>
        /// Log damage anomalies by comparing current game state against original CSV defaults.
        ///
        /// This helps identify:
        /// - Units taking more/less ranged damage than the original CSV default
        /// - Effects of ranged armor multiplier changes from TOML config
        /// - Unexpected damage calculation issues
        ///
        /// Anomaly threshold: >10% deviation from original default
        /// </summary>
        private void LogDamageAnomalies()
        {
            Plugin.Logger.LogInfo("Analyzing ranged damage anomalies (current vs original CSV)...");

            var anomalies = new List<string>();
            int totalComparisons = 0;

            foreach (ProjectileType projectile in Enum.GetValues(typeof(ProjectileType)))
            {
                foreach (eChimps defender in Enum.GetValues(typeof(eChimps)))
                {
                    if (UnitMatrixHelper.ShouldSkipUnit(defender))
                        continue;

                    // Get original CSV default (unmodified game value)
                    int originalDamage = Core.CsvMatrixReader.GetOriginalRangedDamage(projectile, defender);
                    if (originalDamage < 0)
                        continue; // No original default available

                    // Skip non-combatant damage floor
                    if (originalDamage <= 2)
                        continue;

                    // Get current game damage (after TOML armor multipliers applied)
                    int currentDamage;
                    try
                    {
                        currentDamage = ProjectileApiHelper.GetRangedDamage(projectile, defender);
                    }
                    catch
                    {
                        continue;
                    }

                    totalComparisons++;

                    // Calculate deviation
                    float deviation = Math.Abs(currentDamage - originalDamage);
                    float deviationPercent = (deviation / originalDamage) * 100f;

                    // Anomaly threshold: >10% deviation
                    if (deviationPercent > 10f)
                    {
                        anomalies.Add(
                            $"{projectile} → {defender}: " +
                            $"Original={originalDamage}, Current={currentDamage}, " +
                            $"Diff={currentDamage - originalDamage:+0;-0}, " +
                            $"Deviation={deviationPercent:F1}%"
                        );
                    }
                }
            }

            Plugin.Logger.LogInfo($"Ranged damage anomaly analysis complete: {anomalies.Count} anomalies found out of {totalComparisons} comparisons");

            if (anomalies.Count > 0)
            {
                int maxLogs = Math.Min(50, anomalies.Count);
                Plugin.Logger.LogWarning($"Showing first {maxLogs} of {anomalies.Count} ranged damage anomalies:");

                for (int i = 0; i < maxLogs; i++)
                {
                    Plugin.Logger.LogWarning($"  [{i + 1}] {anomalies[i]}");
                }

                if (anomalies.Count > maxLogs)
                {
                    Plugin.Logger.LogWarning($"  ... and {anomalies.Count - maxLogs} more anomalies (suppressed to prevent log spam)");
                }
            }
        }
    }
}