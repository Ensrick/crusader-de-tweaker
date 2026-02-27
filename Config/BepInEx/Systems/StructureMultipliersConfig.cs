// Config/BepInEx/Systems/StructureMultipliersConfig.cs
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Config.BepInEx.Systems.Handlers;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages structure-related damage multipliers (global, wall, tower, civil structures).
    /// Uses real-time event hooks to apply multipliers as structures take damage.
    /// </summary>
    internal class StructureMultipliersConfig : IBepInExConfigSystem
    {
        public string Name => "Structure Multipliers";

        public bool IsInitialized { get; private set; }

        public ConfigEntry<float> GlobalDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> WallDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> TowerDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> CivilStructureDamageTakenMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            GlobalDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "StructureDamageTakenMultiplier",
                1.0f,
                "Global multiplier for damage taken by ALL structures (walls, towers, buildings, etc.).\n" +
                "Example: Set to 1.5 to increase all structure damage by 50%, or 0.75 to reduce it by 25%.\n" +
                "Note: More specific multipliers (walls, towers, etc.) are applied first, then this global multiplier.\n" +
                "Tip: Changes apply in real-time (no restart needed)."
            );

            WallDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "WallDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by walls (stone walls, crenel walls).\n" +
                "Example: Set to 2.0 to make walls take double damage, or 0.5 to make them more durable.\n" +
                "Note: Applied before the global StructureDamageTakenMultiplier.\n" +
                "Tip: Use this to fine-tune wall durability without affecting other structures."
            );

            TowerDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "TowerDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by towers and gatehouses.\n" +
                "Affects: Tower levels 1-5, destroyed tower remnants, and gatehouses.\n" +
                "Example: Set to 1.2 to make towers 20% more vulnerable, or 0.8 to make them 20% more durable.\n" +
                "Note: Applied before the global StructureDamageTakenMultiplier.\n" +
                "Tip: Useful for balancing siege gameplay."
            );

            CivilStructureDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "CivilStructureDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by civilian structures (buildings, houses, workshops, etc.).\n" +
                "Does NOT affect: Walls, towers, or gatehouses.\n" +
                "Example: Set to 0.5 to make buildings more resilient, or 1.5 to make them more vulnerable.\n" +
                "Note: Applied before the global StructureDamageTakenMultiplier.\n" +
                "Tip: Use this to balance economic structure durability."
            );

            ValidateMultipliers();
            IsInitialized = true;
        }

        public void Apply()
        {
            StructureDamageMultiplierHandler.Subscribe(
                GlobalDamageTakenMultiplier,
                WallDamageTakenMultiplier,
                TowerDamageTakenMultiplier,
                CivilStructureDamageTakenMultiplier);
        }

        /// <summary>
        /// Validates all structure multiplier values and logs warnings for invalid values.
        /// </summary>
        private void ValidateMultipliers()
        {
            BepInExConfigHelper.ValidateMultipliers(
                ("StructureDamageTakenMultiplier", GlobalDamageTakenMultiplier),
                ("WallDamageTakenMultiplier", WallDamageTakenMultiplier),
                ("TowerDamageTakenMultiplier", TowerDamageTakenMultiplier),
                ("CivilStructureDamageTakenMultiplier", CivilStructureDamageTakenMultiplier)
            );
        }
    }
}

