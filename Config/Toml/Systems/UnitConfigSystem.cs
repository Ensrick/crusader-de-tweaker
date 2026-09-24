// Config/Toml/Systems/UnitConfigSystem.cs
namespace CrusaderDETweaker.Config.Toml.Systems
{
    /// <summary>
    /// Wrapper that implements IConfigSystem for unit configurations.
    /// </summary>
    internal class UnitConfigSystem : BaseConfigSystem
    {
        public override string Name => "Unit Config";

        public override void GenerateDefaults()
        {
            ConfigGenerator.GenerateDefaultConfigUnits();
        }

        public override void Load()
        {
            // Applied by ConfigLoader.ReapplyTemplateConfigs("launch") once the multipliers are bound (Plugin.cs):
            // the one template write path (v2.7.0). Loading here only marks the system ready.
            IsLoaded = true;
        }
    }
}

