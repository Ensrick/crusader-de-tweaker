// Config/BepInEx/Systems/UnitMultipliersConfig.cs
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Config.BepInEx.Systems.Handlers;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages unit-related multipliers (melee damage, ranged damage, health).
    /// Uses real-time event hooks to apply multipliers as units take damage or are created.
    /// </summary>
    internal class UnitMultipliersConfig : IBepInExConfigSystem
    {
        public string Name => "Unit Multipliers";

        public ConfigEntry<float> MeleeDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> RangedDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> HealthMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            MeleeDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitMeleeDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all melee damage taken by units.\n" +
                "Example: Set to 1.5 to increase all melee damage by 50%, or 0.75 to reduce it by 25%.\n" +
                "Note: This multiplies the final calculated damage. Minimum damage is always 1 (game uses default if damage is 0).\n" +
                "Tip: For fine-grained control, edit BaseMeleeDamage and ArmorValue in the TOML config files instead."
            );

            RangedDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "UnitRangedDamageTakenMultiplier",
                1.0f,
                "Global multiplier for all ranged damage taken by units.\n" +
                "Affects all projectile types: Arrow, Bolt, Slinger, and Javelin.\n" +
                "Example: Set to 1.2 to increase all ranged damage by 20%, or 0.8 to reduce it by 20%.\n" +
                "Note: This multiplies the final calculated damage. Minimum damage is always 1 (game uses default if damage is 0).\n" +
                "Tip: Changes apply in real-time (no restart needed). For unit-specific changes, edit RangedArmorValue in TOML config."
            );

            HealthMultiplier = config.Bind(
                "Multipliers",
                "UnitHealthMultiplier",
                1.0f,
                "Global multiplier for unit max health.\n" +
                "Example: Set to 1.5 to increase all unit health by 50%, or 0.75 to reduce it by 25%.\n" +
                "Note: Changes apply to newly created units (existing units keep their current health).\n" +
                "Tip: For unit-specific changes, edit Health property in the TOML config files."
            );

            ValidateMultipliers();
        }

        public void Apply()
        {
            MeleeDamageMultiplierHandler.Subscribe(MeleeDamageTakenMultiplier);
            RangedDamageMultiplierHandler.Subscribe(RangedDamageTakenMultiplier);
            HealthMultiplierHandler.Subscribe(HealthMultiplier);
        }

        /// <summary>
        /// Validates all unit multiplier values and logs warnings for invalid values.
        /// </summary>
        private void ValidateMultipliers()
        {
            BepInExConfigHelper.ValidateMultipliers(
                ("UnitMeleeDamageTakenMultiplier", MeleeDamageTakenMultiplier),
                ("UnitRangedDamageTakenMultiplier", RangedDamageTakenMultiplier),
                ("UnitHealthMultiplier", HealthMultiplier)
            );
        }
    }
}

