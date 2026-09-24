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
            // Applied by ConfigLoader.ReapplyTemplateConfigs("launch") once the multipliers are bound (Plugin.cs):
            // the one template write path (v2.7.0). Loading here only marks the system ready.
            IsLoaded = true;
        }
    }
}

