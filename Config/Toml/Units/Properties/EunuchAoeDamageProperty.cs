using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the EunuchAoeDamage property for the Bedouin Eunuch unit.
    /// 
    /// Base damage is determined by the AOE damage dealt TO the Engineer unit,
    /// since the Engineer has 1.0 armor from all sources and serves as the reference target.
    /// </summary>
    internal class EunuchAoeDamageProperty : PropertyHandler<eChimps, int>
    {
        private const eChimps ReferenceUnit = eChimps.CHIMP_TYPE_ENGINEER;

        public EunuchAoeDamageProperty() : base("EunuchAoeDamage")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            value = 0;

            if (!CanApplyTo(unit))
                return false;

            try
            {
                value = Plugin.UnitApi.GetMeleeEunuchAOEDamageTo(ReferenceUnit);
                return value >= 0;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogDebug($"Could not get Eunuch AOE damage to Engineer: {ex.Message}");
                return false;
            }
        }

        protected override void SetToAPI(eChimps unit, int newBaseDamage)
        {
            if (!CanApplyTo(unit))
                return;

            try
            {
                Plugin.UnitApi.SetMeleeEunuchAOEDamageTo(ReferenceUnit, newBaseDamage);
                Plugin.Logger.LogDebug($"Set Eunuch AOE base damage to {newBaseDamage}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set Eunuch AOE base damage: {ex.Message}");
            }
        }

        internal override bool ValidateValue(int value)
        {
            return value >= 0 && value <= 100000;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            return unit == eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH;
        }

        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue)
        {
            defaultValue = 0;

            if (!CanApplyTo(unit))
                return false;

            try
            {
                defaultValue = CsvMatrixReader.GetOriginalEunuchAoeDamage(ReferenceUnit);
                return defaultValue >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
