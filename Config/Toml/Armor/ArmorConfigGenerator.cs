// Config/Toml/Armor/ArmorConfigGenerator.cs
using System;
using System.IO;
using System.Text;
using BepInEx;
using CrusaderDETweaker.Config.Toml.Core;

namespace CrusaderDETweaker.Config.Toml.Armor
{
    /// <summary>
    /// Generates the default Armor TOML configuration file.
    /// </summary>
    internal static class ArmorConfigGenerator
    {
        private static string FilePath => Path.Combine(Paths.ConfigPath, "CrusaderDETweaker_Armor.toml");

        /// <summary>
        /// Generate the default armor configuration file.
        /// </summary>
        public static void GenerateDefault()
        {
            if (ConfigFileHelper.ConfigFileExists(FilePath))
            {
                Plugin.Logger.LogInfo($"Armor config already exists: {FilePath}");
                return;
            }

            try
            {
                var config = ArmorConfig.Instance;
                var sb = new StringBuilder();

                sb.AppendLine("# CrusaderDETweaker - Armor Configuration");
                sb.AppendLine("# Controls how different weapon/projectile types interact with armor types");
                sb.AppendLine("# These are MULTIPLIERS applied to damage calculations");
                sb.AppendLine();

                // Ranged Armor Modifiers
                sb.AppendLine("# ============================================");
                sb.AppendLine("# RANGED ARMOR MODIFIERS");
                sb.AppendLine("# ============================================");
                sb.AppendLine("# Formula: FinalDamage = BaseProjectileDamage × UnitArmorValue × RangedArmorModifier");
                sb.AppendLine();

                foreach (var projectileType in ArmorConfigConstants.ProjectileTypes)
                {
                    WriteRangedArmorSection(sb, projectileType, config);
                }

                // Melee Armor Modifiers
                sb.AppendLine("# ============================================");
                sb.AppendLine("# MELEE ARMOR MODIFIERS");
                sb.AppendLine("# ============================================");
                sb.AppendLine("# Formula: FinalDamage = BaseMeleeDamage × UnitArmorValue × MeleeArmorModifier");
                sb.AppendLine("# Currently all set to 1.0 (neutral) - can be tuned via verifier");
                sb.AppendLine();
                sb.AppendLine("# Example: Maces vs Heavy (blunt force might be better vs Heavy)");
                sb.AppendLine("# [MeleeArmor.Mace]");
                sb.AppendLine("# Heavy = 1.2");
                sb.AppendLine();

                // Eunuch AOE Armor Modifiers
                sb.AppendLine("# ============================================");
                sb.AppendLine("# EUNUCH AOE ARMOR MODIFIERS");
                sb.AppendLine("# ============================================");
                sb.AppendLine("# Similar to Sling (blunt AOE damage)");
                sb.AppendLine();
                WriteEunuchAoeArmorSection(sb, config);

                ConfigFileHelper.WriteConfigFile(FilePath, sb.ToString());
                Plugin.Logger.LogInfo($"Generated default Armor config: {FilePath}");
            }
            catch (Exception ex)
            {
                ConfigHelpers.ErrorLogging.LogConfigGenerationException("Armor config", ex);
            }
        }

        private static void WriteRangedArmorSection(StringBuilder sb, string projectileType, ArmorConfig config)
        {
            sb.AppendLine($"[{ArmorConfigConstants.RangedArmorSection}.{projectileType}]");

            foreach (var armorType in ArmorConfigConstants.ArmorTypes)
            {
                float modifier = config.GetRangedArmorModifier(projectileType, armorType);
                string comment = GetRangedArmorComment(projectileType, armorType);
                sb.AppendLine($"{armorType} = {modifier}  # {comment}");
            }

            sb.AppendLine();
        }

        private static void WriteEunuchAoeArmorSection(StringBuilder sb, ArmorConfig config)
        {
            sb.AppendLine($"[{ArmorConfigConstants.EunuchAoeArmorSection}]");

            foreach (var armorType in ArmorConfigConstants.ArmorTypes)
            {
                float modifier = config.GetEunuchAoeArmorModifier(armorType);
                sb.AppendLine($"{armorType} = {modifier}");
            }

            sb.AppendLine();
        }

        private static string GetRangedArmorComment(string projectile, string armor)
        {
            if (projectile == "Crossbow" && armor == "Heavy")
                return "Crossbows PIERCE Heavy armor!";
            if (projectile == "Bow" && armor == "Heavy")
                return "Arrows stopped by Heavy";
            if (armor == "None")
                return "No armor protection";

            return $"{projectile} vs {armor}";
        }
    }
}