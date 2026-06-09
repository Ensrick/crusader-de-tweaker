// Config/Toml/Structures/Properties/KillingPitDamageProperty.cs
// Exposes killing pit damage in CrusaderDETweaker_Structures.toml under [STRUCT_KILLING_PIT].
//
// SHCDE-SE exposes this via OnUnitEnterKillingPit (Pre phase, args.Damage is writable).
// There is no public persistent setter — GameBuildingManagerAPI._killingPitDamage is internal.
// The configured value is applied per-event via the Pre-phase hook.
// Default: 18000 (GameBuildingManagerAPI.DEFAULT_KILLINGPIT_DAMAGE).
using CrusaderDETweaker.Config.Toml.Core;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures.Properties
{
    internal class KillingPitDamageProperty : PropertyHandler<eStructs, int>
    {
        private const int DefaultDamage = 18000;
        private static int _configuredDamage = DefaultDamage;
        private static bool _subscribed = false;

        public KillingPitDamageProperty() : base("Damage") { }

        internal override bool CanApplyTo(eStructs structure)
        {
            return structure == eStructs.STRUCT_KILLING_PIT;
        }

        protected override bool TryGetFromAPI(eStructs structure, out int value)
        {
            // No public getter — return the engine default.
            value = DefaultDamage;
            return true;
        }

        protected override bool TryGetOriginalValue(eStructs entity, out int defaultValue)
        {
            defaultValue = DefaultDamage;
            return true;
        }

        protected override void SetToAPI(eStructs structure, int value)
        {
            _configuredDamage = value;

            if (_subscribed) return;
            _subscribed = true;

            UnitR3EventHooks.OnUnitEnterKillingPit.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => _.Damage = _configuredDamage);
        }

        internal override bool ValidateValue(int value)
        {
            // Game stores damage in a 16-bit field; values outside 0..32767 are rejected (see CHANGELOG v2.1.5).
            return value >= 0 && value <= 32767;
        }
    }
}
