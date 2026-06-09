// Config/DamageMatrix/EunuchAoe/EunuchAoeDamageMatrixLoader.cs
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.EunuchAoe
{
    /// <summary>
    /// Loads and applies the Eunuch AOE damage matrix from CSV file.
    /// Matrix format: Rows = Defenders, Column = EunuchAOE
    /// </summary>
    internal class EunuchAoeDamageMatrixLoader : MatrixLoader<EunuchAoeType, eChimps>
    {
        protected override string FilePath => MatrixPaths.EunuchAoeDamage;

        /// <summary>
        /// Parse attacker type header from CSV column.
        /// </summary>
        protected override Nullable<EunuchAoeType>[] ParseAttackerHeaders(string[] headers)
        {
            return ParseEnumHeaders<EunuchAoeType>(headers);
        }

        /// <summary>
        /// Parse defender unit types from CSV rows.
        /// </summary>
        protected override Nullable<eChimps>[] ParseDefenderHeaders(string[] headers)
        {
            return ParseEnumHeaders<eChimps>(headers);
        }

        /// <summary>
        /// Apply a Eunuch AOE damage value to the game (from CSV override).
        /// </summary>
        protected override bool ApplyDamageValue(EunuchAoeType attackType, eChimps defender, int damage)
        {
            // Note: Skip checks are now done in base class Load() method before calling this
            // This method assumes valid, modifiable entities
            try
            {
                Plugin.UnitApi.SetMeleeEunuchAOEDamageTo(defender, damage);
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to set Eunuch AOE damage for {defender}: {ex.Message}");
                return false;
            }
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