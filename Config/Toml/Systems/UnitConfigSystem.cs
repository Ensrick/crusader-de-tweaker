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
            ConfigLoader.ApplyAllUnitConfigs();
            IsLoaded = true;
        }
    }
}

