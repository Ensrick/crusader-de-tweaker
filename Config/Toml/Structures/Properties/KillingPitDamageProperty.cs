// Config/Toml/Structures/Properties/KillingPitDamageProperty.cs
// Exposes killing pit damage in CrusaderDETweaker_Structures.toml under [STRUCT_KILLING_PIT].
//
// 2.11.9 (GitLab #16, plan finding FWP-E-12): writes the Script Extender's public value
// GameBuildingManagerAPI.KillingPitDamage (SE 2.14.x: a managed value, default 18000, read by SE's killing-pit hook
// for every unit that falls in, BulkUnitDetours c_game_unit_is_over_killingpit_update_hook). Before, the mod
// overwrote the damage of every OnUnitEnterKillingPit event, which also replaced what any other mod set there.
// SE does not reset the value on map unload, so the TemplateBaseline restore (the value read before the first
// write) puts it back. No OnUnitEnterKillingPit subscription remains.
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures.Properties
{
    internal class KillingPitDamageProperty : PropertyHandler<eStructs, int>
    {
        private const int DefaultDamage = 18000;

        public KillingPitDamageProperty() : base("Damage") { }

        internal override bool CanApplyTo(eStructs structure)
        {
            return structure == eStructs.STRUCT_KILLING_PIT;
        }

        protected override bool TryGetFromAPI(eStructs structure, out int value)
        {
            value = DefaultDamage;
            var api = Plugin.BuildingApi;
            if (api == null) return true;
            value = api.KillingPitDamage.Value;
            return true;
        }

        protected override bool TryGetOriginalValue(eStructs entity, out int defaultValue)
        {
            return TryGetFromAPI(entity, out defaultValue);
        }

        protected override void SetToAPI(eStructs structure, int value)
        {
            ErrorHandlingHelper.TryExecute(
                "Set KillingPitDamage", structure.ToString(),
                () => Plugin.BuildingApi.KillingPitDamage.SetValue(value));
        }

        internal override bool ValidateValue(int value)
        {
            // Game stores damage in a 16-bit field; values outside 0..32767 are rejected (see CHANGELOG v2.1.5).
            return value >= 0 && value <= 32767;
        }
    }
}
