// Config/BepInEx/Core/ConfigSystemInitializer.cs
//
// PURPOSE: Orchestrates two-phase initialization of BepInEx config systems.
//
// USAGE:
// - InitializeAll(): Phase 1 - binds ConfigEntry properties to ConfigFile
// - ApplyAll(): Phase 2 - subscribes to event hooks and applies API changes
//
// IMPORTANT FOR AI AGENTS:
// - Two-phase initialization prevents order issues (all entries bound before events)
// - Extracted from BepInExConfigManager to reduce class size
// - Returns counts of initialized/applied systems for logging
// - Handles errors gracefully (continues with other systems if one fails)
//
using System;
using BepInEx.Configuration;

namespace CrusaderDETweaker.Config.BepInEx.Core
{
    /// <summary>
    /// Handles initialization and application of BepInEx config systems.
    /// 
    /// Extracted from BepInExConfigManager to reduce class size.
    /// 
    /// Provides two-phase initialization:
    /// 1. InitializeAll(): Binds ConfigEntry properties to ConfigFile
    /// 2. ApplyAll(): Subscribes to event hooks and applies runtime modifications
    /// 
    /// The two-phase approach ensures all config entries are bound before any
    /// event hooks are registered, preventing initialization order issues.
    /// </summary>
    internal static class ConfigSystemInitializer
    {
        /// <summary>
        /// Initialize all config systems (bind ConfigEntry properties).
        /// </summary>
        public static int InitializeAll(IBepInExConfigSystem[] systems, ConfigFile config)
        {
            Plugin.Logger.LogInfo("Binding BepInEx config entries...");
            int initializedCount = 0;
            foreach (var system in systems)
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
            Plugin.Logger.LogInfo($"Initialized {initializedCount}/{systems.Length} BepInEx config systems");
            return initializedCount;
        }

        /// <summary>
        /// Apply all config systems (subscribe to event hooks, apply API changes).
        /// </summary>
        public static int ApplyAll(IBepInExConfigSystem[] systems)
        {
            Plugin.Logger.LogInfo("Applying BepInEx config systems...");
            int appliedCount = 0;
            foreach (var system in systems)
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
            Plugin.Logger.LogInfo($"Applied {appliedCount}/{systems.Length} BepInEx config systems");
            return appliedCount;
        }
    }
}

