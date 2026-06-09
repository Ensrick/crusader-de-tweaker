// Config/Toml/UnitTags/UnitTagsConfigGenerator.Discovery.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.UnitTags
{
    internal static partial class UnitTagsConfigGenerator
    {
        private const float BASE_ARROW_DAMAGE = 2000f;
        private const float BASE_BOLT_DAMAGE = 10000f;
        private const float BASE_SLINGER_DAMAGE = 2000f;
        private const float BASE_JAVELIN_DAMAGE = 4000f;

        private const int RANGED_ROUNDING_LOW = 50;
        private const int RANGED_ROUNDING_HIGH = 100;
        private const int HIGH_DAMAGE_THRESHOLD = 2000;
        private const int RANGED_FLOOR = 10;
        private const int MELEE_ROUNDING = 5;
        private const int MELEE_FLOOR = 2;

        private static List<RawInteraction> DiscoverInteractions()
        {
            var discovered = new List<RawInteraction>();
            var allUnits = Enum.GetValues(typeof(eChimps)).Cast<eChimps>().Where(u => !UnitCategories.IsNonModifiable(u)).ToList();

            DiscoverRangedInteractions(discovered, allUnits);
            DiscoverMeleeInteractions(discovered, allUnits);

            return discovered;
        }

        private static void DiscoverRangedInteractions(List<RawInteraction> discovered, List<eChimps> allUnits)
        {
            foreach (var defender in allUnits)
            {
                string defenderSemantic = GetPrimarySemanticTag(defender);
                string defenderUnit = defender.ToString();

                // Calculate armor multiplier using Arrow as reference (same as RangedArmorMultiplierProperty)
                int arrowDamage = ProjectileApiHelper.GetRangedDamage(ProjectileType.Arrow, defender);
                
                // DEBUG LOGGING
                if (defender == eChimps.CHIMP_TYPE_PEASANT || defender == eChimps.CHIMP_TYPE_ARCHER)
                {
                    Plugin.Logger.LogInfo($"[Discovery] {defender}: ArrowDamage={arrowDamage} (Base={BASE_ARROW_DAMAGE})");
                }

                if (arrowDamage <= 0) continue;

                float armorMult = arrowDamage / BASE_ARROW_DAMAGE;
                
                // Round to 1 decimal place like the property does
                armorMult = (float)Math.Round(armorMult, 1);

                foreach (var proj in new[] { ProjectileType.Arrow, ProjectileType.Bolt, ProjectileType.Slinger, ProjectileType.Javelin })
                {
                    float baseDamage = GetBaseProjectileDamage(proj);
                    int formulaDamage = (int)Math.Round(baseDamage * armorMult);
                    formulaDamage = RoundRangedDamage(formulaDamage);
                    
                    // Final formula damage after rounding
                    int finalFormula = RoundRangedDamage(formulaDamage);
                    if (finalFormula < RANGED_FLOOR) finalFormula = RANGED_FLOOR;

                    int originalDamage = CsvMatrixReader.GetOriginalRangedDamage(proj, defender);
                    
                    // DEBUG LOGGING for PEASANT
                    if (defender == eChimps.CHIMP_TYPE_PEASANT && proj == ProjectileType.Slinger)
                    {
                        Plugin.Logger.LogInfo($"[Discovery] Slinger->Peasant: Formula={finalFormula}, Original={originalDamage}, Diff={Math.Abs(finalFormula - originalDamage)}");
                    }

                    if (originalDamage > 0 && Math.Abs(finalFormula - originalDamage) >= 50)
                    {
                        // Mismatch found!
                        float requiredDelta = ((float)originalDamage / finalFormula) - 1.0f;
                        Plugin.Logger.LogInfo($"[Discovery] Adding Ranged Mismatch: {proj}_vs_{defenderUnit} (Tag:{defenderSemantic}) Delta={requiredDelta}"); // DEBUG TAG
                        discovered.Add(new RawInteraction(
                            proj.ToString(), 
                            defenderUnit, // Use Unit Name for precision
                            requiredDelta, 
                            50, 
                            "AUTO-DISCOVERED RANGED", 
                            $"{proj}_vs_{defenderSemantic}",
                            defenderSemantic // Store Semantic Tag for compression
                        ));
                    }
                }
            }
        }

        private static void DiscoverMeleeInteractions(List<RawInteraction> discovered, List<eChimps> allUnits)
        {
            var attackers = allUnits.Where(u => UnitCategories.IsRecruitable(u) || UnitCategories.SiegeEquipment.Contains(u)).ToList();

            foreach (var defender in allUnits)
            {
                string defenderSemantic = GetPrimarySemanticTag(defender);
                string defenderUnit = defender.ToString();

                // Calculate armor multiplier using Average method to match Property system
                float armorMult = GetDefenderArmorMultiplier(defender);

                // DEBUG LOGGING
                if (defender == eChimps.CHIMP_TYPE_PEASANT)
                {
                    Plugin.Logger.LogInfo($"[Discovery] {defender}: MeleeArmorMult={armorMult}");
                }

                foreach (var attacker in attackers)
                {
                    if (UnitCategories.IsNonMeleeAttacker(attacker)) continue;

                    int baseDamage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, eChimps.CHIMP_TYPE_ENGINEER);
                    if (baseDamage <= 2) continue;

                    int formulaDamage = (int)Math.Round(baseDamage * armorMult);
                    formulaDamage = RoundDamageInternal(formulaDamage, MELEE_ROUNDING);
                    
                    int finalFormula = RoundDamageInternal(formulaDamage, MELEE_ROUNDING);
                    if (finalFormula < MELEE_FLOOR) finalFormula = MELEE_FLOOR;

                    int originalDamage = CsvMatrixReader.GetOriginalMeleeDamage(attacker, defender);
                    if (originalDamage >= 0 && Math.Abs(finalFormula - originalDamage) >= 5)
                    {
                        float requiredDelta = ((float)originalDamage / finalFormula) - 1.0f;
                        string attackerTag = GetPrimarySemanticTag(attacker);
                        discovered.Add(new RawInteraction(
                            attackerTag, 
                            defenderUnit, // Use Unit Name for precision
                            requiredDelta, 
                            5, 
                            "AUTO-DISCOVERED MELEE", 
                            $"{attackerTag}_vs_{defenderSemantic}",
                            defenderSemantic // Store Semantic Tag for compression
                        ));
                    }
                }
            }
        }

        private static float GetDefenderArmorMultiplier(eChimps defender)
        {
            // Replicate logic from MeleeArmorMultiplierProperty:
            // Calculate average multiplier by examining ratios of (Damage vs Defender) / (Damage vs Engineer) across all attackers.
            // This ensures Discovery uses the exact same armor coefficient that the Property system will derive.
            
            var multipliers = new List<float>();
            var attackers = Enum.GetValues(typeof(eChimps)).Cast<eChimps>();
            eChimps referenceUnit = eChimps.CHIMP_TYPE_ENGINEER; // Standard base reference

            foreach (var attacker in attackers)
            {
                // Skip basic checks if needed, but the >2 check handles most non-combatants
                // We rely on UnitApi directly to be safe
                int baseDamage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, referenceUnit);
                int currentDamage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);

                // Check for valid base damage (>2 to ignore floor) and current damage
                if (baseDamage > 2 && currentDamage > 0)
                {
                    float mult = (float)currentDamage / baseDamage;
                    multipliers.Add(mult);
                }
            }

            if (multipliers.Count == 0) return 1.0f;

            float avg = multipliers.Average();
            return (float)Math.Round(avg, 1);
        }

        private static string GetPrimarySemanticTag(eChimps unit)
        {
            var tags = UnitTagRegistry.GetTagsForUnit(unit);
            return tags.FirstOrDefault() ?? unit.ToString();
        }

        private static float GetBaseProjectileDamage(ProjectileType proj)
        {
            switch (proj)
            {
                case ProjectileType.Arrow: return BASE_ARROW_DAMAGE;
                case ProjectileType.Bolt: return BASE_BOLT_DAMAGE;
                case ProjectileType.Slinger: return BASE_SLINGER_DAMAGE;
                case ProjectileType.Javelin: return BASE_JAVELIN_DAMAGE;
                default: return 0;
            }
        }

        private static List<EquivalenceClass> DetectEquivalenceClasses(List<RawInteraction> raw)
        {
            Plugin.Logger.LogInfo($"[Discovery] Grouping {raw.Count} raw interactions via Semantic Compression...");

            // 1. Calculate Tag Population (How many valid units exist for each Semantic Tag?)
            var allUnits = Enum.GetValues(typeof(eChimps)).Cast<eChimps>()
                .Where(u => !UnitCategories.IsNonModifiable(u)).ToList();

            var tagPopulation = new Dictionary<string, int>();
            foreach (var unit in allUnits)
            {
                string tag = GetPrimarySemanticTag(unit);
                if (!tagPopulation.ContainsKey(tag)) tagPopulation[tag] = 0;
                tagPopulation[tag]++;
            }

            // 2. Group by (SemanticDefenderTag, RoundedDelta) to identify candidates for Semantic Rules
            // Note: We use SemanticDefenderTag here as the primary grouping key
            var groups = raw
                .GroupBy(i => new { i.SemanticDefenderTag, RoundedDelta = RoundDelta(i.Delta, i.Rounding) })
                .ToList();
            
            var finalClasses = new List<EquivalenceClass>();

            foreach (var g in groups)
            {
                string semanticTag = g.Key.SemanticDefenderTag;
                float delta = g.Key.RoundedDelta;

                // 3. Check Consistency: Does this group apply to ALL units of the Semantic Tag?
                // Count unique UnitNames (DefenderTags) in this group
                int uniqueDefendersInGroup = g.Select(i => i.DefenderTag).Distinct().Count();
                int totalPopulation = tagPopulation.ContainsKey(semanticTag) ? tagPopulation[semanticTag] : 999;

                // Compression Condition: Every unit with this Semantic Tag must be present in this Delta Group.
                // If units are missing, they likely had different Deltas (or 0), so we cannot generalize to the Semantic Tag.
                // Relaxed condition: If uniqueDefendersInGroup is large enough (e.g. > 1), we might prefer semantic rule anyway, 
                // but strictly correct logic is ALL. Let's try Strict first.
                // Actually, often we have tags with just 1 unit (like Lord), so population check works there too.
                
                if (uniqueDefendersInGroup >= totalPopulation)
                {
                    // Success: Emit SEMANTIC Rule
                    var eq = new EquivalenceClass(semanticTag, delta);
                    eq.AttackerTags = g.Select(i => i.AttackerTag).Distinct().OrderBy(t => t).ToList();
                    eq.InternalId = GenerateInternalId(string.Join(",", eq.AttackerTags), semanticTag, delta);
                    eq.Group = g.First().Group;
                    
                    string attackerDesc = eq.AttackerTags.Count > 1 ? "MultipleAttackers" : eq.AttackerTags[0];
                    eq.SuggestedId = $"{attackerDesc}_vs_{semanticTag}";
                    finalClasses.Add(eq);
                }
                else
                {
                    // Fail: Inconsistent behavior within Semantic Tag.
                    // Fallback: Emit UNIT SPECIFIC Rules
                    // Group by the actual Defender Unit Name (DefenderTag)
                    var splitGroups = g.GroupBy(i => i.DefenderTag);
                    
                    foreach (var split in splitGroups)
                    {
                         var eq = new EquivalenceClass(split.Key, delta); // Use Unit Name
                         eq.AttackerTags = split.Select(i => i.AttackerTag).Distinct().OrderBy(t => t).ToList();
                         eq.InternalId = GenerateInternalId(string.Join(",", eq.AttackerTags), split.Key, delta);
                         eq.Group = split.First().Group;

                         string attackerDesc = eq.AttackerTags.Count > 1 ? "MultipleAttackers" : eq.AttackerTags[0];
                         eq.SuggestedId = $"{attackerDesc}_vs_{split.Key}";
                         finalClasses.Add(eq);
                    }
                }
            }

            Plugin.Logger.LogInfo($"[Discovery] Compressed into {finalClasses.Count} equivalence classes.");
            return finalClasses;
        }

        private static float RoundDelta(float delta, int rounding)
        {
            // For ranged (50), 0.01 precision is plenty. For melee (5), 0.001.
            int decimals = rounding >= 50 ? 2 : 3;
            return (float)Math.Round(delta, decimals);
        }

        private static int RoundRangedDamage(int damage)
        {
            int granularity = damage > HIGH_DAMAGE_THRESHOLD ? RANGED_ROUNDING_HIGH : RANGED_ROUNDING_LOW;
            return RoundDamageInternal(damage, granularity);
        }

        private static int RoundDamageInternal(int damage, int granularity)
        {
            if (granularity <= 0) return damage;
            return (int)Math.Round((double)damage / granularity) * granularity;
        }
    }
}
