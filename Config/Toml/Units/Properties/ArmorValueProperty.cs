// Config/Toml/Units/Properties/ArmorValueProperty.cs
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Systems;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the ArmorValue property for units.
    /// Multiplier for incoming damage (damage taken multiplier).
    /// 
    /// - 0.4 = Very Heavy (Trebuchet) - takes 40% damage
    /// - 0.5 = Heavy armor (Knight, Swordsman) - takes 50% damage
    /// - 1.0 = Standard armor (most units) - takes 100% damage
    /// - 1.5 = Light armor (Arab Slinger, Eunuch) - takes 150% damage
    /// - 2.0 = Unarmored (Arab Slave, Bedouin Healer) - takes 200% damage
    /// </summary>
    internal class ArmorValueProperty : PropertyHandler<eChimps, float>
    {
        public ArmorValueProperty() : base("ArmorValue")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out float value)
        {
            var unitData = UnitDamageRegistry.GetUnitData(unit);
            if (unitData != null)
            {
                value = unitData.ArmorValue;
                return true;
            }

            value = 1.0f; // Default: Standard armor
            return false;
        }

        protected override void SetToAPI(eChimps unit, float value)
        {
            var unitData = UnitDamageRegistry.GetUnitData(unit);
            if (unitData == null)
            {
                unitData = new Data.UnitDamageData { Unit = unit };
                UnitDamageRegistry.RegisterUnit(unitData);
            }

            unitData.ArmorValue = value;
        }

        internal override bool ValidateValue(float value)
        {
            // Armor can be 0 (immune) or positive up to reasonable max
            return value >= 0f && value <= 10.0f;
        }
    }
}