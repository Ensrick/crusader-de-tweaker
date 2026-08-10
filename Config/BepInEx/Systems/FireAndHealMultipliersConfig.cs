// Config/BepInEx/Systems/FireAndHealMultipliersConfig.cs
// AI DEV: BepInEx config system for fire damage, Bedouin heal, and disease damage multipliers.
// Fire/heal act per-unit via UnitApi/BuildingApi and are applied once at initialization, on top of
// the values already set by TOML globals and CSV matrices.
// Disease is the exception: DiseaseDamageMultiplier is bound + validated here but APPLIED in
// ConfigLoader.LoadGameGlobals (each map load, on top of the [Disease] TOML base) because the
// disease tiers are global immediates that LoadGameGlobals rewrites every map load — applying the
// multiplier at init here would be clobbered (and would write a game global during init, which
// risks a native ACCESS_VIOLATION).

using System;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using SHCDESE.Interop;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages fire damage and Bedouin heal multipliers.
    /// Applied once at initialization (static API modification, not an event hook).
    /// Multipliers act on values already set by TOML and CSV systems.
    /// </summary>
    internal class FireAndHealMultipliersConfig : IBepInExConfigSystem
    {
        public string Name => "Fire & Heal Multipliers";

        public ConfigEntry<float> UnitFireDamageMultiplier { get; private set; }
        public ConfigEntry<float> StructureFireDamageMultiplier { get; private set; }
        public ConfigEntry<float> BedouinHealMultiplier { get; private set; }
        public ConfigEntry<float> DiseaseDamageMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            UnitFireDamageMultiplier = config.Bind(
                "Multipliers",
                "UnitFireDamageTakenMultiplier",
                1.0f,
                "Global multiplier for fire damage taken by all units.\n" +
                "Example: Set to 2.0 to make units twice as vulnerable to fire, or 0.5 to halve fire damage.\n" +
                "Applied once at startup (requires game restart to change). For per-unit values, edit UnitFireDamage.csv."
            );

            StructureFireDamageMultiplier = config.Bind(
                "Multipliers",
                "StructureFireDamageTakenMultiplier",
                1.0f,
                "Global multiplier for fire damage taken by all structures (buildings, towers, etc.).\n" +
                "Example: Set to 2.0 to make buildings burn faster, or 0.5 to make them more fire-resistant.\n" +
                "Applied once at startup (requires game restart to change). For per-building values, edit BuildingFireDamage.csv."
            );

            BedouinHealMultiplier = config.Bind(
                "Multipliers",
                "BedouinHealMultiplier",
                1.0f,
                "Global multiplier for the healing amount provided by Bedouin healers to all unit types.\n" +
                "Example: Set to 1.5 to increase healing by 50%, or 0.75 to reduce it by 25%.\n" +
                "Applied once at startup (requires game restart to change). For per-unit values, edit BedouinHeal.csv."
            );

            DiseaseDamageMultiplier = config.Bind(
                "Multipliers",
                "DiseaseDamageMultiplier",
                1.0f,
                "Global multiplier for all three disease damage tiers (DiseaseDamage1/2/3).\n" +
                "Example: Set to 2.0 to double disease damage, or 0.5 to halve it.\n" +
                "Applied on each map load, on top of the [Disease] base in GameplaySettings.toml."
            );

            BepInExConfigHelper.ValidateMultipliers(
                ("UnitFireDamageTakenMultiplier", UnitFireDamageMultiplier),
                ("StructureFireDamageTakenMultiplier", StructureFireDamageMultiplier),
                ("BedouinHealMultiplier", BedouinHealMultiplier),
                ("DiseaseDamageMultiplier", DiseaseDamageMultiplier)
            );
        }

        public void Apply()
        {
            ApplyUnitFireDamage();
            ApplyStructureFireDamage();
            ApplyBedouinHeal();
            // NOTE: DiseaseDamageMultiplier is intentionally NOT applied here. It is applied in
            // ConfigLoader.LoadGameGlobals (on each map load, on top of the [Disease] TOML base).
            // Applying it here was doubly broken: (1) it wrote game globals during LibraryLoaded
            // init, which is the documented ACCESS_VIOLATION hazard; (2) LoadGameGlobals re-wrote
            // the raw TOML disease values on the first map load, clobbering this multiplier so it
            // never took effect. See ApplyDiseaseDamage removal note below.
        }

        private void ApplyUnitFireDamage()
        {
            if (Mathf.Approximately(UnitFireDamageMultiplier.Value, 1.0f)) return;

            int applied = 0;
            foreach (var unit in UnitMatrixHelper.GetModifiableUnits())
            {
                try
                {
                    int current = Plugin.UnitApi.GetFireDamage(unit);
                    int modified = Mathf.Max(1, (int)(current * UnitFireDamageMultiplier.Value));
                    Plugin.UnitApi.SetFireDamage(unit, modified);
                    applied++;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Failed to apply unit fire damage multiplier for {unit}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Applied UnitFireDamageTakenMultiplier={UnitFireDamageMultiplier.Value} to {applied} units");
        }

        private void ApplyStructureFireDamage()
        {
            if (Mathf.Approximately(StructureFireDamageMultiplier.Value, 1.0f)) return;

            int applied = 0;
            foreach (var building in BuildingMatrixHelper.GetModifiableBuildings())
            {
                try
                {
                    short current = Plugin.BuildingApi.GetBuildingFireDamage(building);
                    short modified = (short)Mathf.Clamp(
                        (int)(current * StructureFireDamageMultiplier.Value),
                        1, short.MaxValue);
                    Plugin.BuildingApi.SetBuildingFireDamage(building, modified);
                    applied++;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Failed to apply structure fire damage multiplier for {building}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Applied StructureFireDamageTakenMultiplier={StructureFireDamageMultiplier.Value} to {applied} buildings");
        }

        private void ApplyBedouinHeal()
        {
            if (Mathf.Approximately(BedouinHealMultiplier.Value, 1.0f)) return;

            int applied = 0;
            foreach (var unit in UnitMatrixHelper.GetModifiableUnits())
            {
                try
                {
                    int current = Plugin.UnitApi.GetBedouinHeal(unit);
                    int modified = Mathf.Max(1, (int)(current * BedouinHealMultiplier.Value));
                    Plugin.UnitApi.SetBedouinHeal(unit, modified);
                    applied++;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Failed to apply Bedouin heal multiplier for {unit}: {ex.Message}");
                }
            }
            Plugin.Logger.LogInfo($"Applied BedouinHealMultiplier={BedouinHealMultiplier.Value} to {applied} units");
        }

        // ApplyDiseaseDamage() was removed: the disease multiplier is now applied in
        // ConfigLoader.LoadGameGlobals (on each map load, on top of the [Disease] TOML base), which
        // is the only writer of DiseaseDamage1/2/3 at runtime. The old once-at-init implementation
        // here both ran a game-API write during LibraryLoaded init (ACCESS_VIOLATION hazard) and was
        // immediately overwritten by LoadGameGlobals' raw TOML write on the first map load, so the
        // multiplier had no effect. The DiseaseDamageMultiplier ConfigEntry binding + validation in
        // Initialize() are kept; only the application moved.
    }
}
