// Config/BepInEx/Systems/WallCostConfig.cs
using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages wall cost multipliers (low and high walls).
    /// Wall costs are applied directly to the game API during initialization.
    /// </summary>
    internal class WallCostConfig : IBepInExConfigSystem
    {
        public string Name => "Wall Cost";

        public bool IsInitialized { get; private set; }

        public ConfigEntry<float> LowWallCostMultiplier { get; private set; }
        public ConfigEntry<float> HighWallCostMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            // Read current values from API to use as defaults (game defaults: Low=0.25, High=0.5)
            float currentLowWallMultiplier = 0.25f;
            float currentHighWallMultiplier = 0.5f;
            try
            {
                currentLowWallMultiplier = Plugin.BuildingApi.GetLowWallCostMultiplier();
                currentHighWallMultiplier = Plugin.BuildingApi.GetHighWallCostMultiplier();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Could not read current wall cost multipliers from API, using defaults: {ex.Message}");
            }

            LowWallCostMultiplier = config.Bind(
                "Multipliers",
                "LowWallCostMultiplier",
                currentLowWallMultiplier,
                "Cost multiplier for low/short walls (stone walls at low height, stairs). Game default: 0.25"
            );

            HighWallCostMultiplier = config.Bind(
                "Multipliers",
                "HighWallCostMultiplier",
                currentHighWallMultiplier,
                "Cost multiplier for high walls (stone walls at high height, crenel walls). Game default: 0.5"
            );

            ValidateMultipliers();
            IsInitialized = true;
        }

        public void Apply()
        {
            ApplyWallCostMultipliers();
        }

        /// <summary>
        /// Validates all wall cost multiplier values and logs warnings for invalid values.
        /// </summary>
        private void ValidateMultipliers()
        {
            BepInExConfigHelper.ValidateMultipliers(
                ("LowWallCostMultiplier", LowWallCostMultiplier),
                ("HighWallCostMultiplier", HighWallCostMultiplier)
            );
        }

        /// <summary>
        /// Applies wall cost multipliers from BepInEx config to the game API.
        /// Wall costs are based on height - low walls use LowWallCostMultiplier, high walls use HighWallCostMultiplier.
        /// </summary>
        private void ApplyWallCostMultipliers()
        {
            try
            {
                Plugin.BuildingApi.SetLowWallCostMultiplier(LowWallCostMultiplier.Value);
                Plugin.BuildingApi.SetHighWallCostMultiplier(HighWallCostMultiplier.Value);
                Plugin.Logger.LogInfo($"Applied wall cost multipliers: Low={LowWallCostMultiplier.Value}, High={HighWallCostMultiplier.Value}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to apply wall cost multipliers: {ex.Message}");
            }
        }
    }
}

