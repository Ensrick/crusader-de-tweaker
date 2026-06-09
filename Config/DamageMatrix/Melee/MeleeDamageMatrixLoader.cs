// Config/DamageMatrix/Melee/MeleeDamageMatrixLoader.cs
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Melee
{
    /// <summary>
    /// Loads and applies the melee damage matrix from CSV file.
    /// Matrix format: Rows = Defenders, Columns = Attackers
    /// </summary>
    internal class MeleeDamageMatrixLoader : MatrixLoader<eChimps, eChimps>
    {
        protected override string FilePath => MatrixPaths.MeleeDamage;

        /// <summary>
        /// Parse attacker unit types from CSV columns.
        /// </summary>
        protected override Nullable<eChimps>[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        /// <summary>
        /// Parse defender unit types from CSV rows.
        /// </summary>
        protected override Nullable<eChimps>[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        /// <summary>
        /// Apply a melee damage value to the game (from CSV override).
        /// </summary>
        protected override bool ApplyDamageValue(eChimps attacker, eChimps defender, int damage)
        {
            // Note: Skip checks are now done in base class Load() method before calling this
            // This method assumes valid, modifiable entities
            try
            {
                Plugin.UnitApi.SetMeleeDamageFromTo(attacker, defender, damage);
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set melee damage {attacker} -> {defender}: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Validate the damage value before applying.
        /// </summary>
        protected override bool ValidateDamageValue(int damage)
        {
            // Skip negative values (they indicate invalid/N/A pairs)
            // Allow 0 (some units deal no damage but should still be configurable)
            return damage >= 0;
        }

        /// <summary>
        /// Check if an attacker should be skipped.
        /// </summary>
        protected override bool ShouldSkipAttacker(eChimps attacker)
        {
            return UnitMatrixHelper.ShouldSkipUnit(attacker);
        }

        /// <summary>
        /// Check if a defender should be skipped.
        /// </summary>
        protected override bool ShouldSkipDefender(eChimps defender)
        {
            return UnitMatrixHelper.ShouldSkipUnit(defender);
        }

    }
}