// Config/Toml/Structures/Properties/StructureHousingPopulationSpaceProperty.cs
using System.Linq;
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures.Properties
{
    /// <summary>
    /// Handles the HousingPopulationSpace property for housing structures.
    /// Controls how much population capacity a building provides.
    /// </summary>
    internal class StructureHousingPopulationSpaceProperty : PropertyHandler<eStructs, ushort>
    {
        public StructureHousingPopulationSpaceProperty() : base("HousingPopulationSpace")
        {
        }

        protected override bool TryGetFromAPI(eStructs structure, out ushort value)
        {
            int housingValue = ErrorHandlingHelper.TryGetValue(
                $"Get {Name}",
                structure.ToString(),
                () => Plugin.BuildingApi.GetDefaultHousingPopulationSpace(structure),
                defaultValue: 0
            );
            value = (ushort)housingValue;
            return housingValue >= 0;
        }

        protected override void SetToAPI(eStructs structure, ushort value)
        {
            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                structure.ToString(),
                () => Plugin.BuildingApi.SetDefaultHousingPopulationSpace(structure, value)
            );
        }

        internal override bool CanApplyTo(eStructs structure)
        {
            // Housing space can be 0 and allowed on all structures
            // Only apply to housing structures
            // Check if structure has any housing capacity
            if (!TryGetFromAPI(structure, out _))
                return false;

            // If it has 0 housing capacity, it's not a housing structure
            // But we still want to allow it in config in case user wants to add housing
            // So we'll return true for all structures that don't throw an exception
            return true;
        }

        internal override bool ValidateValue(ushort value)
        {
            // Housing space must be non-negative (0 is valid for non-housing structures)
            // ushort is already non-negative by definition, so always valid
            return true;
        }
    }
}