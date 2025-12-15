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
            _instance.Register(new Resource1TypeProperty());
            _instance.Register(new Resource2TypeProperty());
            _instance.Register(new Resource3TypeProperty());
            _instance.Register(new Resource4TypeProperty());

            // ============================================
            // ADVANCED DAMAGE SYSTEM PROPERTIES
            // ============================================

            // Base damage output (before armor multipliers)
            _instance.Register(new BaseMeleeDamageProperty());

            // Armor value - defender damage multiplier (0.5 = heavy, 1.0 = standard, 2.0 = unarmored)
            _instance.Register(new ArmorValueProperty());

            // Tags for special rules (Polearm, Cavalry, Beast, Ranged_Bow, etc.)
            _instance.Register(new TagsProperty());

            // ============================================
            // DISCOVERED MODEL PROPERTIES
            // ============================================

            // Discovered base damage from model discovery system
            _instance.Register(new DiscoveredBaseDamageProperty());

            // Discovered armor value from model discovery system
            _instance.Register(new DiscoveredArmorValueProperty());

            // Weapon profile ID (NMF clustering)
            _instance.Register(new WeaponProfileIdProperty());

            // Armor profile ID (NMF clustering)
            _instance.Register(new ArmorProfileIdProperty());

            // ============================================
            // SPECIAL UNIT PROPERTIES
            // ============================================
            
            // Shield health for Bedouin Demolisher
            _instance.Register(new ShieldHealthProperty());
            
            // AOE base damage for Bedouin Eunuch
            _instance.Register(new EunuchAoeBaseDamageProperty());

            Plugin.Logger.LogInfo($"Registered {_instance.Count} unit property handlers");
        }
    }
}