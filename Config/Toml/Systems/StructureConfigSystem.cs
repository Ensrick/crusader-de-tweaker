// Config/Toml/Systems/StructureConfigSystem.cs
namespace CrusaderDETweaker.Config.Toml.Systems
{
    /// <summary>
    /// Wrapper that implements IConfigSystem for structure configurations.
    /// </summary>
    internal class StructureConfigSystem : BaseConfigSystem
    {
        public override string Name => "Structure Config";

        public override void GenerateDefaults()
        {
            ConfigGenerator.GenerateDefaultConfigStructures();
        }

        public override void Load()
        {
            ConfigLoader.ApplyAllStructureConfigs();
            IsLoaded = true;
        }
    }
}

