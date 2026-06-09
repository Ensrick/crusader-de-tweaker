// Config/Toml/Structures/StructurePropertyRegistry.cs
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Structures.Properties;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Structures
{
    /// <summary>
    /// Registry containing all structure property handlers.
    /// Singleton pattern - only one instance exists.
    /// </summary>
    internal static class StructurePropertyRegistry
    {
        private static PropertyRegistry<eStructs> _instance;

        /// <summary>
        /// Get the singleton registry instance.
        /// Lazily initialized on first access.
        /// </summary>
        public static PropertyRegistry<eStructs> Instance
        {
            get
            {
                if (_instance == null)
                {
                    Initialize();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initialize the registry and register all property handlers.
        /// </summary>
        private static void Initialize()
        {
            _instance = new PropertyRegistry<eStructs>();

            // Register basic properties
            _instance.Register(new StructureHealthProperty());

            // Register cost properties (individual handlers)
            _instance.Register(new GoldCostProperty());
            _instance.Register(new WoodCostProperty());
            _instance.Register(new StoneCostProperty());
            _instance.Register(new IronCostProperty());
            _instance.Register(new PitchCostProperty());

            // Register housing property
            _instance.Register(new StructureHousingPopulationSpaceProperty());

            // Register killing pit damage (STRUCT_KILLING_PIT only — applied via OnUnitEnterKillingPit hook)
            _instance.Register(new KillingPitDamageProperty());

            Plugin.Logger.LogInfo($"Registered {_instance.Count} structure property handlers");
        }
    }
}