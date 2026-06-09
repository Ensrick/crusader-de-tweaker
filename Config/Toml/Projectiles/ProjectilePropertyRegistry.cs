using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Config.Toml.Projectiles.Properties;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Config.Toml.Projectiles
{
    /// <summary>
    /// Registry containing all projectile property handlers.
    /// Singleton pattern - only one instance exists.
    /// </summary>
    internal static class ProjectilePropertyRegistry
    {
        private static PropertyRegistry<ProjectileType> _instance;

        /// <summary>
        /// Get the singleton registry instance.
        /// Lazily initialized on first access.
        /// </summary>
        public static PropertyRegistry<ProjectileType> Instance
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
            _instance = new PropertyRegistry<ProjectileType>();

            try
            {
                // DEACTIVATED: Base damage (damage to Engineer)
                // Damage is now exclusively controlled via CSV matrices.
                /*
                var handler = new ProjectileBaseDamageProperty();
                Plugin.Logger.LogDebug($"Created ProjectileBaseDamageProperty: {handler.Name}");
                _instance.Register(handler);
                Plugin.Logger.LogDebug($"Registered ProjectileBaseDamageProperty, count now: {_instance.Count}");
                */
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Failed to register ProjectileBaseDamageProperty: {ex.Message}");
            }

            Plugin.Logger.LogInfo($"Registered {_instance.Count} projectile property handlers");
        }
    }
}
