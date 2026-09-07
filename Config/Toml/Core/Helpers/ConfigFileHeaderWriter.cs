// Config/Toml/Core/ConfigFileHeaderWriter.cs
//
// PURPOSE: Generates standard header comments for TOML config files.
//
// USAGE:
// - WriteHeader(): Called by ConfigGenerator to write file headers
// - Writes property descriptions and usage instructions
// - Different headers for units vs structures
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just header generation
// - Headers explain what each property does and how to use the config file
// - Extracted from ConfigGenerator to improve modularity
// - Headers are user-facing documentation in the config files themselves
//
using System.Text;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Writes header comments for TOML configuration files.
    /// 
    /// Generates user-friendly headers that explain:
    /// - What the config file is for
    /// - What each property does
    /// - How to modify values
    /// - Where to find more documentation
    /// 
    /// Used by ConfigGenerator when creating default config files.
    /// </summary>
    internal static class ConfigFileHeaderWriter
    {
        /// <summary>
        /// Write a header comment explaining what this config file is for.
        /// </summary>
        public static void WriteHeader(StringBuilder sb, string entityTypeName)
        {
            sb.AppendLine("# ========================================");
            sb.AppendLine($"# Crusader DE Tweaker - {entityTypeName.ToUpperInvariant()} Configuration");
            sb.AppendLine("# ========================================");
            sb.AppendLine("#");
            if (entityTypeName == "unit")
            {
                sb.AppendLine("# This file allows you to customize game unit properties.");
                sb.AppendLine("# Each section [CHIMP_TYPE_...] contains properties for that specific unit.");
            }
            else
            {
                sb.AppendLine("# This file allows you to customize game structure properties.");
                sb.AppendLine("# Each section [STRUCT_...] contains properties for that specific structure.");
            }
            sb.AppendLine("#");
            sb.AppendLine("# PROPERTY DESCRIPTIONS:");
            sb.AppendLine("#");
            
            if (entityTypeName == "unit")
            {
                WriteUnitHeader(sb);
            }
            else // structure
            {
                WriteStructureHeader(sb);
            }
            
            sb.AppendLine("#");
            sb.AppendLine("# ========================================");
            sb.AppendLine();
        }

        private static void WriteUnitHeader(StringBuilder sb)
        {
            sb.AppendLine("# ========================================");
            sb.AppendLine("# BASIC PROPERTIES");
            sb.AppendLine("# ========================================");
            sb.AppendLine("# Health:        Maximum hit points");
            sb.AppendLine("# Speed:         Movement speed (smaller values = faster)");
            sb.AppendLine("# GoldCost:      Recruitment cost in gold (Crusader units only)");
            sb.AppendLine("# WeaponType:    Equipment slot 1 — resource required to recruit (STORED_SWORDS, STORED_BOWS, STORED_CROSSBOWS,");
            sb.AppendLine("#                  STORED_SPEARS, STORED_PIKES, STORED_MACES). \"NONE\" = no weapon needed (gold-only hire, like");
            sb.AppendLine("#                  Arabian mercenaries). Deleting the line does NOT do that: a missing key = \"use game default\".");
            sb.AppendLine("# ArmorType:     Equipment slot 2 — resource required to recruit (STORED_METAL_ARMOUR, STORED_LEATHER_ARMOUR, or \"NONE\")");
            sb.AppendLine("# RequiresHorse: true = hiring needs a free stable horse and the unit occupies that slot until it dies.");
            sb.AppendLine("#                  Add \"RequiresHorse = true\" to any recruitable unit's section - Barracks units are checked by");
            sb.AppendLine("#                  the game, Mercenary Post units (Horse Archer, Camel Lancer, ...) by the mod at hire time.");
            sb.AppendLine("# ShieldHealth:  Shield durability for the Bedouin Demolisher");
            sb.AppendLine("# MaxCount:      Limit on units of this type alive at once for the local player.");
            sb.AppendLine("#                  -1 = unlimited (default), 0 = disabled (every spawn removed), >0 = max alive. Excess removed on spawn.");
            sb.AppendLine("#");
            sb.AppendLine("# ========================================");
            sb.AppendLine("# DAMAGE SYSTEM");
            sb.AppendLine("# ========================================");
            sb.AppendLine("# Damage values are configured via CSV matrix files in the DamageMatrices folder:");
            sb.AppendLine("#   - CrusaderDETweaker_MeleeDamage.csv      — unit-vs-unit melee damage");
            sb.AppendLine("#   - CrusaderDETweaker_RangedDamage.csv     — projectile-vs-unit ranged damage");
            sb.AppendLine("#   - CrusaderDETweaker_EunuchAoeDamage.csv  — Eunuch AoE damage");
            sb.AppendLine("#   - CrusaderDETweaker_BallistaDamage.csv   — Ballista projectile damage");
            sb.AppendLine("#   - CrusaderDETweaker_UnitFireDamage.csv   — fire damage taken by units");
            sb.AppendLine("#   - CrusaderDETweaker_BuildingFireDamage.csv — fire damage taken by buildings");
            sb.AppendLine("#   - CrusaderDETweaker_BedouinHeal.csv      — Bedouin Healer heal amounts");
            sb.AppendLine("#");
            sb.AppendLine("# HOW VALUES WORK:");
            sb.AppendLine("#   A value of -1 means \"use the game default\" — the mod leaves that property UNCHANGED.");
            sb.AppendLine("#   Set any other number to override it. The '# Default:' comment after each value shows the");
            sb.AppendLine("#   game's current default and is refreshed every launch.");
            sb.AppendLine("#   (Applies to Health, Speed, GoldCost, ShieldHealth and armor multipliers. WeaponType,");
            sb.AppendLine("#    ArmorType and RequiresHorse are written directly (\"NONE\" removes an equipment requirement).");
            sb.AppendLine("#    MaxCount: see above — there -1 = unlimited.)");
        }

        private static void WriteStructureHeader(StringBuilder sb)
        {
            sb.AppendLine("# Health: Maximum hit points");
            sb.AppendLine("# GoldCost, WoodCost, StoneCost, IronCost, PitchCost: Building costs");
            sb.AppendLine("# HousingPopulationSpace: Population capacity (housing structures only)");
            sb.AppendLine("# MaxCount: Limit on buildings of this type placed at once for the local player.");
            sb.AppendLine("#            -1 = unlimited (default), 0 = disabled (every placement removed), >0 = max placed. Excess removed on placement.");
            sb.AppendLine("#");
            sb.AppendLine("# HOW VALUES WORK:");
            sb.AppendLine("#   A value of -1 means \"use the game default\" — the mod leaves that property UNCHANGED.");
            sb.AppendLine("#   Set any other number to override it. The '# Default:' comment shows the game's current");
            sb.AppendLine("#   default, refreshed every launch. (MaxCount: see above — there -1 = unlimited.)");
        }
    }
}

