// Config/Toml/UnitTags/UnitTagsConfigGenerator.cs
//
// PURPOSE: Generates default CrusaderDETweaker_UnitTags.toml configuration file.
// Contains tag interactions for 100% damage accuracy matching original game values.
//
// ARCHITECTURE:
// - ADDITIVE STACKING: All matching tags contribute deltas that SUM together
// - Formula: FinalMultiplier = 1.0 + Σ(interaction.Multiplier - 1.0)
// - Benefits: Tag order is irrelevant (commutative), no priority bugs
// - Ranged: Projectile names (Arrow, Bolt, Slinger, Javelin) act as attacker tags
// - Melee: Unit-specific tags correct formula mismatches
// - Two-stage rounding: Round before AND after tag multiplier for accuracy
//
// ADDITIVE STACKING EXAMPLE:
// - Knight has tags [Knight, HeavyArmor]. For Bolt damage:
//   - Bolt_vs_Knight contributes (2.50 - 1.0) = +1.50 delta
//   - If other tags match, their deltas also add
//   - Final = 1.0 + 1.50 + ... = stacked multiplier
//
// FORMULA:
// - Ranged: Damage = Round50(Round50(ProjectileBase × ArmorMult) × StackedTagMult), floor 10
// - Melee:  Damage = Round5(Round5(AttackerBase × ArmorMult) × StackedTagMult), floor 2
//
// PRECISION:
// - Tag multipliers use 2 decimal places (F2 format) - sufficient given rounding
// - Ranged rounds to 50, melee rounds to 5, so 3rd/4th decimals never affect output
//
// TAG MERGING:
// - Units with identical multipliers share tags to reduce redundancy
// - Example: 44 civilian units share "Civilian" tag with Slinger×0.3333
//
// FALLBACK:
// - See UnitTagsConfigGenerator.cs.bak for the original first-match system
// - See UnitTagInteraction.cs.bak for the original registry implementation
//
// STATUS: DEACTIVATED - This generator and the entire tag system are on ice.
// The UnitTagsConfigSystem.GenerateDefaults() and Load() methods are both no-ops.
// Damage corrections are now handled exclusively via CSV matrices.
// This file is kept for reference in case the tag system is revived.

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using CrusaderDETweaker.Config.Toml.Core;

namespace CrusaderDETweaker.Config.Toml.UnitTags
{
    /// <summary>
    /// Generates default unit tags configuration file with pre-defined special case interactions.
    /// </summary>
    internal static partial class UnitTagsConfigGenerator
    {
        private struct RawInteraction
        {
            public string AttackerTag;
            public string DefenderTag;
            public string SemanticDefenderTag; // For compression analysis
            public float Delta;
            public int Rounding; // 5 for melee, 50 for ranged
            public string Group;
            public string SuggestedId;

            public RawInteraction(string attacker, string defender, float delta, int rounding, string group = null, string id = null, string semanticDefender = null)
            {
                AttackerTag = attacker;
                DefenderTag = defender;
                SemanticDefenderTag = semanticDefender ?? defender; // Default to same as defender if not provided
                Delta = delta;
                Rounding = rounding;
                Group = group;
                SuggestedId = id;
            }
        }

        private struct EquivalenceClass
        {
            public List<string> AttackerTags;
            public List<string> Defenders;
            public string DefenderTag;
            public float Delta;
            public string Group;
            public string InternalId;
            public string SuggestedId;

            public EquivalenceClass(string defenderTag, float delta, string group = null, string suggestedId = null)
            {
                AttackerTags = new List<string>();
                Defenders = new List<string>();
                DefenderTag = defenderTag;
                Delta = delta;
                Group = group;
                SuggestedId = suggestedId;
                InternalId = null;
            }
        }

        private static readonly RawInteraction[] RangedInteractions = new[]
        {
            // SHARED DEFENSE GROUPS
            new RawInteraction("Slinger", "StoneResistant", -0.50f, 50, "SHARED DEFENSE GROUPS", "Slinger_vs_StoneResistant"),

            // LORD
            new RawInteraction("Arrow", "Lord", -0.50f, 50, "LORD", "Arrow_vs_Lord"),
            new RawInteraction("Bolt", "Lord", -0.85f, 50, null, "Bolt_vs_Lord"),
            new RawInteraction("Javelin", "Lord", -0.75f, 50, null, "Javelin_vs_Lord"),
            new RawInteraction("Slinger", "Lord", -0.50f, 50, null, "Slinger_vs_Lord"),

            // HEAVY ARMOR
            new RawInteraction("Arrow", "HeavyArmor", -0.25f, 50, "HEAVY ARMOR", "Arrow_vs_HeavyArmor"),
            new RawInteraction("Javelin", "HeavyArmor", -0.50f, 50, null, "Javelin_vs_HeavyArmor"),
            new RawInteraction("Slinger", "HeavyArmor", -0.25f, 50, null, "Slinger_vs_HeavyArmor"),
            new RawInteraction("Bolt", "HeavyArmor", 1.20f, 50, null, "Bolt_vs_HeavyArmor"),

            // KNIGHT
            new RawInteraction("Bolt", "Knight", 0.30f, 50, "KNIGHT", "Bolt_vs_Knight"), // Combined with HeavyArmor delta

            // SPEARMAN
            new RawInteraction("Arrow", "Spearman", -0.03f, 50, "SPEARMAN", "Arrow_vs_Spearman"),
            new RawInteraction("Javelin", "Spearman", -0.03f, 50, null, "Javelin_vs_Spearman"),

            // PIKEMAN
            new RawInteraction("Bolt", "Pikeman", -0.40f, 50, "PIKEMAN", "Bolt_vs_Pikeman"),
            new RawInteraction("Javelin", "Pikeman", -0.25f, 50, null, "Javelin_vs_Pikeman"),

            // ARCHER
            new RawInteraction("Bolt", "Archer", 0.50f, 50, "ARCHER", "Bolt_vs_Archer"),

            // XBOWMAN
            new RawInteraction("Arrow", "Xbowman", -0.06f, 50, "XBOWMAN", "Arrow_vs_Xbowman"),
            new RawInteraction("Bolt", "Xbowman", -0.37f, 50, null, "Bolt_vs_Xbowman"),
            new RawInteraction("Javelin", "Xbowman", -0.06f, 50, null, "Javelin_vs_Xbowman"),
            new RawInteraction("Slinger", "Xbowman", -0.53f, 50, null, "Slinger_vs_Xbowman"),

            // LADDERMAN
            new RawInteraction("Bolt", "Ladderman", -0.50f, 50, "LADDERMAN", "Bolt_vs_Ladderman"),
            new RawInteraction("Javelin", "Ladderman", 0.25f, 50, null, "Javelin_vs_Ladderman"),
            new RawInteraction("Slinger", "Ladderman", 1.50f, 50, null, "Slinger_vs_Ladderman"),

            // MONK
            new RawInteraction("Bolt", "Monk", -0.65f, 50, "MONK", "Bolt_vs_Monk"),
            new RawInteraction("Javelin", "Monk", -0.25f, 50, null, "Javelin_vs_Monk"),
            new RawInteraction("Slinger", "Monk", -0.875f, 50, null, "Slinger_vs_Monk"),

            // TREBUCHET
            new RawInteraction("Slinger", "Trebuchet", -0.25f, 50, "TREBUCHET", "Slinger_vs_Trebuchet"),

            // CATAPULT
            new RawInteraction("Arrow", "Catapult", -0.25f, 50, "CATAPULT", "Arrow_vs_Catapult"),
            new RawInteraction("Bolt", "Catapult", -0.25f, 50, null, "Bolt_vs_Catapult"),
            new RawInteraction("Javelin", "Catapult", -0.25f, 50, null, "Javelin_vs_Catapult"),

            // TOWER SIEGE EQUIPMENT
            new RawInteraction("Javelin", "TowerSiegeEquipment", -0.50f, 50, "TOWER SIEGE EQUIPMENT", "Javelin_vs_TowerSiegeEquipment"),

            // MANGONEL
            new RawInteraction("Bolt", "Mangonel", -0.75f, 50, "MANGONEL", "Bolt_vs_Mangonel"),
            new RawInteraction("Slinger", "Mangonel", -0.17f, 50, null, "Slinger_vs_Mangonel"),

            // BALLISTA
            new RawInteraction("Bolt", "Ballista", -0.70f, 50, "BALLISTA", "Bolt_vs_Ballista"),

            // HEAVY SIEGE EQUIPMENT
            new RawInteraction("Bolt", "HeavySiegeEquipment", -0.47f, 50, "HEAVY SIEGE EQUIPMENT", "Bolt_vs_HeavySiegeEquipment"),
            new RawInteraction("Slinger", "HeavySiegeEquipment", -0.33f, 50, null, "Slinger_vs_HeavySiegeEquipment"),

            // ARAB SWORDSMAN
            new RawInteraction("Arrow", "ArabSwordsman", -0.25f, 50, "ARAB SWORDSMAN", "Arrow_vs_ArabSwordsman"),
            new RawInteraction("Bolt", "ArabSwordsman", 3.00f, 50, null, "Bolt_vs_ArabSwordsman"),
            new RawInteraction("Javelin", "ArabSwordsman", -0.625f, 50, null, "Javelin_vs_ArabSwordsman"),

            // ARAB ASSASSIN
            new RawInteraction("Arrow", "ArabAssassin", -0.06f, 50, "ARAB ASSASSIN", "Arrow_vs_ArabAssassin"),
            new RawInteraction("Javelin", "ArabAssassin", -0.22f, 50, null, "Javelin_vs_ArabAssassin"),
            new RawInteraction("Slinger", "ArabAssassin", -0.69f, 50, null, "Slinger_vs_ArabAssassin"),

            // ARAB BOW
            new RawInteraction("Slinger", "ArabBow", -0.75f, 50, "ARAB BOW", "Slinger_vs_ArabBow"),

            // ARAB SLINGER
            new RawInteraction("Bolt", "ArabSlinger", -0.60f, 50, "ARAB SLINGER", "Bolt_vs_ArabSlinger"),
            new RawInteraction("Javelin", "ArabSlinger", -0.37f, 50, null, "Javelin_vs_ArabSlinger"),
            new RawInteraction("Slinger", "ArabSlinger", -0.25f, 50, null, "Slinger_vs_ArabSlinger"),

            // ARAB SLAVE
            new RawInteraction("Bolt", "ArabSlave", -0.83f, 50, "ARAB SLAVE", "Bolt_vs_ArabSlave"),
            new RawInteraction("Javelin", "ArabSlave", -0.57f, 50, null, "Javelin_vs_ArabSlave"),
            new RawInteraction("Slinger", "ArabSlave", -0.43f, 50, null, "Slinger_vs_ArabSlave"),

            // LIGHT CAVALRY
            new RawInteraction("Arrow", "LightCavalry", 0.04f, 50, "LIGHT CAVALRY", "Arrow_vs_LightCavalry"),
            new RawInteraction("Bolt", "LightCavalry", 0.33f, 50, null, "Bolt_vs_LightCavalry"),
            new RawInteraction("Javelin", "LightCavalry", 0.25f, 50, null, "Javelin_vs_LightCavalry"),
            new RawInteraction("Slinger", "LightCavalry", -0.58f, 50, null, "Slinger_vs_LightCavalry"),

            // LIGHT ARABIAN
            new RawInteraction("Arrow", "LightArabian", 0.04f, 50, "LIGHT ARABIAN", "Arrow_vs_LightArabian"),
            new RawInteraction("Bolt", "LightArabian", 0.33f, 50, null, "Bolt_vs_LightArabian"),
            new RawInteraction("Javelin", "LightArabian", 0.04f, 50, null, "Javelin_vs_LightArabian"),
            new RawInteraction("Slinger", "LightArabian", -0.67f, 50, null, "Slinger_vs_LightArabian"),

            // ARAB BALLISTA
            new RawInteraction("Arrow", "ArabBallista", 0.25f, 50, "ARAB BALLISTA", "Arrow_vs_ArabBallista"),
            new RawInteraction("Bolt", "ArabBallista", -0.75f, 50, null, "Bolt_vs_ArabBallista"),
            new RawInteraction("Javelin", "ArabBallista", -0.37f, 50, null, "Javelin_vs_ArabBallista"),
            new RawInteraction("Slinger", "ArabBallista", -0.75f, 50, null, "Slinger_vs_ArabBallista"),

            // BEDOUIN HEAVY CAMEL
            new RawInteraction("Arrow", "BedouinHeavyCamel", 0.25f, 50, "BEDOUIN HEAVY CAMEL", "Arrow_vs_BedouinHeavyCamel"),
            new RawInteraction("Bolt", "BedouinHeavyCamel", 2.00f, 50, null, "Bolt_vs_BedouinHeavyCamel"),
            new RawInteraction("Javelin", "BedouinHeavyCamel", -0.25f, 50, null, "Javelin_vs_BedouinHeavyCamel"),
            new RawInteraction("Slinger", "BedouinHeavyCamel", -0.25f, 50, null, "Slinger_vs_BedouinHeavCamel"),

            // BEDOUIN EUNUCH
            new RawInteraction("Bolt", "BedouinEunuch", 0.47f, 50, "BEDOUIN EUNUCH", "Bolt_vs_BedouinEunuch"),
            new RawInteraction("Javelin", "BedouinEunuch", -0.67f, 50, null, "Javelin_vs_BedouinEunuch"),
            new RawInteraction("Slinger", "BedouinEunuch", -0.67f, 50, null, "Slinger_vs_BedouinEunuch"),

            // BEDOUIN HEALER
            new RawInteraction("Bolt", "BedouinHealer", -0.80f, 50, "BEDOUIN HEALER", "Bolt_vs_BedouinHealer"),
            new RawInteraction("Javelin", "BedouinHealer", -0.50f, 50, null, "Javelin_vs_BedouinHealer"),
            new RawInteraction("Slinger", "BedouinHealer", -0.33f, 50, null, "Slinger_vs_BedouinHealer"),

            // BEDOUIN SKIRMISHER
            new RawInteraction("Arrow", "BedouinSkirmisher", 0.04f, 50, "BEDOUIN SKIRMISHER", "Arrow_vs_BedouinSkirmisher"),
            new RawInteraction("Javelin", "BedouinSkirmisher", -0.17f, 50, null, "Javelin_vs_BedouinSkirmisher"),
            new RawInteraction("Slinger", "BedouinSkirmisher", -0.37f, 50, null, "Slinger_vs_BedouinSkirmisher"),

            // BEDOUIN SAPPER & DEMOLISHER
            new RawInteraction("Bolt", "BedouinSapper", -0.11f, 50, "BEDOUIN SAPPER & DEMOLISHER", "Bolt_vs_BedouinSapper"),
            new RawInteraction("Javelin", "BedouinSapper", -0.17f, 50, null, "Javelin_vs_BedouinSapper"),
            new RawInteraction("Slinger", "BedouinSapper", -0.56f, 50, null, "Slinger_vs_BedouinSapper"),

            // TUNNELER
            new RawInteraction("Javelin", "Tunneler", 0.50f, 50, "TUNNELER", "Javelin_vs_Tunneler"),

            // PREDATOR
            new RawInteraction("Bolt", "Predator", -0.50f, 50, "PREDATOR", "Bolt_vs_Predator"),
            new RawInteraction("Javelin", "Predator", 0.25f, 50, null, "Javelin_vs_Predator"),

            // LARGE PREDATORS
            new RawInteraction("Slinger", "LargePredator", -0.25f, 50, "LARGE PREDATORS", "Slinger_vs_LargePredator"),

            // CIVILIANS & ANIMALS
            new RawInteraction("Slinger", "Civilian", -0.67f, 50, "CIVILIANS & ANIMALS", "Slinger_vs_Civilian"),
        };

        private static readonly RawInteraction[] MeleeInteractions = new[]
        {
            // ARAB_BOW MELEE 
            new RawInteraction("ArabBow", "Arabian", 1.0f, 5, "ARAB_BOW MELEE ATTACKS", "ArabBow_vs_Arabian"),
            new RawInteraction("ArabBow", "EuropeanInfantry", 0.5f, 5, null, "ArabBow_vs_EuropeanInfantry"),
            new RawInteraction("ArabBow", "BedouinEunuch", 1.5f, 5, null, "ArabBow_vs_BedouinEunuch"),

            // BEAST MELEE
            new RawInteraction("Beast", "BedouinHealer", -0.41f, 5, "BEAST MELEE ATTACKS", "Beast_vs_BedouinHealer"),
            new RawInteraction("Beast", "ArabSlave", -0.37f, 5, null, "Beast_vs_ArabSlave"),
            new RawInteraction("Beast", "ArabSlinger", -0.29f, 5, null, "Beast_vs_ArabSlinger"),
            new RawInteraction("Beast", "LightMelee", -0.17f, 5, null, "Beast_vs_LightMelee"),
            new RawInteraction("Beast", "HeavyArmor", 0.11f, 5, null, "Beast_vs_HeavyArmor"),
            new RawInteraction("Beast", "Trebuchet", 0.67f, 5, null, "Beast_vs_Trebuchet"),
            new RawInteraction("Beast", "Rabbit", -0.09f, 5, null, "Beast_vs_Rabbit"),

            // POLEARM MELEE
            new RawInteraction("Polearm", "Ladderman", 3.0f, 5, "POLEARM MELEE ATTACKS", "Polearm_vs_Ladderman"),
            new RawInteraction("Polearm", "ArabianLight", 0.33f, 5, null, "Polearm_vs_ArabianLight"),
            new RawInteraction("Polearm", "BedouinHealer", 0.43f, 5, null, "Polearm_vs_BedouinHealer"),
            new RawInteraction("Polearm", "BedouinEunuch", 0.2f, 5, null, "Polearm_vs_BedouinEunuch"),

            // PICKAXE MELEE
            new RawInteraction("Pickaxe", "LightMelee", -0.17f, 5, "PICKAXE MELEE ATTACKS", "Pickaxe_vs_LightMelee"),
            new RawInteraction("Pickaxe", "Rabbit", -0.17f, 5, null, "Pickaxe_vs_Rabbit"),
            new RawInteraction("Pickaxe", "HeavyArmor", 0.25f, 5, null, "Pickaxe_vs_HeavyArmor"),
            new RawInteraction("Pickaxe", "ArabSlinger", -0.14f, 5, null, "Pickaxe_vs_ArabSlinger"),
            new RawInteraction("Pickaxe", "BedouinHealer", -0.11f, 5, null, "Pickaxe_vs_BedouinHealer"),
            new RawInteraction("Pickaxe", "Trebuchet", 0.67f, 5, null, "Pickaxe_vs_Pickaxe_vs_Trebuchet"),

            // HEAVY SWORD MELEE
            new RawInteraction("HeavySword", "HeavyArmor", -0.44f, 5, "HEAVY SWORD ATTACKS", "HeavySword_vs_HeavyArmor"),
            new RawInteraction("HeavySword", "ArabSlave", 0.25f, 5, null, "HeavySword_vs_ArabSlave"),
            new RawInteraction("HeavySword", "BedouinEunuch", 0.25f, 5, null, "HeavySword_vs_BedouinEunuch"),
            new RawInteraction("HeavySword", "Trebuchet", -0.33f, 5, null, "HeavySword_vs_Trebuchet"),
            new RawInteraction("HeavySword", "ArabSlinger", 0.07f, 5, null, "HeavySword_vs_ArabSlinger"),
            new RawInteraction("HeavySword", "BedouinHealer", 0.18f, 5, null, "HeavySword_vs_BedouinHealer"),
            new RawInteraction("HeavySword", "Rabbit", -0.09f, 5, null, "HeavySword_vs_Rabbit"),
            new RawInteraction("HeavySword", "Ladderman", -0.17f, 5, null, "HeavySword_vs_Ladderman"),

            // HEAVY CAV MELEE
            new RawInteraction("HeavyCav", "HeavyArmor", -0.29f, 5, "KNIGHT & CAMEL LANCER ATTACKS", "HeavyCav_vs_HeavyArmor"),
            new RawInteraction("HeavyCav", "Ladderman", -0.16f, 5, null, "HeavyCav_vs_Ladderman"),
            new RawInteraction("HeavyCav", "Rabbit", -0.11f, 5, null, "HeavyCav_vs_Rabbit"),
            new RawInteraction("HeavyCav", "Trebuchet", -0.4f, 5, null, "HeavyCav_vs_Trebuchet"),
            new RawInteraction("HeavyCav", "ArabSlinger", 0.09f, 5, null, "HeavyCav_vs_ArabSlinger"),
            new RawInteraction("HeavyCav", "ArabSlave", 0.23f, 5, null, "HeavyCav_vs_ArabSlave"),
            new RawInteraction("HeavyCav", "BedouinHealer", 0.19f, 5, null, "HeavyCav_vs_BedouinHealer"),
            new RawInteraction("HeavyCav", "BedouinEunuch", 0.26f, 5, null, "HeavyCav_vs_BedouinEunuch"),

            // CAMEL LANCER SPECIFIC
            new RawInteraction("BedouinCamelLancer", "Xbowman", -0.125f, 5, "BEDOUIN CAMEL LANCER SPECIFIC", "BedouinCamelLancer_vs_Xbowman"),
            new RawInteraction("BedouinCamelLancer", "Maceman", -0.125f, 5, null, "BedouinCamelLancer_vs_Maceman"),
            new RawInteraction("BedouinCamelLancer", "Pikeman", -0.5f, 5, null, "BedouinCamelLancer_vs_Pikeman"),
            new RawInteraction("BedouinCamelLancer", "ArabSwordsman", -0.62f, 5, null, "BedouinCamelLancer_vs_ArabSwordsman"),
            new RawInteraction("BedouinCamelLancer", "Swordsman", -0.36f, 5, null, "BedouinCamelLancer_vs_Swordsman"),
            new RawInteraction("BedouinCamelLancer", "Knight", -0.36f, 5, null, "BedouinCamelLancer_vs_Knight"),

            // MACEMAN MELEE
            new RawInteraction("Maceman", "HeavyArmor", -0.62f, 5, "MACEMAN ATTACKS", "Maceman_vs_HeavyArmor"),
            new RawInteraction("Maceman", "Ladderman", -0.17f, 5, null, "Maceman_vs_Ladderman"),
            new RawInteraction("Maceman", "Trebuchet", -0.56f, 5, null, "Maceman_vs_Trebuchet"),
            new RawInteraction("Maceman", "Rabbit", -0.12f, 5, null, "Maceman_vs_Rabbit"),
            new RawInteraction("Maceman", "BedouinHealer", 0.15f, 5, null, "Maceman_vs_BedouinHealer"),
            new RawInteraction("Maceman", "ArabSlinger", 0.14f, 5, null, "Maceman_vs_ArabSlinger"),

            // MONK MELEE
            new RawInteraction("Monk", "HeavyArmor", 0.11f, 5, "MONK ATTACKS", "Monk_vs_HeavyArmor"),
            new RawInteraction("Monk", "Ladderman", -0.17f, 5, null, "Monk_vs_Ladderman"),
            new RawInteraction("Monk", "Trebuchet", 0.67f, 5, null, "Monk_vs_Trebuchet"),
            new RawInteraction("Monk", "Rabbit", -0.09f, 5, null, "Monk_vs_Rabbit"),
            new RawInteraction("Monk", "BedouinHealer", 0.18f, 5, null, "Monk_vs_BedouinHealer"),
            new RawInteraction("Monk", "BedouinEunuch", 0.17f, 5, null, "Monk_vs_BedouinEunuch"),
            new RawInteraction("Monk", "ArabSlinger", 0.14f, 5, null, "Monk_vs_ArabSlinger"),

            // ASSASSIN MELEE
            new RawInteraction("ArabAssassin", "HeavyArmor", 0.14f, 5, "ARAB ASSASSIN ATTACKS", "ArabAssassin_vs_HeavyArmor"),
            new RawInteraction("ArabAssassin", "Ladderman", -0.16f, 5, null, "ArabAssassin_vs_Ladderman"),
            new RawInteraction("ArabAssassin", "Rabbit", -0.11f, 5, null, "ArabAssassin_vs_Rabbit"),
            new RawInteraction("ArabAssassin", "Trebuchet", -0.96f, 5, null, "ArabAssassin_vs_Trebuchet"),
            new RawInteraction("ArabAssassin", "BedouinHealer", 0.85f, 5, null, "ArabAssassin_vs_BedouinHealer"),
            new RawInteraction("ArabAssassin", "ArabSlave", 0.92f, 5, null, "ArabAssassin_vs_ArabSlave"),
            new RawInteraction("ArabAssassin", "ArabSlinger", 1.27f, 5, null, "ArabAssassin_vs_ArabSlinger"),
            new RawInteraction("ArabAssassin", "BedouinEunuch", 0.58f, 5, null, "ArabAssassin_vs_BedouinEunuch"),

            // LORD MELEE
            new RawInteraction("Lord", "HeavyArmor", 0.11f, 5, "LORD ATTACKS", "Lord_vs_HeavyArmor"),
            new RawInteraction("Lord", "Ladderman", -0.17f, 5, null, "Lord_vs_Ladderman"),
            new RawInteraction("Lord", "ArabSlave", -0.17f, 5, null, "Lord_vs_ArabSlave"),
            new RawInteraction("Lord", "Trebuchet", 0.67f, 5, null, "Lord_vs_Trebuchet"),
            new RawInteraction("Lord", "BedouinHealer", -0.22f, 5, null, "Lord_vs_BedouinHealer"),
            new RawInteraction("Lord", "ArabSlinger", -0.05f, 5, null, "Lord_vs_ArabSlinger"),
            new RawInteraction("Lord", "BedouinEunuch", -0.03f, 5, null, "Lord_vs_BedouinEunuch"),
            new RawInteraction("Lord", "Rabbit", -0.09f, 5, null, "Lord_vs_Rabbit"),

            // HEAVY CAMEL & DEMOLISHER
            new RawInteraction("BedouinHeavy", "HeavyArmor", 0.14f, 5, "BEDOUIN HEAVY CAMEL & DEMOLISHER ATTACKS", "BedouinHeavy_vs_HeavyArmor"),
            new RawInteraction("BedouinHeavy", "Rabbit", -0.11f, 5, null, "BedouinHeavy_vs_Rabbit"),
            new RawInteraction("BedouinHeavy", "ArabSlinger", -0.27f, 5, null, "BedouinHeavy_vs_ArabSlinger"),
            new RawInteraction("BedouinHeavy", "ArabSlave", -0.38f, 5, null, "BedouinHeavy_vs_ArabSlave"),
            new RawInteraction("BedouinHeavy", "BedouinHealer", -0.14f, 5, null, "BedouinHeavy_vs_BedouinHealer"),
            new RawInteraction("BedouinHeavy", "Trebuchet", -0.6f, 5, null, "BedouinHeavy_vs_Trebuchet"),
            new RawInteraction("BedouinHeavy", "Ladderman", -0.2f, 5, null, "BedouinHeavy_vs_Ladderman"),

            // DEMOLISHER SPECIFIC
            new RawInteraction("BedouinDemolisher", "Trebuchet", 1.2f, 5, "BEDOUIN DEMOLISHER SPECIFIC", "BedouinDemolisher_vs_Trebuchet"),

            // RANGED MELEE
            new RawInteraction("RangedMelee", "ArabSlave", 0.33f, 5, "RANGED UNIT MELEE ATTACKS", "RangedMelee_vs_ArabSlave"),
            new RawInteraction("RangedMelee", "ArabSlinger", 0.33f, 5, null, "RangedMelee_vs_ArabSlinger"),
            new RawInteraction("RangedMelee", "BedouinHealer", 0.33f, 5, null, "RangedMelee_vs_BedouinHealer"),

            // ARAB HORSEMAN MELEE
            new RawInteraction("ArabHorsemanMelee", "ArabSlave", -0.17f, 5, "ARAB HORSEMAN ATTACKS", "ArabHorsemanMelee_vs_ArabSlave"),
            new RawInteraction("ArabHorsemanMelee", "ArabSlinger", -0.17f, 5, null, "ArabHorsemanMelee_vs_ArabSlinger"),
            new RawInteraction("ArabHorsemanMelee", "BedouinHealer", -0.29f, 5, null, "ArabHorsemanMelee_vs_BedouinHealer"),
            new RawInteraction("ArabHorsemanMelee", "Trebuchet", -0.8f, 5, null, "ArabHorsemanMelee_vs_Trebuchet"),
            new RawInteraction("ArabHorsemanMelee", "Ladderman", -0.2f, 5, null, "ArabHorsemanMelee_vs_Ladderman"),

            // WORKER MELEE
            new RawInteraction("Worker", "LightMelee", -0.2f, 5, "WORKER ATTACKS", "Worker_vs_LightMelee"),
            new RawInteraction("Worker", "ArabSlave", 0.33f, 5, null, "Worker_vs_ArabSlave"),
            new RawInteraction("Worker", "BedouinHealer", -0.14f, 5, null, "Worker_vs_BedouinHealer"),
            new RawInteraction("Worker", "Trebuchet", 1.0f, 5, null, "Worker_vs_Trebuchet"),

            // DOG MELEE
            new RawInteraction("Dog", "Rabbit", 4.0f, 5, "DOG ATTACKS", "Dog_vs_Rabbit"),
            new RawInteraction("Dog", "ArabSlave", 0.33f, 5, null, "Dog_vs_ArabSlave"),
            new RawInteraction("Dog", "BedouinHealer", -0.33f, 5, null, "Dog_vs_BedouinHealer"),
            new RawInteraction("Dog", "Trebuchet", 1.0f, 5, null, "Dog_vs_Trebuchet"),

            // HUNTER MELEE
            new RawInteraction("Hunter", "ArabSlave", 0.33f, 5, "HUNTER ATTACKS", "Hunter_vs_ArabSlave"),
            new RawInteraction("Hunter", "Trebuchet", 1.0f, 5, null, "Hunter_vs_Trebuchet"),
            new RawInteraction("Hunter", "LargePredator", 1.0f, 5, null, "Hunter_vs_LargePredator"),
            new RawInteraction("Hunter", "WarDog", 1.0f, 5, null, "Hunter_vs_WarDog"),

            // LIGHT RANGED MELEE (Grenadier, Skirmisher)
            new RawInteraction("LightRanged", "Ladderman", -0.25f, 5, "ARAB GRENADIER & SKIRMISHER ATTACKS", "LightRanged_vs_Ladderman"),
            new RawInteraction("LightRanged", "ArabSlinger", -0.25f, 5, null, "LightRanged_vs_ArabSlinger"),
            new RawInteraction("LightRanged", "Trebuchet", -0.8f, 5, null, "LightRanged_vs_Trebuchet"),
            new RawInteraction("BedouinSkirmisher", "BedouinHealer", 0.2f, 5, null, "BedouinSkirmisher_vs_BedouinHealer"),
            new RawInteraction("BedouinSkirmisher", "ArabSlave", -0.4f, 5, null, "BedouinSkirmisher_vs_ArabSlave"),

            // BEDOUIN AMBUSHER MELEE
            new RawInteraction("BedouinAmbusher", "ArabianLight", -0.33f, 5, "BEDOUIN AMBUSHER ATTACKS", "BedouinAmbusher_vs_ArabianLight"),
            new RawInteraction("BedouinAmbusher", "BedouinHealer", 0.33f, 5, null, "BedouinAmbusher_vs_BedouinHealer"),

            // EUNUCH & HEALER MELEE
            new RawInteraction("BedouinEunuch", "ArabianLight", -0.33f, 5, "BEDOUIN EUNUCH & HEALER ATTACKS", "BedouinEunuch_vs_ArabianLight"),
            new RawInteraction("BedouinEunuch", "BedouinHealer", 0.33f, 5, null, "BedouinEunuch_vs_BedouinHealer"),
            new RawInteraction("BedouinHealer", "ArabianLight", -0.33f, 5, null, "BedouinHealer_vs_ArabianLight"),
            new RawInteraction("BedouinHealer", "BedouinHealer", 0.33f, 5, null, "BedouinHealer_vs_BedouinHealer_Melee"),

            // ARAB SLAVE MELEE
            new RawInteraction("ArabSlaveMelee", "BedouinEunuch", 0.5f, 5, "ARAB SLAVE ATTACKS", "ArabSlaveMelee_vs_BedouinEunuch"),

            // ARAB SLINGER MELEE
            new RawInteraction("ArabSlingerMelee", "Ladderman", -0.2f, 5, "ARAB SLINGER MELEE ATTACKS", "ArabSlingerMelee_vs_Ladderman"),
            new RawInteraction("ArabSlingerMelee", "Trebuchet", -0.8f, 5, null, "ArabSlingerMelee_vs_Trebuchet"),
            new RawInteraction("ArabSlingerMelee", "ArabianLight", -0.17f, 5, null, "ArabSlingerMelee_vs_ArabianLight"),
            new RawInteraction("ArabSlingerMelee", "BedouinHealer", -0.14f, 5, null, "ArabSlingerMelee_vs_BedouinHealer"),

            // BLACKSMITH MELEE
            new RawInteraction("Blacksmith", "ArabSlinger", -0.17f, 5, "BLACKSMITH ATTACKS", "Blacksmith_vs_ArabSlinger"),
            new RawInteraction("Blacksmith", "LightMelee", -0.2f, 5, null, "Blacksmith_vs_LightMelee"),
            new RawInteraction("Blacksmith", "Trebuchet", 1.0f, 5, null, "Blacksmith_vs_Trebuchet"),
            new RawInteraction("Blacksmith", "BedouinHealer", -0.14f, 5, null, "Blacksmith_vs_BedouinHealer"),

            // ADDITIONAL FIXES
            new RawInteraction("Maceman", "ArabSlave", 0.25f, 5, "ADDITIONAL SPECIFIC FIXES", "Maceman_vs_ArabSlave"),
            new RawInteraction("Maceman", "BedouinEunuch", 0.11f, 5, null, "Maceman_vs_BedouinEunuch"),
            new RawInteraction("Spearman", "ArabSlinger", -0.33f, 5, null, "Spearman_vs_ArabSlinger"),
        };
        /// <summary>
        /// Generate the default unit tags TOML file.
        /// </summary>
        public static void Generate(string filePath)
        {
            Plugin.Logger.LogInfo($"[Generator] Generate called for {filePath}");
            
            // Check if generation is needed
            if (!AutoGenConfig.ForceRegenerate && File.Exists(filePath))
            {
                Plugin.Logger.LogInfo("[Generator] File exists and ForceRegenerate is false. Skipping.");
                return;
            }
            try
            {
                LoadAliasMapping();

                // 1. DISCOVER RAW INTERACTIONS (Phase 1)
                var rawInteractions = DiscoverInteractions();
                Plugin.Logger.LogInfo($"[Generator] Discovery returned {rawInteractions.Count} interactions.");

                // MERGE MANUAL INTERACTIONS
                // SKIPPED: Manual interactions overlap with discovered ones, causing double counting.
                // Relying purely on auto-discovery for accuracy.
                // rawInteractions.AddRange(RangedInteractions);
                // rawInteractions.AddRange(MeleeInteractions);
                Plugin.Logger.LogInfo($"[Generator] Total interactions after merge: {rawInteractions.Count}");

                // 2. MATHEMATICAL COMPRESSION (Phase 3)
                var equivalenceClasses = DetectEquivalenceClasses(rawInteractions);
                Plugin.Logger.LogInfo($"[Generator] Equivalence Classes count: {equivalenceClasses.Count}");

                var sb = new StringBuilder();
                sb.AppendLine("# CrusaderDETweaker_UnitTags.toml");
                sb.AppendLine("# ================================================================================");
                sb.AppendLine("# SEMANTIC INTERACTION CONFIGURATION (Auto-Generated)");
                sb.AppendLine("#");
                sb.AppendLine("# This file defines bonuses and penalties between unit tags.");
                sb.AppendLine("# Multiplier logic: FinalValue = 1.0 + Σ(deltas)");
                sb.AppendLine("#");
                sb.AppendLine("# COMPRESSION STATUS: Mathematical equivalence classes applied.");
                sb.AppendLine("# ================================================================================");
                sb.AppendLine();

                // 2.5 OUTPUT TAG DEFINITIONS (Phase 3)
                // SKIPPED: Using unit-native semantic tags instead of synthetic tags
                // sb.AppendLine("[Tags]");
                // ...

                sb.AppendLine();

                sb.AppendLine("[Interactions]");

                // 3. RANGED SECTION
                var rangedKeywords = new[] { "RANGED", "ARROW", "BOLT", "SLINGER", "JAVELIN" };
                var rangedClasses = equivalenceClasses
                    .Where(eq => eq.Group != null && rangedKeywords.Any(k => eq.Group.ToUpper().Contains(k)))
                    .OrderBy(eq => eq.DefenderTag)
                    .ThenBy(eq => eq.Delta);

                if (rangedClasses.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("    # ----------------------------------------------------------------------------");
                    sb.AppendLine("    # RANGED INTERACTIONS");
                    sb.AppendLine("    # ----------------------------------------------------------------------------");

                    foreach (var eq in rangedClasses)
                    {
                        string interactionId = GetInteractionName(eq.InternalId, eq.SuggestedId);
                        AppendInteraction(sb, interactionId, eq.AttackerTags, new List<string> { eq.DefenderTag }, eq.Delta, 0);
                    }
                }

                // 4. MELEE SECTION
                var meleeClasses = equivalenceClasses
                    .Where(eq => !rangedClasses.Contains(eq))
                    .OrderBy(eq => eq.DefenderTag)
                    .ThenBy(eq => eq.Delta);

                if (meleeClasses.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("    # ----------------------------------------------------------------------------");
                    sb.AppendLine("    # MELEE INTERACTIONS");
                    sb.AppendLine("    # ----------------------------------------------------------------------------");

                    foreach (var eq in meleeClasses)
                    {
                        string interactionId = GetInteractionName(eq.InternalId, eq.SuggestedId);
                        AppendInteraction(sb, interactionId, eq.AttackerTags, new List<string> { eq.DefenderTag }, eq.Delta, 0);
                    }
                }

                File.WriteAllText(filePath, sb.ToString());
                
                SaveAliasMapping();
                Plugin.Logger.LogInfo($"Generated unit tags: {filePath}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to generate unit tags: {ex.Message}");
            }
        }

        /// <summary>
        /// Stub for legacy auto-generated unit tag interactions. No-op to disable broken auto-generation.
        /// </summary>
        /// <param name="sb">StringBuilder for the config file contents.</param>
        private static void AppendGeneratedInteractions(StringBuilder sb)
        {
            // Intentionally left blank: auto-generation of tag interactions is disabled.
        }

        private static Dictionary<string, string> AliasMapping = new Dictionary<string, string>();
        private const string AliasFileName = "CrusaderDETweaker_UnitTags_Aliases.json";

        private static void LoadAliasMapping()
        {
            try
            {
                string filePath = ConfigPaths.UnitTagsAliases;
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var lines = json.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains(":"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length == 2)
                            {
                                string key = parts[0].Trim(' ', '"', ',', '\r', '\n', '\t');
                                string val = parts[1].Trim(' ', '"', ',', '\r', '\n', '\t');
                                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                                    AliasMapping[key] = val;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void SaveAliasMapping()
        {
            try
            {
                string filePath = ConfigPaths.UnitTagsAliases;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                var sorted = AliasMapping.OrderBy(kv => kv.Key).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    sb.Append($"  \"{sorted[i].Key}\": \"{sorted[i].Value}\"");
                    if (i < sorted.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("}");
                File.WriteAllText(filePath, sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Generates a stable internal ID for an interaction based on its properties.
        /// Format: INT_HASH (Internal ID)
        /// </summary>
        private static string GenerateInternalId(string attacker, string defender, float delta)
        {
            // Normalize delta string to 4 decimal places for stable hashing
            string deltaStr = delta.ToString("F4");
            string raw = $"{attacker}|{defender}|{deltaStr}";
            
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(raw);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                
                // Use first 8 chars of hash for a reasonably collision-free ID
                StringBuilder sb = new StringBuilder("INT_");
                for (int i = 0; i < 4; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Get the best name for an interaction (alias if exists, otherwise internal ID).
        /// </summary>
        private static string GetInteractionName(string internalId, string suggestedName)
        {
            if (AliasMapping.TryGetValue(internalId, out var alias))
                return alias;
            
            // Register new mapping if we have a suggested name
            if (!string.IsNullOrEmpty(suggestedName))
            {
                // Clean up suggested name for TOML compatibility
                string cleanName = suggestedName.Replace(" ", "_").Replace("-", "_");
                
                // If this name is already taken by another ID, append hash suffix
                if (AliasMapping.Values.Contains(cleanName))
                {
                    cleanName += "_" + internalId.Replace("INT_", "");
                }
                
                AliasMapping[internalId] = cleanName;
                return cleanName;
            }
            
            return internalId;
        }


        /// <summary>
        /// Append a tag interaction to the config file using list-based tags.
        /// </summary>
        private static void AppendInteraction(StringBuilder sb, string id, List<string> attackerTags, List<string> defenderTags, float delta, int flatBonus)
        {
            sb.AppendLine($"    [Interactions.{id}]");
            
            if (attackerTags.Count == 1)
                sb.AppendLine($"    AttackerTag = \"{attackerTags[0]}\"");
            else
                sb.AppendLine($"    AttackerTags = [{string.Join(", ", attackerTags.Select(t => $"\"{t}\""))}]");

            if (defenderTags.Count == 1)
                sb.AppendLine($"    DefenderTag = \"{defenderTags[0]}\"");
            else
                sb.AppendLine($"    DefenderTags = [{string.Join(", ", defenderTags.Select(t => $"\"{t}\""))}]");

            sb.AppendLine($"    Delta = {delta:F3}");
            if (flatBonus != 0)
                sb.AppendLine($"    FlatBonus = {flatBonus}");
            sb.AppendLine();
        }
    }
}
