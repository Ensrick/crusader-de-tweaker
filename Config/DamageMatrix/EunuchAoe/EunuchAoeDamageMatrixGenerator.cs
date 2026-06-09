// Config/DamageMatrix/EunuchAoe/EunuchAoeDamageMatrixGenerator.cs
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.EunuchAoe
{
    /// <summary>
    /// Generates the Eunuch AOE damage matrix CSV file.
    /// Matrix format: Rows = Defenders, Column = EunuchAOE (single attacker)
    /// </summary>
    internal class EunuchAoeDamageMatrixGenerator : MatrixGenerator<EunuchAoeType, eChimps>
    {
        protected override string FilePath => MatrixPaths.EunuchAoeDamage;

        /// <summary>
        /// Get the single "attacker" type (EunuchAOE).
        /// </summary>
        protected override EunuchAoeType[] GetAttackers()
        {
            return new[] { EunuchAoeType.EunuchAOE };
        }

        /// <summary>
        /// Get all units that can take Eunuch AOE damage (defenders = rows).
        /// Excludes non-modifiable units.
        /// </summary>
        protected override eChimps[] GetDefenders()
        {
            return UnitMatrixHelper.GetModifiableUnits();
        }

        /// <summary>
        /// Get the Eunuch AOE damage value against a defender.
        /// </summary>
        protected override int GetDamageValue(EunuchAoeType attackType, eChimps defender)
        {
            try
            {
                return Plugin.UnitApi.GetMeleeEunuchAOEDamageTo(defender);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get Eunuch AOE damage for {defender}: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Customize attacker header name.
        /// </summary>
        protected override string GetAttackerHeader(EunuchAoeType attackType)
        {
            return "EunuchAOE";
        }

    }
}