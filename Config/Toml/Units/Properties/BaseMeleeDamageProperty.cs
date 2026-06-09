// Config/Toml/Units/Properties/BaseMeleeDamageProperty.cs
using System;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the BaseMeleeDamage property for units.
    /// 
    /// Base damage is determined by the damage this unit deals TO the Engineer unit,
    /// since the Engineer has 1.0 armor from all sources and serves as the reference target.
    /// 
    /// PURPOSE:
    /// - Provides a reference value representing the unit's base melee damage output
    /// - Uses Engineer as reference target (1.0 armor = no damage reduction)
    /// - Setting this value updates the damage dealt to Engineer
    /// 
    /// IMPORTANT FOR AI AGENTS:
    /// - Engineer is used as the reference because it has neutral (1.0) armor
    /// - Damage to Engineer = base damage for that unit
    /// </summary>
    internal class BaseMeleeDamageProperty : PropertyHandler<eChimps, int>
    {
        /// <summary>
        /// The Engineer unit serves as the reference target for base damage calculation.
        /// Engineer has 1.0 armor from all damage sources, so damage dealt to Engineer = base damage.
        /// </summary>
        private const eChimps ReferenceUnit = eChimps.CHIMP_TYPE_ENGINEER;

        public BaseMeleeDamageProperty() : base("BaseMeleeDamage")
        {
        }

        /// <summary>
        /// Get the base melee damage as the damage this unit deals TO the Engineer.
        /// </summary>
        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            value = 0;

            // Skip non-modifiable units
            if (UnitCategories.IsNonModifiable(unit))
            {
                return false;
            }

            try
            {
                // Base damage = damage dealt to Engineer (who has 1.0 armor)
                value = Plugin.UnitApi.GetMeleeDamageFromTo(unit, ReferenceUnit);
                return value >= 0;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get melee damage {unit} -> Engineer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set the base melee damage by updating the damage dealt to Engineer.
        /// </summary>
        protected override void SetToAPI(eChimps unit, int newBaseDamage)
        {
            if (UnitCategories.IsNonModifiable(unit))
                return;

            try
            {
                Plugin.UnitApi.SetMeleeDamageFromTo(unit, ReferenceUnit, newBaseDamage);
                Plugin.Logger.LogDebug($"Set base melee damage for {unit} to {newBaseDamage}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set base melee damage for {unit}: {ex.Message}");
            }
        }

        internal override bool ValidateValue(int value)
        {
            return value >= 0 && value <= 10000;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            if (UnitCategories.IsNonModifiable(unit))
                return false;

            // Exclude units that don't attack in melee (e.g., Arab Ballista)
            if (UnitCategories.IsNonMeleeAttacker(unit))
            {
                Plugin.Logger.LogDebug($"Excluding {unit} from BaseMeleeDamage (non-melee attacker)");
                return false;
            }

            // Only apply to units that can deal melee damage (>2 damage in at least one matchup)
            // Units that deal exactly 2 damage are non-combatants and cannot actually damage in-game
            try
            {
                foreach (eChimps defender in Enum.GetValues(typeof(eChimps)))
                {
                    if (UnitCategories.IsNonModifiable(defender))
                        continue;

                    int damage = Plugin.UnitApi.GetMeleeDamageFromTo(unit, defender);
                    if (damage > 2)
                        return true; // Found at least one matchup with real damage
                }
            }
            catch
            {
                // If we can't check damage, assume it's not a combatant
            }

            return false; // No matchups with >2 damage found
        }

        /// <summary>
        /// Get the original base damage value (damage to Engineer from original defaults).
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue)
        {
            defaultValue = 0;

            if (UnitCategories.IsNonModifiable(unit))
                return false;

            try
            {
                defaultValue = CsvMatrixReader.GetOriginalMeleeDamage(unit, ReferenceUnit);
                return defaultValue >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

