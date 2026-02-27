// AI Dev Comment: Data structures for unit tag-based damage modifiers system.
// Allows user-defined tags (e.g., "Polearm", "Assassin") to apply multipliers or flat damage bonuses
// to specific attacker-defender relationships. Used to handle original game's hardcoded special cases
// (e.g., Spearman vs Ladderman 5x bonus, Assassin instant-kill vs Lord).
//
// ADDITIVE STACKING SYSTEM (refactored from first-match):
// - All matching tag interactions contribute deltas that SUM together
// - Formula: FinalMultiplier = 1.0 + Σ(interaction.Multiplier - 1.0)
// - Example: Knight has tags [Knight, HeavyArmor]. If Bolt_vs_HeavyArmor=2.20 and Bolt_vs_Knight=0.30,
//   then total delta = (2.20-1.0) + (0.30-1.0) = 1.20 + (-0.70) = 0.50, final = 1.50
// - Benefits: Tag order is irrelevant (commutative), no priority bugs, cleaner mental model
// - FALLBACK: See UnitTagInteraction.cs.bak for the original first-match system

using System;
using System.Collections.Generic;
using System.Linq;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Defines a damage modifier relationship between units with specific tags.
    /// Applied after base damage formula: FinalDamage = (BaseDamage × ArmorMult × TagMult) + TagFlat
    /// </summary>
    internal class UnitTagInteraction
    {
        /// <summary>
        /// Unique identifier for this interaction (e.g., "Polearm_vs_Ladderman")
        /// </summary>
        public string InteractionId { get; set; }

        /// <summary>
        /// Tags that attackers must have for this interaction to apply (OR logic)
        /// </summary>
        public List<string> AttackerTags { get; set; } = new List<string>();

        /// <summary>
        /// Tags that defenders must have for this interaction to apply (OR logic)
        /// </summary>
        public List<string> DefenderTags { get; set; } = new List<string>();

        /// <summary>
        /// Damage delta applied after armor calculation.
        /// Default: 0.0 (no change). Example: +4.0 = 5x damage, -0.7 = 30% damage
        /// </summary>
        public float Delta { get; set; } = 0.0f;

        /// <summary>
        /// Flat damage bonus/penalty applied after multiplier.
        /// Default: 0. Example: 5000 = +5000 damage, -1000 = -1000 damage
        /// </summary>
        public int FlatBonus { get; set; }

        public UnitTagInteraction()
        {
            Delta = 0.0f;
            FlatBonus = 0;
        }

        /// <summary>
        /// Apply this tag interaction to a base damage value.
        /// Formula: (baseDamage * (1.0 + Delta)) + FlatBonus
        /// </summary>
        public int ApplyTo(int baseDamage)
        {
            int result = (int)Math.Round(baseDamage * (1.0f + Delta)) + FlatBonus;
            return Math.Max(1, result); // Minimum 1 damage
        }
    }

    /// <summary>
    /// Registry of unit tags and their interactions.
    /// Tags are user-defined strings (e.g., "Polearm", "Assassin") that group units.
    /// Interactions define multipliers/bonuses between attacker and defender tags.
    /// </summary>
    internal static class UnitTagRegistry
    {
        // Tag name → List of units with that tag
        private static readonly Dictionary<string, HashSet<SHCDESE.Interop.eChimps>> UnitsByTag
            = new Dictionary<string, HashSet<SHCDESE.Interop.eChimps>>();

        // Reverse lookup: Unit → List of tags (for O(1) lookup in TryGetInteraction)
        private static readonly Dictionary<SHCDESE.Interop.eChimps, List<string>> TagsByUnit
            = new Dictionary<SHCDESE.Interop.eChimps, List<string>>();

        // Interaction lookup: Flat list for Phase 3 mathematical compression
        private static readonly List<UnitTagInteraction> InteractionList = new List<UnitTagInteraction>();

        /// <summary>
        /// Register a tag and its associated units.
        /// </summary>
        public static void RegisterTag(string tagName, IEnumerable<SHCDESE.Interop.eChimps> units)
        {
            if (!UnitsByTag.ContainsKey(tagName))
                UnitsByTag[tagName] = new HashSet<SHCDESE.Interop.eChimps>();

            foreach (var unit in units)
            {
                UnitsByTag[tagName].Add(unit);
                AddToReverseIndex(unit, tagName);
            }
        }

        /// <summary>
        /// Register a single unit to a tag.
        /// Used when tags are defined as unit properties rather than in tag lists.
        /// </summary>
        public static void RegisterUnitTag(SHCDESE.Interop.eChimps unit, string tagName)
        {
            if (!UnitsByTag.ContainsKey(tagName))
                UnitsByTag[tagName] = new HashSet<SHCDESE.Interop.eChimps>();

            UnitsByTag[tagName].Add(unit);
            AddToReverseIndex(unit, tagName);
        }

        /// <summary>
        /// Add a unit-tag mapping to the reverse index for O(1) lookup.
        /// </summary>
        private static void AddToReverseIndex(SHCDESE.Interop.eChimps unit, string tagName)
        {
            if (!TagsByUnit.TryGetValue(unit, out var tags))
            {
                tags = new List<string>();
                TagsByUnit[unit] = tags;
            }
            if (!tags.Contains(tagName))
                tags.Add(tagName);
        }

        /// <summary>
        /// Register a tag interaction.
        /// </summary>
        public static void RegisterInteraction(UnitTagInteraction interaction)
        {
            InteractionList.Add(interaction);
        }

        /// <summary>
        /// Get all tags for a unit. Uses O(1) reverse index lookup.
        /// </summary>
        public static List<string> GetTagsForUnit(SHCDESE.Interop.eChimps unit)
        {
            // Use reverse index for O(1) lookup instead of O(tags) iteration
            if (TagsByUnit.TryGetValue(unit, out var tags))
                return new List<string>(tags); // Return a copy to prevent modification
            return new List<string>();
        }

        /// <summary>
        /// Try to find tag interactions between attacker and defender.
        /// Uses ADDITIVE STACKING: All matching interactions contribute deltas that sum together.
        /// Formula: FinalMultiplier = 1.0 + Σ(interaction.Multiplier - 1.0)
        /// This eliminates tag ordering dependencies - order is now irrelevant (commutative).
        /// Returns a synthetic interaction with the stacked result, or null if no matches.
        /// </summary>
        public static UnitTagInteraction TryGetInteraction(SHCDESE.Interop.eChimps attacker, SHCDESE.Interop.eChimps defender)
        {
            var attackerTags = GetTagsForUnit(attacker);
            var defenderTags = GetTagsForUnit(defender);

            // Stack all matching deltas (additive system)
            float totalDelta = 0.0f;
            int totalFlatBonus = 0;
            int matchCount = 0;
            string lastId = null;

            foreach (var interaction in InteractionList)
            {
                // Check if any attacker tag matches AND any defender tag matches
                bool attackerMatches = interaction.AttackerTags.Any(t => attackerTags.Contains(t));
                bool defenderMatches = interaction.DefenderTags.Any(t => defenderTags.Contains(t));

                if (attackerMatches && defenderMatches)
                {
                    totalDelta += interaction.Delta;
                    totalFlatBonus += interaction.FlatBonus;
                    matchCount++;
                    lastId = interaction.InteractionId;
                }
            }

            if (matchCount == 0)
                return null;

            // Return synthetic interaction with stacked result
            return new UnitTagInteraction
            {
                InteractionId = matchCount == 1 ? lastId : $"Stacked({matchCount})",
                AttackerTags = new List<string> { "Stacked" },
                DefenderTags = new List<string> { "Stacked" },
                Delta = totalDelta,  // Stacked deltas
                FlatBonus = totalFlatBonus
            };
        }

        /// <summary>
        /// Clear all registered tags and interactions.
        /// Used when reloading configuration.
        /// </summary>
        public static void Clear()
        {
            UnitsByTag.Clear();
            TagsByUnit.Clear();
            InteractionList.Clear();
        }

        /// <summary>
        /// Clear only interactions, preserving tags.
        /// </summary>
        public static void ClearInteractions()
        {
            InteractionList.Clear();
        }

        /// <summary>
        /// Try to find tag interactions for a projectile attacking a defender.
        /// Uses ADDITIVE STACKING: All matching interactions contribute deltas that sum together.
        /// Formula: FinalMultiplier = 1.0 + Σ(interaction.Multiplier - 1.0)
        /// Uses projectile name as the attacker tag (e.g., "Arrow", "Bolt", "Slinger", "Javelin").
        /// Returns a synthetic interaction with the stacked result, or null if no matches.
        /// </summary>
        public static UnitTagInteraction TryGetProjectileInteraction(ProjectileType projectile, SHCDESE.Interop.eChimps defender)
        {
            // Get the projectile name as the attacker tag
            string projectileTag = projectile.ToString();
            
            // Check if there are any interactions defined for this projectile
            if (!InteractionList.Any(i => i.AttackerTags.Contains(projectileTag)))
                return null;

            // Get all tags for the defender
            var defenderTags = GetTagsForUnit(defender);

            // Stack all matching deltas (additive system)
            float totalDelta = 0.0f;
            int totalFlatBonus = 0;
            int matchCount = 0;
            string lastId = null;

            foreach (var interaction in InteractionList)
            {
                // Projectile acts as a single attacker tag
                bool attackerMatches = interaction.AttackerTags.Contains(projectileTag);
                bool defenderMatches = interaction.DefenderTags.Any(t => defenderTags.Contains(t));

                if (attackerMatches && defenderMatches)
                {
                    totalDelta += interaction.Delta;
                    totalFlatBonus += interaction.FlatBonus;
                    matchCount++;
                    lastId = interaction.InteractionId;
                }
            }

            if (matchCount == 0)
                return null;

            // Return synthetic interaction with stacked result
            return new UnitTagInteraction
            {
                InteractionId = matchCount == 1 ? lastId : $"Stacked({matchCount})",
                AttackerTags = new List<string> { projectileTag },
                DefenderTags = new List<string> { "Stacked" },
                Delta = totalDelta,  // Stacked deltas
                FlatBonus = totalFlatBonus
            };
        }

        /// <summary>
        /// Get all registered tag names.
        /// </summary>
        public static IEnumerable<string> GetAllTags()
        {
            return UnitsByTag.Keys;
        }

        /// <summary>
        /// Get all units with a specific tag.
        /// </summary>
        public static IEnumerable<SHCDESE.Interop.eChimps> GetUnitsWithTag(string tagName)
        {
            if (UnitsByTag.TryGetValue(tagName, out var units))
                return units;
            return new HashSet<SHCDESE.Interop.eChimps>();
        }
    }
}
