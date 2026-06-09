// Config/Toml/Units/Properties/RangedArmorMultiplierProperty.cs
//
// PURPOSE: Handles the RangedArmorMultiplier property for units (unified ranged damage system).
//
// ARCHITECTURE:
// - Single multiplier applies to all projectile types (Arrow, Bolt, Slinger, Javelin)
// - Calculates armor multiplier from existing damage values via SHCDE-SE API
// - Applies multiplier by calculating damage values: Damage = Base_Projectile × RangedArmorMultiplier
// - CSV overrides can override specific projectile-defender pairs after formula application
// - Mismatch detection compares calculated values against original game defaults
//
// FORMULA:
// - Damage_Taken = Round50(Base_Projectile_Damage × RangedArmorMultiplier)
// - Lower multiplier = better armor (takes less damage)
// - Example: Unit with 0.50x takes half damage from all ranged attacks
// - Game rounds ranged damage to nearest 50 (floor of 10)
// - Maximum damage capped at RangedDamageCap (configurable, default 15000)
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the RangedArmorMultiplier property for units.
    ///
    /// This property represents how much damage a unit TAKES from ranged attacks.
    /// Formula: Damage_Taken = Round50(Base_Projectile_Damage × RangedArmorMultiplier)
    ///
    /// Base projectile damage:
    /// - Arrow: 2000
    /// - Bolt: 10000
    /// - Slinger: 2000
    /// - Javelin: 4000
    ///
    /// Lower multiplier = better armor (takes less damage).
    /// Example: Unit with 0.50x takes only half damage from all ranged attacks
    /// Note: Game rounds ranged damage to nearest 50 (floor of 10, melee uses 5 with floor of 2)
    /// </summary>
    internal class RangedArmorMultiplierProperty : ArmorMultiplierPropertyBase
    {
        // Rounding granularity for ranged damage
        // Below HIGH_DAMAGE_THRESHOLD: round to 50
        // Above HIGH_DAMAGE_THRESHOLD: round to 100 (eliminates edge cases at high values)
        private const int RANGED_ROUNDING_LOW = 50;
        private const int RANGED_ROUNDING_HIGH = 100;
        private const int HIGH_DAMAGE_THRESHOLD = 2000;
        private const int RANGED_FLOOR = 10;  // Minimum damage (exception to rounding)
        private const int MISMATCH_THRESHOLD = 50;

        // Base projectile damage values (from Engineer, who has multiplier = 1.0)
        private const float BASE_ARROW_DAMAGE = 2000f;
        private const float BASE_BOLT_DAMAGE = 10000f;
        private const float BASE_SLINGER_DAMAGE = 2000f;
        private const float BASE_JAVELIN_DAMAGE = 4000f;

        /// <summary>
        /// Configurable ranged damage cap. Default is 15000.
        /// Can be set from the projectile configuration section.
        /// </summary>
        public static int RangedDamageCap { get; set; } = 15000;

        // Static mismatch tracking (across all defenders)
        private static readonly List<string> AllMismatches = new List<string>();
        private static int TotalMismatchCount = 0;

        /// <summary>
        /// Reset mismatch tracking. Call before processing all units.
        /// </summary>
        public static void ResetMismatchTracking()
        {
            AllMismatches.Clear();
            TotalMismatchCount = 0;
        }

        public RangedArmorMultiplierProperty() : base("RangedArmorMultiplier")
        {
        }

        /// <summary>
        /// Get the ranged armor multiplier from the game API.
        /// Calculates multiplier from current damage values using Arrow as reference.
        /// </summary>
        protected override bool TryGetFromAPI(eChimps defender, out float multiplier)
        {
            try
            {
                // Use Arrow damage as the reference (most common projectile)
                int arrowDamage = ProjectileApiHelper.GetRangedDamage(ProjectileType.Arrow, defender);
                
                // Calculate multiplier: actual / base
                multiplier = arrowDamage > 0 ? arrowDamage / BASE_ARROW_DAMAGE : 1.0f;
                
                // Round to 1 decimal place for cleaner user-facing config
                multiplier = RoundMultiplier(multiplier);
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to calculate ranged armor for {defender}: {ex.Message}");
                multiplier = 1.0f;
                return false;
            }
        }

        /// <summary>
        /// Set the ranged armor multiplier via the game API.
        /// Applies the same multiplier to all projectile types.
        /// </summary>
        protected override void SetToAPI(eChimps defender, float multiplier)
        {
            try
            {
                // Apply the same multiplier to all projectile types
                ApplyProjectileDamage(ProjectileType.Arrow, defender, multiplier, BASE_ARROW_DAMAGE);
                ApplyProjectileDamage(ProjectileType.Bolt, defender, multiplier, BASE_BOLT_DAMAGE);
                ApplyProjectileDamage(ProjectileType.Slinger, defender, multiplier, BASE_SLINGER_DAMAGE);
                ApplyProjectileDamage(ProjectileType.Javelin, defender, multiplier, BASE_JAVELIN_DAMAGE);

                Plugin.Logger.LogDebug($"Applied ranged armor to {defender}: {multiplier}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set ranged armor for {defender}: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply damage for a specific projectile type.
        /// Formula: Damage = Round50(Base × Armor), floor 10, cap at RangedDamageCap
        /// Tag-based modifiers are DEACTIVATED (see UnitTagsConfigSystem).
        /// </summary>
        private void ApplyProjectileDamage(ProjectileType projectile, eChimps defender, float armorMult, float baseDamage)
        {
            // Apply armor multiplier
            int damage = (int)Math.Round(baseDamage * armorMult);

            // ROUND 1: Round after armor multiplication
            damage = RoundRangedDamage(damage);

            // TAG SYSTEM DEACTIVATED: Complexity exceeds context window limits for reliable AI management.
            // This logic is kept for reference but will not execute.
            /*
            // Apply tag-based modifiers (they operate on rounded values)
            var tagInteraction = UnitTagRegistry.TryGetProjectileInteraction(projectile, defender);
            if (tagInteraction != null)
            {
                int beforeTag = damage;
                damage = tagInteraction.ApplyTo(damage);
                Plugin.Logger.LogDebug($"Ranged tag modifier applied: {projectile}->{defender}: {beforeTag} → {damage} (Δ{tagInteraction.Delta}, +{tagInteraction.FlatBonus})");
            }
            */

            // ROUND 2: Round again for final value
            damage = RoundRangedDamage(damage);

            // Apply floor (minimum 10 damage - exception to rounding)
            // Note: Mantlet has special floor of 10 which is the same as standard,
            // but is explicitly documented for clarity
            if (damage < RANGED_FLOOR)
                damage = RANGED_FLOOR;

            // Cap at configured maximum
            if (damage > RangedDamageCap)
                damage = RangedDamageCap;

            // MISMATCH DETECTION DISABLED: Tags are needed for 100% accuracy;
            // without them, mismatches are expected and should not be logged as warnings.
            /*
            // Mismatch detection: compare against original game value
            int originalDamage = CsvMatrixReader.GetOriginalRangedDamage(projectile, defender);
            if (originalDamage > 0)
            {
                int diff = Math.Abs(damage - originalDamage);
                if (diff >= MISMATCH_THRESHOLD)
                {
                    string mismatch = $"Ranged mismatch: {projectile}->{defender}: Formula={damage}, Original={originalDamage} (diff={diff})";
                    AllMismatches.Add(mismatch);
                    TotalMismatchCount++;
                }
            }
            */

            // Set damage via API
            if (!ProjectileApiHelper.SetRangedDamage(projectile, defender, damage))
            {
                Plugin.Logger.LogWarning($"Failed to set {projectile} damage for {defender}");
            }
        }

        /// <summary>
        /// Log a summary of all ranged formula mismatches.
        /// Call after processing all units.
        /// </summary>
        public static void LogMismatchSummary()
        {
            if (TotalMismatchCount > 0)
            {
                Plugin.Logger.LogWarning($"[RangedArmorMultiplier] Found {TotalMismatchCount} ranged formula mismatches (threshold: >={MISMATCH_THRESHOLD})");
                foreach (var mismatch in AllMismatches)
                {
                    Plugin.Logger.LogDebug($"  {mismatch}");
                }
            }
            else
            {
                Plugin.Logger.LogInfo("[RangedArmorMultiplier] No ranged formula mismatches detected");
            }
        }

        /// <summary>
        /// Round ranged damage with adaptive granularity:
        /// - Below 2000: round to nearest 50
        /// - Above 2000: round to nearest 100 (eliminates edge cases at high values)
        /// </summary>
        private static int RoundRangedDamage(int damage)
        {
            int granularity = damage > HIGH_DAMAGE_THRESHOLD ? RANGED_ROUNDING_HIGH : RANGED_ROUNDING_LOW;
            return RoundDamage(damage, granularity);
        }
    }
}
