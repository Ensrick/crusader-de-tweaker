// Config/Toml/Structures/Properties/StructureHealthProperty.cs
using System.Linq;
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures.Properties
{
    /// <summary>
    /// Handles the Health property for structures.
    /// </summary>
    internal class StructureHealthProperty : PropertyHandler<eStructs, uint>
    {
        public StructureHealthProperty() : base("Health")
        {
        }

        protected override bool TryGetFromAPI(eStructs structure, out uint value)
        {
            int healthValue = ErrorHandlingHelper.TryGetValue(
                $"Get {Name}",
                structure.ToString(),
                () => Plugin.BuildingApi.GetDefaultHealth(structure),
                defaultValue: 0
            );
            value = (uint)healthValue;
            // Don't include health property in config if health is 0 (e.g., drawbridge)
            return healthValue > 0;
        }

        protected override bool TryGetOriginalValue(eStructs entity, out uint defaultValue)
        {
            return TryGetFromAPI(entity, out defaultValue);
        }

        protected override void SetToAPI(eStructs structure, uint value)
        {
            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                structure.ToString(),
                () => Plugin.BuildingApi.SetDefaultHealth(structure, value)
            );
        }

        internal override bool CanApplyTo(eStructs structure)
        {
            // All structures can have health modified (walls are in NonModableStructures and won't be processed)
            return true;
        }

        internal override bool ValidateValue(uint value)
        {
            // Health must be positive or zero (some structures like pitch ditch have 0)
            return true;
        }
    }
}