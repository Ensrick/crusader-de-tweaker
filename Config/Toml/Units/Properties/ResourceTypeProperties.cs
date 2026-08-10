// Config/Toml/Units/Properties/ResourceTypeProperties.cs
using System;
using System.Linq;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Base class for resource type properties.
    /// </summary>
    internal abstract class ResourceTypePropertyBase : PropertyHandler<eChimps, string>
    {
        protected abstract int SlotIndex { get; }

        protected ResourceTypePropertyBase(string name) : base(name)
        {
        }

        protected override bool TryGetFromAPI(eChimps unit, out string value)
        {
            value = null;

            // Try to get costs, but catch IndexOutOfRangeException for units without resource costs
            UnitGoodCosts costs;
            try
            {
                costs = Plugin.UnitApi.GetUnitGoodCosts(unit);
            }
            catch (IndexOutOfRangeException)
            {
                // Unit doesn't have resource costs - don't write this property to config
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Get {Name}] Failed for {unit}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }

            eGoods32 resource;
            switch (SlotIndex)
            {
                case 0:
                    resource = costs.cost1;
                    break;
                case 1:
                    resource = costs.cost2;
                    break;
                case 2:
                    resource = costs.cost3;
                    break;
                case 3:
                    resource = costs.cost4;
                    break;
                default:
                    resource = eGoods32.STORED_NULL;
                    break;
            }

            value = resource.ToString();

            // Only return true if this is a meaningful resource
            return !string.IsNullOrEmpty(value) && value != "STORED_NULL";
        }

        protected override void SetToAPI(eChimps unit, string value)
        {
            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                unit.ToString(),
                () =>
                {
                    var costs = Plugin.UnitApi.GetUnitGoodCosts(unit);
                    eGoods32 resource = ParseResource(value);

                    // Update the appropriate slot
                    switch (SlotIndex)
                    {
                        case 0:
                            costs.cost1 = resource;
                            break;
                        case 1:
                            costs.cost2 = resource;
                            break;
                        case 2:
                            costs.cost3 = resource;
                            break;
                        case 3:
                            costs.cost4 = resource;
                            break;
                    }

                    Plugin.UnitApi.SetUnitGoodCosts(unit, costs);
                }
            );
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            // Only military units that use weapons/equipment
            return UnitCategories.IsRecruitable(unit);
        }

        internal override bool ValidateValue(string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Try to get the original/default value from the API (captured before TOML modifications).
        /// For properties that read directly from API, we use the current API value as default when generating.
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out string defaultValue)
        {
            // When generating config, current API value is the default
            // This is called during config generation, so API still has original values
            return TryGetFromAPI(unit, out defaultValue);
        }

        private eGoods32 ParseResource(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType))
                return eGoods32.STORED_NULL;

            if (!TypeConverter.TryParseEnum<eGoods>(resourceType, out var goodType))
            {
                Plugin.Logger.LogWarning($"Invalid resource type '{resourceType}', using STORED_NULL");
                return eGoods32.STORED_NULL;
            }

            int enumValue = (int)goodType;

            // Allow special case: _SE_REQUIRE_HORSE = -1
            if (enumValue == -1)
            {
                return (eGoods32)enumValue;
            }

            // For other values, validate they're in the normal range
            if (enumValue >= 0 && enumValue < (int)eGoods32.Count)
            {
                return (eGoods32)enumValue;
            }

            Plugin.Logger.LogWarning($"Resource type '{resourceType}' (enum value {enumValue}) out of valid range, using STORED_NULL");
            return eGoods32.STORED_NULL;
        }
    }

    /// <summary>
    /// Handles the WeaponType property for Crusader units.
    /// This is Resource1Type (slot 0) - always a weapon resource for recruitable units.
    /// Examples: STORED_SWORDS, STORED_BOWS, STORED_XBOWS, STORED_PIKES, STORED_MACES
    /// </summary>
    internal class WeaponTypeProperty : ResourceTypePropertyBase
    {
        protected override int SlotIndex => 0;
        public WeaponTypeProperty() : base("WeaponType") { }
    }

    /// <summary>
    /// Handles the ArmorType property for Crusader units.
    /// This is Resource2Type (slot 1) - always an armor resource for recruitable units.
    /// Examples: STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR
    /// </summary>
    internal class ArmorTypeProperty : ResourceTypePropertyBase
    {
        protected override int SlotIndex => 1;
        public ArmorTypeProperty() : base("ArmorType") { }
    }

    // Resource1Type/Resource2Type were never registered; their function is served by
    // WeaponType (slot 0) and ArmorType (slot 1). The old names are recognized as
    // obsolete in ErrorLogging so legacy configs do not warn.
    // Resource3Type - unused in game logic.
    // Resource4Type - replaced by RequiresHorseProperty (boolean wrapper).

    /// <summary>
    /// Handles the RequiresHorse property for Crusader cavalry units.
    /// This is Resource4Type (slot 3) - represented as boolean for clarity.
    /// True = unit requires horse (_SE_REQUIRE_HORSE), False = no horse required
    ///
    /// HorseRequiringUnits tracks which unit types were set to RequiresHorse = true via config.
    /// Used by ConfigLoader to link newly spawned units to stable slots via SetStablesUnitIdLink.
    /// </summary>
    internal class RequiresHorseProperty : PropertyHandler<eChimps, bool>
    {
        /// <summary>
        /// Unit types that have RequiresHorse = true in the current config.
        /// Populated in SetToAPI; consumed by the OnUnitCreate stable-tracking hook.
        /// </summary>
        internal static readonly System.Collections.Generic.HashSet<eChimps> HorseRequiringUnits =
            new System.Collections.Generic.HashSet<eChimps>();

        public RequiresHorseProperty() : base("RequiresHorse") { }

        protected override bool TryGetFromAPI(eChimps unit, out bool value)
        {
            value = false;

            // Try to get costs, but catch IndexOutOfRangeException for units without resource costs
            UnitGoodCosts costs;
            try
            {
                costs = Plugin.UnitApi.GetUnitGoodCosts(unit);
            }
            catch (IndexOutOfRangeException)
            {
                // Unit doesn't have resource costs - doesn't require horse
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Get RequiresHorse] Failed for {unit}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }

            // Check if Resource4 is _SE_REQUIRE_HORSE (enum value -1)
            value = ((int)costs.cost4 == -1);

            // Only return true if this property should be written to config (i.e., unit requires horse)
            return value;
        }

        protected override void SetToAPI(eChimps unit, bool value)
        {
            ErrorHandlingHelper.TryExecute(
                "Set RequiresHorse",
                unit.ToString(),
                () =>
                {
                    var costs = Plugin.UnitApi.GetUnitGoodCosts(unit);

                    // Set Resource4 based on boolean value
                    costs.cost4 = value ? (eGoods32)(-1) : eGoods32.STORED_NULL;

                    Plugin.UnitApi.SetUnitGoodCosts(unit, costs);
                }
            );

            // Update the tracked set so the OnUnitCreate stable-linking hook
            // knows which unit types need a stable slot assignment.
            if (value)
                HorseRequiringUnits.Add(unit);
            else
                HorseRequiringUnits.Remove(unit);
        }

        internal override bool ValidateValue(bool value)
        {
            // Boolean is always valid
            return true;
        }

        internal override bool CanApplyTo(eChimps unit)
        {
            // Only apply to Crusader recruitable units
            return UnitCategories.IsRecruitable(unit) && !UnitCategories.IsNonModifiable(unit);
        }

        protected override bool TryGetOriginalValue(eChimps unit, out bool defaultValue)
        {
            return TryGetFromAPI(unit, out defaultValue);
        }
    }
}