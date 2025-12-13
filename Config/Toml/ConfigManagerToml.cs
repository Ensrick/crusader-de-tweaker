using CrusaderDETweaker.Config.Toml.Armor;

namespace CrusaderDETweaker
{
    internal static class ConfigManagerToml
    {
        internal static void Initialize()
        {
            // Create default configs if they don't exist
            ConfigGenerator.GenerateDefaultConfigUnits();
            ConfigGenerator.GenerateDefaultConfigStructures();
            ArmorConfigGenerator.GenerateDefault();

            // Load and apply all configs
            ConfigLoader.ApplyAllUnitConfigs();
            ConfigLoader.ApplyAllStructureConfigs();
            ArmorConfigLoader.Load();
        }
    }

    internal class UnitConfig
    {
        public int? Health { get; set; }
        public int? ArrowDamageTaken { get; set; }
        public int? BoltDamageTaken { get; set; }
        public int? SlingerDamageTaken { get; set; }
        public int? Speed { get; set; }

        public float? MeleeSwordDamageTakenMult { get; set; }
        public float? MeleeBluntDamageTakenMult { get; set; }
        public float? MeleePolearmDamageTakenMult { get; set; }
        public float? MeleeCavalryDamageTakenMult { get; set; }
        public float? MeleeOtherDamageTakenMult { get; set; }

        public int? GoldCost { get; set; }

        public string Resource1Type { get; set; }
        public string Resource2Type { get; set; }
        public string Resource3Type { get; set; }
        public string Resource4Type { get; set; }
    }

    internal class StructureConfig
    {
        public int? Health { get; set; }
        public int? GoldCost { get; set; }
        public int? WoodCost { get; set; }
        public int? StoneCost { get; set; }
        public int? IronCost { get; set; }
        public int? PitchCost { get; set; }
        public ushort? HousingPopulationSpace { get; set; }
    }

    internal class TomlRoot
    {
        public Dictionary<string, UnitConfig> Units { get; set; }
        public Dictionary<string, StructureConfig> Structures { get; set; }
    }
}