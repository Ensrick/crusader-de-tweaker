// Config/Toml/Units/UnitPropertyRegistry.cs
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Units.Properties;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units
{
    /// <summary>
    /// Registry containing all unit property handlers.
    /// Singleton pattern - only one instance exists.
    /// </summary>
    internal static class UnitPropertyRegistry
    {
        private static PropertyRegistry<eChimps> _instance;

        /// <summary>
        /// Get the singleton registry instance.
        /// Lazily initialized on first access.
        /// </summary>
        public static PropertyRegistry<eChimps> Instance
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
        /// 
        /// TO ADD A NEW PROPERTY:
        /// 1. Create a new PropertyHandler class in Config/Toml/Units/Properties/
        /// 2. Inherit from PropertyHandler&lt;eChimps, TValue&gt;
        /// 3. Add _instance.Register(new YourPropertyHandler()); below in the appropriate section
        /// 4. See CLAUDE.md PropertyHandler System section for template
        /// </summary>
        private static void Initialize()
        {
            _instance = new PropertyRegistry<eChimps>();

            // ============================================
            // BASIC PROPERTIES
            // ============================================
            _instance.Register(new HealthProperty());
            _instance.Register(new SpeedProperty());

            // ============================================
            // COST PROPERTIES
            // ============================================
            _instance.Register(new GoldCostProperty());

            // ============================================
            // RESOURCE TYPE PROPERTIES
            // ============================================
            // Only three resource requirements are supported:
            // - WeaponType (Resource1): STORED_SWORDS, STORED_BOWS, STORED_XBOWS, STORED_PIKES, STORED_MACES
            // - ArmorType (Resource2): STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR
            // - RequiresHorse (Resource4): true/false for cavalry units
            // Resource3 is unused in game logic and has been removed
            _instance.Register(new WeaponTypeProperty());
            _instance.Register(new ArmorTypeProperty());
            _instance.Register(new RequiresHorseProperty());

            // Per-unit and per-matchup damage is configured entirely through the CSV matrix
            // files (CrusaderDETweaker_MeleeDamage.csv / _RangedDamage.csv / _EunuchAoeDamage.csv,
            // etc.), not through TOML property handlers.

            // ============================================
            // SPECIAL UNIT PROPERTIES
            // ============================================

            // Shield health for Bedouin Demolisher
            _instance.Register(new ShieldHealthProperty());

            // Run speed bonuses
            _instance.Register(new KnightRunSpeedBonusProperty());
            _instance.Register(new ArabHorsemanRunSpeedBonusProperty());
            _instance.Register(new BedouinCamelLancerRunSpeedBonusProperty());
            _instance.Register(new BedouinHeavyCamelRunSpeedBonusProperty());

            Plugin.Logger.LogInfo($"Registered {_instance.Count} unit property handlers");
        }
    }
}