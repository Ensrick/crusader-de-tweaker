using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;
using SHCDESE.GameGlobals;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the ArabHorsemanRunSpeedBonus property.
    /// Determines the speed bonus applied to Arab Horsemen when they run.
    /// </summary>
    internal class ArabHorsemanRunSpeedBonusProperty : PropertyHandler<eChimps, ushort>
    {
        public ArabHorsemanRunSpeedBonusProperty() : base("ArabHorsemanRunSpeedBonus")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out ushort value)
        {
            if (unit == eChimps.CHIMP_TYPE_ARAB_HORSEMAN)
            {
                var prop = Plugin.GlobalsApi?.ArabHorsemanRunSpeedBonus;
                if (prop != null)
                {
                    value = prop.GetValue();
                    return true;
                }
            }

            value = 0;
            return false;
        }

        protected override void SetToAPI(eChimps unit, ushort value)
        {
            if (unit == eChimps.CHIMP_TYPE_ARAB_HORSEMAN)
            {
                var prop = Plugin.GlobalsApi?.ArabHorsemanRunSpeedBonus;
                if (prop != null)
                {
                    prop.SetValue(value);
                    Plugin.Logger.LogInfo($"Set Arab Horseman run speed bonus to {value} via GameGlobalsManager.");
                }
                else
                {
                    Plugin.Logger.LogWarning("ArabHorsemanRunSpeedBonus property not found in GameGlobalsManager.");
                }
            }
        }

        protected override bool TryGetOriginalValue(eChimps unit, out ushort defaultValue)
        {
            if (unit == eChimps.CHIMP_TYPE_ARAB_HORSEMAN)
            {
                var prop = Plugin.GlobalsApi?.ArabHorsemanRunSpeedBonus;
                if (prop != null)
                {
                    defaultValue = prop.GetValue();
                    return true;
                }
            }
            defaultValue = 0;
            return false;
        }

        internal override bool ValidateValue(ushort value)
        {
            return true;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            return unit == eChimps.CHIMP_TYPE_ARAB_HORSEMAN;
        }
    }
}
