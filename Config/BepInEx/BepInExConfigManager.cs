// Config/BepInEx/BepInExConfigManager.cs
using System;
using System.Linq;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Config.BepInEx.Systems;

namespace CrusaderDETweaker.Config.BepInEx
{
    /// <summary>
    /// Manages initialization of all BepInEx configuration systems.
    /// BepInEx configs use ConfigFile and ConfigEntry, and often require real-time event hooks.
    /// Similar to ConfigManager but for BepInEx-specific configurations.
    /// </summary>
    internal static class BepInExConfigManager
    {
        private static readonly IBepInExConfigSystem[] ConfigSystems = new IBepInExConfigSystem[]
        {
            new UnitMultipliersConfig(),
            new StructureMultipliersConfig(),
            new WallCostConfig()
        };

        /// <summary>
        /// Initialize all BepInEx configuration systems.
        /// First binds all ConfigEntry properties, then applies them (subscribes to event hooks, etc.).
        /// </summary>
        /// <param name="config">The BepInEx ConfigFile to bind entries to</param>
        internal static void Initialize(ConfigFile config)
        {
            Plugin.Logger.LogInfo($"Initializing {ConfigSystems.Length} BepInEx config systems...");

            // Initialize all systems (bind ConfigEntry properties)
            Plugin.Logger.LogInfo("Binding BepInEx config entries...");
            int initializedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    system.Initialize(config);
                    initializedCount++;
                    Plugin.Logger.LogDebug($"  Initialized {system.Name}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to initialize {system.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            Plugin.Logger.LogInfo($"Initialized {initializedCount}/{ConfigSystems.Length} BepInEx config systems");

            // Apply all systems (subscribe to event hooks, apply API changes, etc.)
            Plugin.Logger.LogInfo("Applying BepInEx config systems...");
            int appliedCount = 0;
            foreach (var system in ConfigSystems)
            {
                try
                {
                    system.Apply();
                    appliedCount++;
                    Plugin.Logger.LogDebug($"  Applied {system.Name}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"  Failed to apply {system.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            Plugin.Logger.LogInfo($"Applied {appliedCount}/{ConfigSystems.Length} BepInEx config systems");

            // Set config system instances in legacy ConfigManagerBepinex for backward compatibility
#pragma warning disable CS0618 // Type or member is obsolete
            var unitConfig = ConfigSystems.OfType<UnitMultipliersConfig>().FirstOrDefault();
            var structureConfig = ConfigSystems.OfType<StructureMultipliersConfig>().FirstOrDefault();
            var wallCostConfig = ConfigSystems.OfType<WallCostConfig>().FirstOrDefault();
            ConfigManagerBepinex.SetConfigSystems(unitConfig, structureConfig, wallCostConfig);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}

