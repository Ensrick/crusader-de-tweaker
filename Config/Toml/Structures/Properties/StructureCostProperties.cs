// Config/Toml/Structures/Properties/StructureCostProperties.cs
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures.Properties
{
    /// <summary>
    /// Base class for structure cost properties.
    /// </summary>
    internal abstract class StructureCostPropertyBase : PropertyHandler<eStructs, int>
    {
        protected StructureCostPropertyBase(string name) : base(name)
        {
        }

        protected abstract int GetCost(BuildingCost cost);
        protected abstract void SetCost(ref BuildingCost cost, int value);

        protected override bool TryGetFromAPI(eStructs structure, out int value)
        {
            value = 0;

            // Walls use multipliers, not regular cost properties
            if (StructureCategories.IsWall(structure))
                return false;

            BuildingCost cost = ErrorHandlingHelper.TryGetValue(
                $"Get {Name}",
                structure.ToString(),
                () => Plugin.BuildingApi.GetDefaultCost(structure),
                defaultValue: default
            );

            value = GetCost(cost);
            // Skip 0-cost entries — user can add them manually if needed
            return value > 0;
        }

        protected override bool TryGetOriginalValue(eStructs entity, out int defaultValue)
        {
            return TryGetFromAPI(entity, out defaultValue);
        }

        protected override void SetToAPI(eStructs structure, int value)
        {
            // Walls use multipliers, not regular cost properties
            if (StructureCategories.IsWall(structure))
                return;

            ErrorHandlingHelper.TryExecute(
                $"Set {Name}",
                structure.ToString(),
                () =>
                {
                    var cost = Plugin.BuildingApi.GetDefaultCost(structure);
                    SetCost(ref cost, value);
                    Plugin.BuildingApi.SetDefaultCost(structure, cost);
                }
            );
        }

        internal override bool ValidateValue(int value)
        {
            // Costs can be 0, positive, or even negative for special cases
            return true;
        }
    }

    internal class GoldCostProperty : StructureCostPropertyBase
    {
        public GoldCostProperty() : base("GoldCost") { }

        protected override int GetCost(BuildingCost cost) => cost.Gold;
        protected override void SetCost(ref BuildingCost cost, int value) => cost.Gold = value;
    }

    internal class WoodCostProperty : StructureCostPropertyBase
    {
        public WoodCostProperty() : base("WoodCost") { }

        protected override int GetCost(BuildingCost cost) => cost.Wood;
        protected override void SetCost(ref BuildingCost cost, int value) => cost.Wood = value;
    }

    internal class StoneCostProperty : StructureCostPropertyBase
    {
        public StoneCostProperty() : base("StoneCost") { }

        protected override int GetCost(BuildingCost cost) => cost.Stone;
        protected override void SetCost(ref BuildingCost cost, int value) => cost.Stone = value;
    }

    internal class IronCostProperty : StructureCostPropertyBase
    {
        public IronCostProperty() : base("IronCost") { }

        protected override int GetCost(BuildingCost cost) => cost.Iron;
        protected override void SetCost(ref BuildingCost cost, int value) => cost.Iron = value;
    }

    internal class PitchCostProperty : StructureCostPropertyBase
    {
        public PitchCostProperty() : base("PitchCost") { }

        protected override int GetCost(BuildingCost cost) => cost.Pitch;
        protected override void SetCost(ref BuildingCost cost, int value) => cost.Pitch = value;
    }
}