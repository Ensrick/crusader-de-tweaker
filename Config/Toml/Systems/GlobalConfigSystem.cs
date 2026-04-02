// Config/Toml/Systems/GlobalConfigSystem.cs
//
// PURPOSE: IConfigSystem wrapper for global (GameplaySettings) TOML config.
//
// WARNING: Load() must NOT call ApplyAllGlobalConfigs() directly.
// ApplyAllGlobalConfigs writes to native GameGlobalsManager memory and requires
// an active game session. Calling it here (during LibraryLoaded init) causes a
// native ACCESS_VIOLATION crash. It is invoked safely via OnStartMap/OnLoadMap
// hooks registered by RegisterSessionHooks().
//
using CrusaderDETweaker.Config.Toml;

namespace CrusaderDETweaker.Config.Toml.Systems
{
    internal class GlobalConfigSystem : BaseConfigSystem
    {
        public override string Name => "Global Config";

        public override void GenerateDefaults()
        {
            ConfigGenerator.GenerateDefaultConfigGlobals();
        }

        public override void Load()
        {
            // Registers OnStartMap/OnLoadMap hooks that call ApplyAllGlobalConfigs().
            // Do NOT call ApplyAllGlobalConfigs() here — no game session exists yet.
            ConfigLoader.RegisterSessionHooks();
            IsLoaded = true;
        }
    }
}
