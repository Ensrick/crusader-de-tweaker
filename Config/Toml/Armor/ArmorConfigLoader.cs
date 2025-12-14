// Config/Toml/Armor/ArmorConfigLoader.cs
using System;
using System.IO;
using BepInEx;
using CrusaderDETweaker.Config.Toml.Core;
using Tomlyn;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Toml.Armor
{
    /// <summary>
    /// Loads the Armor TOML configuration file.
    /// </summary>
    internal static class ArmorConfigLoader
    {
        private static string FilePath => Path.Combine(Paths.ConfigPath, "CrusaderDETweaker_Armor.toml");

        /// <summary>
        /// Load the armor configuration from TOML.
        /// </summary>
        public static void Load()
        {
            if (!ConfigFileHelper.ConfigFileExists(FilePath))
            {
                ConfigHelpers.ErrorLogging.LogMissingConfigFile(FilePath, "Using default armor values");
                return;
            }

            try
            {
                string tomlContent = ConfigFileHelper.ReadConfigFile(FilePath);
                var model = Tomlyn.Toml.ToModel(tomlContent);

                var config = ArmorConfig.Instance;

                // Load ranged armor modifiers
                if (model.TryGetValue(ArmorConfigConstants.RangedArmorSection, out var rangedObj) && rangedObj is TomlTable rangedTable)
                {
                    LoadRangedArmorModifiers(rangedTable, config);
                }

                // Load melee armor modifiers
                if (model.TryGetValue(ArmorConfigConstants.MeleeArmorSection, out var meleeObj) && meleeObj is TomlTable meleeTable)
                {
                    LoadMeleeArmorModifiers(meleeTable, config);
                }

                // Load Eunuch AOE armor modifiers
                if (model.TryGetValue(ArmorConfigConstants.EunuchAoeArmorSection, out var eunuchObj) && eunuchObj is TomlTable eunuchTable)
                {
                    LoadEunuchAoeArmorModifiers(eunuchTable, config);
                }

                Plugin.Logger.LogInfo("Loaded Armor configuration");
            }
            catch (Exception ex)
            {
                ConfigHelpers.ErrorLogging.LogConfigLoadException("Armor", ex);
                Plugin.Logger.LogInfo("Using default armor values");
            }
        }

        private static void LoadRangedArmorModifiers(TomlTable rangedTable, ArmorConfig config)
        {
            foreach (var projectileType in ArmorConfigConstants.ProjectileTypes)
            {
                if (rangedTable.TryGetValue(projectileType, out var projObj) && projObj is TomlTable projTable)
                {
                    LoadRangedArmorForProjectile(projectileType, projTable, config);
                }
            }
        }

        private static void LoadRangedArmorForProjectile(string projectileType, TomlTable table, ArmorConfig config)
        {
            foreach (var armorType in ArmorConfigConstants.ArmorTypes)
            {
                if (table.TryGetValue(armorType, out var valueObj) && ConfigHelpers.TryConvertToFloat(valueObj, out var modifier))
                {
                    config.SetRangedArmorModifier(projectileType, armorType, modifier);
                    Plugin.Logger.LogInfo($"  Ranged: {projectileType} vs {armorType}: {modifier}x");
                }
            }
        }

        private static void LoadMeleeArmorModifiers(TomlTable meleeTable, ArmorConfig config)
        {
            // Melee weapon types can be added dynamically
            // For now, look for common weapon types
            foreach (var weaponType in ArmorConfigConstants.WeaponTypes)
            {
                if (meleeTable.TryGetValue(weaponType, out var weapObj) && weapObj is TomlTable weapTable)
                {
                    LoadMeleeArmorForWeapon(weaponType, weapTable, config);
                }
            }
        }

        private static void LoadMeleeArmorForWeapon(string weaponType, TomlTable table, ArmorConfig config)
        {
            foreach (var armorType in ArmorConfigConstants.ArmorTypes)
            {
                if (table.TryGetValue(armorType, out var valueObj) && ConfigHelpers.TryConvertToFloat(valueObj, out var modifier))
                {
                    config.SetMeleeArmorModifier(weaponType, armorType, modifier);
                    Plugin.Logger.LogInfo($"  Melee: {weaponType} vs {armorType}: {modifier}x");
                }
            }
        }

        private static void LoadEunuchAoeArmorModifiers(TomlTable table, ArmorConfig config)
        {
            foreach (var armorType in ArmorConfigConstants.ArmorTypes)
            {
                if (table.TryGetValue(armorType, out var valueObj) && ConfigHelpers.TryConvertToFloat(valueObj, out var modifier))
                {
                    config.SetEunuchAoeArmorModifier(armorType, modifier);
                    Plugin.Logger.LogInfo($"  EunuchAOE vs {armorType}: {modifier}x");
                }
            }
        }
    }
}