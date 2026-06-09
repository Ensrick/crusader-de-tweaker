// Config/DamageMatrix/Melee/MeleeDamageMatrixGenerator.cs
//
// PURPOSE: Generates the melee damage CSV matrix file from game API data.
//
// CRITICAL GAME MECHANIC - DAMAGE FLOOR:
// The game has a minimum damage floor of 2 for all units. Units incapable of dealing
// melee damage (workers, non-combatants, etc.) still return 2 from the game API.
// This is NOT actual damage capability - it's just the game's minimum floor value.
//
// FILTERING LOGIC:
// This generator filters out "attackers" that deal only 2 damage to all defenders.
// These units are incapable of actual melee combat and only show 2 due to the damage floor.
// Excluding them from the CSV reduces file size and focuses on actual combat units.
//
// WHY 2 IS THE MAGIC NUMBER:
// - Real combat units deal damage > 2 (e.g., Spearman deals 20-100+ damage)
// - Non-combat units (workers, etc.) return 2 from API (the minimum floor)
// - The floor of 2 ensures minimum damage in edge cases, but indicates no real combat ability
//
// IMPORTANT FOR AI AGENTS:
// - Don't assume units dealing 2 damage are "weak attackers" - they're non-combatants
// - The 2-damage floor is a game constant, not a balance value
// - Filtering on "damage > 2" effectively separates combatants from non-combatants
//
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Melee
{
    /// <summary>
    /// Generates the melee damage matrix CSV file.
    /// Matrix format: Rows = Defenders, Columns = Attackers
    /// 
    /// <para>
    /// <b>DAMAGE FLOOR FILTERING:</b><br/>
    /// The game has a minimum damage floor of 2. Units incapable of dealing melee damage
    /// (workers, non-combatants, animals, etc.) return 2 from the API due to this floor.
    /// </para>
    /// 
    /// <para>
    /// This generator excludes attackers that deal only 2 damage to all defenders, as these
    /// units are incapable of actual melee combat. The value 2 is the game's damage floor,
    /// not an indication of combat capability.
    /// </para>
    /// 
    /// <para>
    /// Real combat units deal damage significantly higher than 2 (typically 10-150+).
    /// Filtering on "damage > 2" effectively separates combatants from non-combatants.
    /// </para>
    /// </summary>
    internal class MeleeDamageMatrixGenerator : MatrixGenerator<eChimps, eChimps>
    {
        protected override string FilePath => MatrixPaths.MeleeDamage;

        /// <summary>
        /// Get all units that can attack in melee (columns).
        /// 
        /// <para>
        /// <b>FILTERING LOGIC:</b><br/>
        /// Excludes units that deal only 2 damage to all defenders. These units are incapable
        /// of actual melee combat - the value 2 is the game's damage floor for non-combatants.
        /// </para>
        /// 
        /// <para>
        /// Units excluded are typically: workers, non-combatant units, passive animals, etc.<br/>
        /// Real combat units deal damage > 2 (typically 10-150+ damage).
        /// </para>
        /// </summary>
        protected override eChimps[] GetAttackers()
        {
            var allUnits = UnitMatrixHelper.GetModifiableUnits();
            var defenders = GetDefenders();
            
            // Filter out non-combatants that only deal the damage floor of 2
            // These units are incapable of actual melee damage - they only return 2 due to the game's minimum floor
            var validAttackers = new List<eChimps>();
            
            foreach (var attacker in allUnits)
            {
                // Skip units that don't attack in melee (e.g., Arab Ballista)
                if (UnitCategories.IsNonMeleeAttacker(attacker))
                {
                    Plugin.Logger.LogDebug($"Excluding {attacker} from attackers (non-melee unit - no melee attack)");
                    continue;
                }

                bool hasActualCombatCapability = false;
                
                // Check if this attacker deals more than the damage floor (2) to any defender
                // If damage > 2 to ANY defender, this unit has actual combat capability
                foreach (var defender in defenders)
                {
                    try
                    {
                        int damage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);
                        
                        // DEV NOTE: 2 is the game's damage floor for units with no combat capability
                        // Real combatants deal damage significantly higher than this (10-150+)
                        if (damage > 2)
                        {
                            hasActualCombatCapability = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore API errors, continue checking other defenders
                    }
                }
                
                if (hasActualCombatCapability)
                {
                    validAttackers.Add(attacker);
                }
                else
                {
                    // DEV NOTE: This unit deals only 2 damage (the floor) to all defenders
                    // It's a non-combatant (worker, passive unit, etc.) with no melee capability
                    Plugin.Logger.LogDebug($"Excluding {attacker} from attackers (non-combatant - only deals damage floor of 2)");
                }
            }
            
            Plugin.Logger.LogInfo($"Filtered melee attackers: {validAttackers.Count}/{allUnits.Length} units are combatants (deal damage > floor of 2)");
            return validAttackers.ToArray();
        }

        /// <summary>
        /// Get all units that can be attacked (rows).
        /// </summary>
        protected override eChimps[] GetDefenders()
        {
            return UnitMatrixHelper.GetModifiableUnits();
        }

        /// <summary>
        /// Get the melee damage value from attacker to defender.
        /// Returns -1 if the damage cannot be determined.
        /// </summary>
        protected override int GetDamageValue(eChimps attacker, eChimps defender)
        {
            try
            {
                int damage = Plugin.UnitApi.GetMeleeDamageFromTo(attacker, defender);
                return damage;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"No melee damage data for {attacker} -> {defender}: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Validate that the damage value is reasonable.
        /// </summary>
        protected override bool ValidateDamageValue(int damage)
        {
            // Allow any value including -1 for invalid pairs
            return true;
        }
    }
}