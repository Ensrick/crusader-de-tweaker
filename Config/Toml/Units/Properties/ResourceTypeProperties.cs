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
            // An unknown name leaves the game's requirement untouched: a typo must not silently
            // turn the unit into a gold-only hire. NONE / "" / STORED_NULL remove the requirement.
            if (!TryParseResource(value, out eGoods32 resource))
            {
                Plugin.Logger.LogWarning(
                    $"[Set {Name}] Unknown resource '{value}' for {unit} - keeping the game default. " +
                    "Valid: STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS, STORED_SPEARS, STORED_PIKES, STORED_MACES, " +
                    "STORED_LEATHER_ARMOUR, STORED_METAL_ARMOUR, or NONE (no requirement).");
                return;
            }

            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                unit.ToString(),
                () =>
                {
                    var costs = Plugin.UnitApi.GetUnitGoodCosts(unit);

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
            // "" is a legal value ("no requirement", same as NONE); only a missing string is invalid.
            return value != null;
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

        /// <summary>
        /// User-facing keyword for "no resource required" (gold-only hire). "" and STORED_NULL are
        /// accepted as synonyms. Deleting the line does NOT do this - a missing key means "no
        /// override" and the game default is re-added on the next launch.
        /// </summary>
        internal const string NoneKeyword = "NONE";

        /// <summary>
        /// Parse a resource name from the TOML. Returns false for an unknown name so the caller can
        /// leave the game's requirement untouched instead of zeroing it.
        /// </summary>
        private static bool TryParseResource(string resourceType, out eGoods32 resource)
        {
            resource = eGoods32.STORED_NULL;
            if (resourceType == null)
                return false;

            string name = resourceType.Trim();
            if (name.Length == 0
                || name.Equals(NoneKeyword, StringComparison.OrdinalIgnoreCase)
                || name.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!TypeConverter.TryParseEnum<eGoods>(name, out var goodType))
                return false;

            int enumValue = (int)goodType;

            // Allow special case: _SE_REQUIRE_HORSE = -1
            if (enumValue == -1)
            {
                resource = (eGoods32)enumValue;
                return true;
            }

            // For other values, validate they're in the normal range
            if (enumValue >= 0 && enumValue < (int)eGoods32.Count)
            {
                resource = (eGoods32)enumValue;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Handles the WeaponType property for Crusader units.
    /// This is Resource1Type (slot 0) - always a weapon resource for recruitable units.
    /// Examples: STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS, STORED_PIKES, STORED_MACES.
    /// "NONE" removes the requirement (gold-only hire, like the Arabian mercenaries).
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
    /// Handles the RequiresHorse property for recruitable units.
    /// For the 7 Barracks units (Archer..Knight) this is Resource4Type (slot 3) of the game's EU
    /// good-cost table, where -1 (_SE_REQUIRE_HORSE) is the game's own "needs a stable horse"
    /// marker, so the Barracks enforces it natively. Arabian / Bedouin mercenaries have no row in
    /// that table (SHCDE-SE exposes exactly 7 and silently ignores writes past them); for them the
    /// requirement is enforced by MakeTroopRecruitHook at hire time (see StableTracking).
    ///
    /// HorseRequiringUnits tracks which unit types were set to RequiresHorse = true via config.
    /// Consumed by StableTracking (recruit gate + linking spawned/recruited units to a stable slot).
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
            if (Config.Core.StableTracking.GameEnforcesHorseCost(unit))
            {
                // Barracks unit: write the game's own marker so the native purchase check applies.
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
            }
            else if (value)
            {
                // No good-cost row exists for this unit; the recruit hook enforces the horse instead.
                Plugin.Logger.LogDebug($"[Set RequiresHorse] {unit} is not a Barracks unit - horse requirement enforced by the mod at hire time.");
            }

            // Update the tracked set so the recruit gate and the stable-linking hooks
            // know which unit types need a stable slot.
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