// Config/BepInEx/ConfigManagerBepinex.cs
// DEPRECATED: This file is kept for backward compatibility.
// New code should use BepInExConfigManager and the individual config systems.
// This file will be removed in a future version.

using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Systems;

namespace CrusaderDETweaker
{
    /// <summary>
    /// DEPRECATED: Legacy wrapper for backward compatibility.
    /// Use BepInExConfigManager and individual config systems instead.
    /// </summary>
    [System.Obsolete("Use BepInExConfigManager and individual config systems instead")]
    internal static class ConfigManagerBepinex
    {
        private static UnitMultipliersConfig _unitConfig;
        private static StructureMultipliersConfig _structureConfig;
        private static WallCostConfig _wallCostConfig;

        internal static ConfigFile Config { get; private set; }

        // Legacy property accessors for backward compatibility
        internal static ConfigEntry<float> UnitMeleeDamageTakenMultiplier => _unitConfig?.MeleeDamageTakenMultiplier;
        internal static ConfigEntry<float> UnitRangedDamageTakenMultiplier => _unitConfig?.RangedDamageTakenMultiplier;
        internal static ConfigEntry<float> UnitHealthMultiplier => _unitConfig?.HealthMultiplier;
        internal static ConfigEntry<float> StructureDamageTakenMultiplier => _structureConfig?.GlobalDamageTakenMultiplier;
        internal static ConfigEntry<float> WallDamageTakenMultiplier => _structureConfig?.WallDamageTakenMultiplier;
        internal static ConfigEntry<float> TowerDamageTakenMultiplier => _structureConfig?.TowerDamageTakenMultiplier;
        internal static ConfigEntry<float> CivilStructureDamageTakenMultiplier => _structureConfig?.CivilStructureDamageTakenMultiplier;
        internal static ConfigEntry<float> LowWallCostMultiplier => _wallCostConfig?.LowWallCostMultiplier;
        internal static ConfigEntry<float> HighWallCostMultiplier => _wallCostConfig?.HighWallCostMultiplier;

        /// <summary>
        /// DEPRECATED: Initialize is now handled by BepInExConfigManager.
        /// This method exists for backward compatibility only.
        /// </summary>
        [System.Obsolete("Use BepInExConfigManager.Initialize() instead")]
        internal static void Initialize(ConfigFile config)
        {
            Config = config;

            // Initialize config systems (for backward compatibility)
            // Note: BepInExConfigManager.Initialize() should be called instead
            if (_unitConfig == null)
            {
                _unitConfig = new UnitMultipliersConfig();
                _structureConfig = new StructureMultipliersConfig();
                _wallCostConfig = new WallCostConfig();

                _unitConfig.Initialize(config);
                _structureConfig.Initialize(config);
                _wallCostConfig.Initialize(config);
            }
        }

        /// <summary>
        /// DEPRECATED: Apply is now handled by BepInExConfigManager.
        /// This method exists for backward compatibility only.
        /// </summary>
        [System.Obsolete("Use BepInExConfigManager.Initialize() instead (it calls Apply automatically)")]
        internal static void ApplyAllMultiplierConfigs()
        {
            // If systems weren't initialized via Initialize(), try to get them from BepInExConfigManager
            // Otherwise apply them directly
            _unitConfig?.Apply();
            _structureConfig?.Apply();
            _wallCostConfig?.Apply();
        }

        /// <summary>
        /// Internal method to set config system instances (used by BepInExConfigManager).
        /// </summary>
        internal static void SetConfigSystems(UnitMultipliersConfig unitConfig, StructureMultipliersConfig structureConfig, WallCostConfig wallCostConfig)
        {
            _unitConfig = unitConfig;
            _structureConfig = structureConfig;
            _wallCostConfig = wallCostConfig;
        }
    }
}
