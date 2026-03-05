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
            // Skip 0-housing structures — user can add manually if needed
            return housingValue > 0;
        }

        protected override bool TryGetOriginalValue(eStructs entity, out ushort defaultValue)
        {
            return TryGetFromAPI(entity, out defaultValue);
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
            // Allow loading for any structure (user can manually add housing to non-housing structures)
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