using System;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal class MeleeDamageTypeProperty : PropertyHandler<eChimps, string>
    {
        public MeleeDamageTypeProperty() : base("MeleeDamageType")
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out string value)
        {
            value = UnitMeleeDamageTypeRegistry.Get(unit).ToString();
            return true;
        }

        protected override void SetToAPI(eChimps unit, string value)
        {
            if (Enum.TryParse<MeleeDamageType>(value, ignoreCase: true, out var parsed))
            {
                UnitMeleeDamageTypeRegistry.Set(unit, parsed);
            }
        }

        internal override bool ValidateValue(string value)
        {
            return Enum.TryParse<MeleeDamageType>(value, ignoreCase: true, out _);
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            if (UnitCategories.IsNonModifiable(unit))
                return false;

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

        protected override bool TryGetOriginalValue(eChimps unit, out string defaultValue)
        {
            defaultValue = MeleeDamageType.Type1.ToString();
            return true;
        }
    }
}
