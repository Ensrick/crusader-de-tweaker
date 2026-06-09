using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;
using SHCDESE.GameGlobals;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the KnightRunSpeedBonus property.
    /// Determines the speed bonus applied to Knights when they run.
    /// </summary>
    internal class KnightRunSpeedBonusProperty : PropertyHandler<eChimps, ushort>
    {
        public KnightRunSpeedBonusProperty() : base("KnightRunSpeedBonus")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out ushort value)
        {
            if (unit == eChimps.CHIMP_TYPE_KNIGHT)
            {
                var prop = Plugin.GlobalsApi?.KnightRunSpeedBonus;
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
            if (unit == eChimps.CHIMP_TYPE_KNIGHT)
            {
                var prop = Plugin.GlobalsApi?.KnightRunSpeedBonus;
                if (prop != null)
                {
                    prop.SetValue(value);
                    Plugin.Logger.LogInfo($"Set Knight run speed bonus to {value} via GameGlobalsManager.");
                }
                else
                {
                    Plugin.Logger.LogWarning("KnightRunSpeedBonus property not found in GameGlobalsManager.");
                }
            }
        }

        protected override bool TryGetOriginalValue(eChimps unit, out ushort defaultValue)
        {
            if (unit == eChimps.CHIMP_TYPE_KNIGHT)
            {
                var prop = Plugin.GlobalsApi?.KnightRunSpeedBonus;
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
            return unit == eChimps.CHIMP_TYPE_KNIGHT;
        }
    }
}
