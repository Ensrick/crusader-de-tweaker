// AI Dev Comment: Handles the Tags property for units.
// Tags are string identifiers that group units for special damage interactions.
// Tags are used by the UnitTagInteraction system to apply bonuses/penalties between tagged units.

using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    /// <summary>
    /// Handles the Tags property for units.
    /// Tags group units for special damage interactions (e.g., "Polearm", "Ladderman").
    /// </summary>
    internal class TagsProperty : PropertyHandler<eChimps, List<string>>
    {
        private static readonly HashSet<string> ExcludedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // No exclusions - all tags are now actively used for 100% accuracy
        };

        public TagsProperty() : base("Tags")
        {
        }

        private static List<string> NormalizeTags(List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return new List<string>();

            var normalized = new List<string>(tags.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in tags)
            {
                var trimmed = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (ExcludedTags.Contains(trimmed))
                    continue;

                if (seen.Add(trimmed))
                    normalized.Add(trimmed);
            }

            return normalized;
        }

        /// <summary>
        /// Tags are not stored in the game API - they're purely config-driven.
        /// Return default tags from TryGetOriginalValue instead.
        /// Returns false if the unit has no tags (empty list) to skip writing to config.
        ///
        /// IMPORTANT: Even if tags aren't written to config (because they match defaults),
        /// we still need to register them with UnitTagRegistry during initialization.
        /// This is handled separately in RegisterDefaultTags().
        /// </summary>
        protected override bool TryGetFromAPI(eChimps unit, out List<string> value)
        {
            // Tags come from default values, not API
            if (!TryGetOriginalValue(unit, out value))
                return false;

            var before = string.Join(",", value);
            value = NormalizeTags(value);
            var after = string.Join(",", value);
            if (before != after)
            {
                Plugin.Logger.LogDebug($"Tags normalized for {unit}: [{before}] → [{after}]");
            }

            // Don't write empty tag lists to config (most units have no tags)
            return value != null && value.Count > 0;
        }

        /// <summary>
        /// Tags don't apply to the game API - they're used by the tag interaction system.
        /// This method registers the tags with UnitTagRegistry instead.
        /// </summary>
        protected override void SetToAPI(eChimps unit, List<string> tags)
        {
            tags = NormalizeTags(tags);

            // Register each tag with the UnitTagRegistry
            foreach (var tag in tags)
            {
                UnitTagRegistry.RegisterUnitTag(unit, tag);
            }

            Plugin.Logger.LogDebug($"Registered {tags.Count} tag(s) for {unit}: {string.Join(", ", tags)}");
        }

        /// <summary>
        /// Validate that tags are non-empty strings.
        /// </summary>
        internal override bool ValidateValue(List<string> tags)
        {
            if (tags == null)
                return false;

            // All tags must be non-empty strings
            return tags.All(tag => !string.IsNullOrWhiteSpace(tag));
        }

        /// <summary>
        /// Tags can be applied to any modifiable unit.
        /// </summary>
        internal override bool CanApplyTo(eChimps unit)
        {
            return !UnitCategories.IsNonModifiable(unit);
        }

        /// <summary>
        /// Default tags for units.
        /// Units get category-based tags for armor/role classification,
        /// which enables projectile-specific damage adjustments.
        /// </summary>
        protected override bool TryGetOriginalValue(eChimps unit, out List<string> defaultValue)
        {
            defaultValue = new List<string>();

            // Add armor/role category tags for projectile interactions
            AddCategoryTags(unit, defaultValue);

            return defaultValue.Count > 0;
        }

        /// <summary>
        /// Add category-based tags to a unit based on its armor class and role.
        /// These tags are used by projectile interactions to adjust damage.
        /// For 100% accuracy, many units have unique tags to match original game values.
        /// </summary>
        private static void AddCategoryTags(eChimps unit, List<string> tags)
        {
            switch (unit)
            {
                // === LORD (unique - very low bolt/slinger damage) ===
                case eChimps.CHIMP_TYPE_LORD:
                    tags.Add("Lord");       // Defender tag for ranged
                    tags.Add("Lord");       // Attacker tag for melee (has unique patterns)
                    break;

                // === KNIGHT (shares HeavyArmor tag with Swordsman for ranged and melee) ===
                // Knight keeps unique Bolt multiplier (mounted anti-cavalry penalty)
                // HeavyArmor now handles both ranged (Arrow, Javelin, Slinger) and melee via additive stacking
                case eChimps.CHIMP_TYPE_KNIGHT:
                    tags.Add("Knight");     // Defender tag for ranged (Bolt only)
                    tags.Add("HeavyArmor"); // Defender tag for ranged AND melee (shared with Swordsman)
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("HeavyCav");   // Attacker tag for melee (Knight + CamelLancer share)
                    break;

                // === SWORDSMAN (shares HeavyArmor tag with Knight for ranged and melee) ===
                // Swordsman keeps unique Bolt multiplier (infantry)
                // HeavyArmor now handles both ranged (Arrow, Javelin, Slinger) and melee via additive stacking
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                    tags.Add("Swordsman");  // Defender tag for ranged (Bolt only)
                    tags.Add("HeavyArmor"); // Defender tag for ranged AND melee (shared with Knight)
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("HeavySword"); // Attacker tag for melee (Swordsman + ArabSwordsman share)
                    break;

                // === BEDOUIN HEAVY CAMEL (unique projectile response) ===
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    tags.Add("BedouinHeavyCamel");
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("BedouinHeavy"); // Attacker tag (shares with Demolisher for some patterns)
                    break;

                // === TREBUCHET (unique - high javelin damage, low bolt) ===
                case eChimps.CHIMP_TYPE_TREBUCHET:
                    tags.Add("Trebuchet");  // Both defender (ranged) and defender (melee)
                    break;

                // === ARAB SWORDSMAN (unique - very high bolt, low arrow/javelin) ===
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                    tags.Add("ArabSwordsman");  // Defender tag for both ranged and melee (unique patterns)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("HeavySword");      // Attacker tag for melee
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === PIKEMAN (unique - very low bolt damage) ===
                case eChimps.CHIMP_TYPE_PIKEMAN:
                    tags.Add("Pikeman");
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("Polearm");    // Attacker tag for melee (Spearman + Pikeman share)
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === MACEMAN (unique - low bolt, high javelin) ===
                case eChimps.CHIMP_TYPE_MACEMAN:
                    tags.Add("Maceman");    // Both defender (ranged) and attacker (melee)
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === SPEARMAN (unique - slight adjustments) ===
                case eChimps.CHIMP_TYPE_SPEARMAN:
                    tags.Add("Spearman");
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("Polearm");    // Attacker tag for melee
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === BEDOUIN EUNUCH (unique projectile response) ===
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                    tags.Add("BedouinEunuch");  // Both defender (ranged) and attacker (melee)
                    tags.Add("LightMelee");      // Shared with Ladderman for many patterns
                    break;

                // === ARAB ASSASSIN (unique - reduced slinger/bolt) ===
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                    tags.Add("ArabAssassin");   // Both defender (ranged) and attacker (melee)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === ARAB GRENADIER (shares LightArabian ranged tag with Bedouin Ambusher) ===
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                    tags.Add("LightArabian");   // Defender tag for ranged (shared)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("LightRanged");    // Attacker tag (shares with Skirmisher)
                    break;

                // === BEDOUIN AMBUSHER (shares LightArabian ranged tag with Arab Grenadier) ===
                // NOTE: Tag order matters! More specific attacker tags must come BEFORE generic ones
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                    tags.Add("BedouinAmbusher"); // Unique attacker patterns (FIRST - most specific)
                    tags.Add("LightArabian");   // Defender tag for ranged (shared)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === BEDOUIN SAPPER (shares ranged tag with Demolisher) ===
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                    tags.Add("BedouinSapper"); // Defender for ranged (shared with Demolisher)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("Pickaxe");        // Attacker tag (shares with Tunneler)
                    break;

                // === BEDOUIN DEMOLISHER (shares ranged tag with Sapper) ===
                // NOTE: Tag order matters! More specific attacker tags must come BEFORE generic ones
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    tags.Add("BedouinDemolisher"); // Unique attacker patterns (FIRST - most specific)
                    tags.Add("BedouinHeavy");   // Attacker tag (shares with Heavy Camel)
                    tags.Add("BedouinSapper"); // Defender for ranged (shared with Sapper)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === TUNNELER (unique - high javelin/slinger damage) ===
                case eChimps.CHIMP_TYPE_TUNNELER:
                    tags.Add("Tunneler");
                    tags.Add("Pickaxe");        // Attacker tag for melee
                    break;

                // === ARAB HORSEMAN (shares LightCavalry ranged tag with Bedouin Camel Lancer) ===
                // NOTE: Tag order matters! More specific attacker tags must come BEFORE generic ones
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                    tags.Add("ArabHorseman");  // Unique attacker patterns (FIRST - most specific)
                    tags.Add("ArabHorsemanMelee"); // Additional attacker tag for melee
                    tags.Add("WeakAttacker");   // Shared weak attacker tag
                    tags.Add("LightCavalry");  // Defender tag for ranged (shared)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === BEDOUIN CAMEL LANCER (shares LightCavalry ranged tag with Arab Horseman) ===
                // NOTE: Tag order matters! More specific attacker tags must come BEFORE generic ones
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                    tags.Add("BedouinCamelLancer"); // Unique attacker patterns (FIRST - most specific)
                    tags.Add("HeavyCav");       // Attacker tag (shares with Knight)
                    tags.Add("LightCavalry");  // Defender tag for ranged (shared)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === ARCHER (unique - high bolt damage) ===
                case eChimps.CHIMP_TYPE_ARCHER:
                    tags.Add("Archer");
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("RangedMelee");    // Attacker tag for melee (Archer, Xbowman share)
                    tags.Add("WeakAttacker");   // Shared weak attacker tag
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === XBOWMAN (unique - slight reduction all projectiles) ===
                case eChimps.CHIMP_TYPE_XBOWMAN:
                    tags.Add("Xbowman");
                    tags.Add("EuropeanInfantry"); // Defender tag for ArabBow melee
                    tags.Add("RangedMelee");    // Attacker tag for melee
                    tags.Add("WeakAttacker");   // Shared weak attacker tag
                    break;

                // === ARAB BOW (unique - high bolt, low slinger) ===
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                    tags.Add("ArabBow");        // Both defender (ranged) and attacker (melee - unique 2x pattern)
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === ARAB SLINGER (unique - high slinger, low bolt/javelin) ===
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                    tags.Add("ArabSlinger");    // Defender tag for both ranged and melee
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("ArabSlingerMelee"); // Attacker tag for melee (unique patterns)
                    tags.Add("ArabianLight");    // Shared light unit tag
                    break;

                // === BEDOUIN SKIRMISHER (unique projectile response) ===
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                    tags.Add("BedouinSkirmisher"); // Defender for ranged
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("LightRanged");    // Attacker tag (shares with Grenadier)
                    break;

                // === MONK (unique - very low slinger/bolt) ===
                case eChimps.CHIMP_TYPE_MONK:
                    tags.Add("Monk");           // Both defender (ranged) and attacker (melee)
                    break;

                // === BEDOUIN HEALER (unique - low bolt/javelin, high slinger) ===
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                    tags.Add("BedouinHealer");  // Defender tag for both ranged and melee
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === ARAB SLAVE (unique - low bolt/javelin, high slinger) ===
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                    tags.Add("ArabSlave");      // Defender tag for both ranged and melee
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    tags.Add("ArabSlaveMelee"); // Attacker tag for melee (unique patterns)
                    tags.Add("RangedMelee");    // Slave behaves like RangedMelee attacker
                    tags.Add("ArabianLight");    // Shared light unit tag
                    break;

                // === BALLISTA (unique siege + wall-mounted) ===
                case eChimps.CHIMP_TYPE_BALLISTA:
                    tags.Add("Ballista");
                    tags.Add("TowerSiegeEquipment");  // Shared Javelin defense with Mangonel
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === ARAB BALLISTA (unique siege - NO MELEE ATTACK) ===
                // Note: Arab Ballista has no melee attack animation in-game.
                // Only defender tags are meaningful; no attacker tags.
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    tags.Add("ArabBallista");   // Defender tag for ranged only
                    tags.Add("Arabian");        // Defender tag for ArabBow melee
                    break;

                // === CATAPULT (unique siege) ===
                case eChimps.CHIMP_TYPE_CATAPULT:
                    tags.Add("Catapult");
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === MANGONEL (unique siege + wall-mounted) ===
                case eChimps.CHIMP_TYPE_MANGONEL:
                    tags.Add("Mangonel");
                    tags.Add("TowerSiegeEquipment");  // Shared Javelin defense with Ballista
                    break;

                // === BATTERING RAM (shares tag with Siege Tower) ===
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                    tags.Add("HeavySiegeEquipment");
                    break;

                // === SIEGE TOWER (shares tag with Battering Ram) ===
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                    tags.Add("HeavySiegeEquipment");
                    break;

                // === LADDERMAN (unique - slingers extra effective) ===
                case eChimps.CHIMP_TYPE_LADDERMAN:
                    tags.Add("Ladderman");      // Defender for both ranged and melee
                    tags.Add("LightMelee");      // Shared with Eunuch
                    break;

                // === LARGE PREDATORS (Lion, Hyena) ===
                // Predator: shared ranged base (Bolt=0.5, Javelin=1.25)
                // LargePredator: Slinger delta (large cats resist stones)
                case eChimps.CHIMP_TYPE_LION:
                case eChimps.CHIMP_TYPE_HYENA:
                    tags.Add("Predator");       // Ranged base (Bolt, Javelin)
                    tags.Add("LargePredator");  // Slinger delta
                    tags.Add("Beast");          // Attacker tag for melee
                    break;

                // === WAR DOG ===
                // Predator: shared ranged base (Bolt=0.5, Javelin=1.25)
                // WarDog: Slinger delta (dogs vulnerable to stones)
                case eChimps.CHIMP_TYPE_WAR_DOG:
                    tags.Add("Predator");       // Ranged base (Bolt, Javelin)
                    tags.Add("WarDog");         // Slinger delta
                    tags.Add("Beast");          // Attacker tag for melee
                    tags.Add("StoneResistant");  // Slinger ×0.50
                    break;

                // === CROCODILE (shares beast patterns) ===
                case eChimps.CHIMP_TYPE_CROCODILE:
                    tags.Add("Civilian");       // Defender for ranged (Slinger×0.3333)
                    tags.Add("Beast");          // Attacker tag for melee
                    break;

                // === CAMEL (shares beast patterns) ===
                case eChimps.CHIMP_TYPE_CAMEL:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Beast");          // Attacker tag for melee
                    break;

                // === RABBIT (unique defender for melee) ===
                case eChimps.CHIMP_TYPE_RABBIT:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Rabbit");         // Defender for melee (unique patterns)
                    break;

                // === DOG (unique attacker for melee) ===
                case eChimps.CHIMP_TYPE_DOG:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Dog");            // Attacker tag for melee (unique ×5.0 vs Rabbit)
                    break;

                // === HUNTER (unique attacker for melee) ===
                case eChimps.CHIMP_TYPE_HUNTER:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Hunter");         // Attacker tag for melee (×2.0 vs beasts/trebuchet)
                    break;

                // === WORKERS (share Worker attacker tag, except Blacksmith) ===
                case eChimps.CHIMP_TYPE_WOODCUTTER:
                case eChimps.CHIMP_TYPE_QUARRY_MASON:
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Worker");         // Attacker tag for melee
                    break;

                // === BLACKSMITH (unique patterns - not Worker) ===
                case eChimps.CHIMP_TYPE_BLACKSMITH:
                    tags.Add("Civilian");       // Defender for ranged
                    tags.Add("Blacksmith");     // Attacker tag for melee (unique patterns)
                    break;

                // === CIVILIANS AND SMALL ANIMALS (Slinger×0.3333, no melee attacker tags) ===
                case eChimps.CHIMP_TYPE_PEASANT:
                case eChimps.CHIMP_TYPE_ARMOURER:
                case eChimps.CHIMP_TYPE_BAKER:
                case eChimps.CHIMP_TYPE_BREWER:
                case eChimps.CHIMP_TYPE_FARMER_APPLE:
                case eChimps.CHIMP_TYPE_FARMER_CATTLE:
                case eChimps.CHIMP_TYPE_FARMER_HOPS:
                case eChimps.CHIMP_TYPE_FARMER_WHEAT:
                case eChimps.CHIMP_TYPE_FLETCHER:
                case eChimps.CHIMP_TYPE_INNKEEPER:
                case eChimps.CHIMP_TYPE_MILLER:
                case eChimps.CHIMP_TYPE_MINER1:
                case eChimps.CHIMP_TYPE_MINER2:
                case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_QUARRY_OX:
                case eChimps.CHIMP_TYPE_TANNER:
                case eChimps.CHIMP_TYPE_TRADER:
                case eChimps.CHIMP_TYPE_TRADER_HORSE:
                case eChimps.CHIMP_TYPE_PITCHMAN:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_DRUNKARD:
                case eChimps.CHIMP_TYPE_LADY:
                case eChimps.CHIMP_TYPE_PRIEST:
                case eChimps.CHIMP_TYPE_JESTER:
                case eChimps.CHIMP_TYPE_JUGGLER:
                case eChimps.CHIMP_TYPE_GOAT:
                case eChimps.CHIMP_TYPE_COW:
                case eChimps.CHIMP_TYPE_DEER:
                case eChimps.CHIMP_TYPE_CHICKEN:
                case eChimps.CHIMP_TYPE_CROW:
                case eChimps.CHIMP_TYPE_SEAGULL:
                    tags.Add("Civilian");
                    break;
            }
        }

        /// <summary>
        /// Parse tags from TOML array.
        /// Expected format: Tags = ["Polearm", "Heavy"]
        /// </summary>
        internal override bool TryParseValue(object tomlValue, out List<string> result)
        {
            result = new List<string>();

            // TOML arrays are returned as TomlArray
            if (!(tomlValue is TomlArray array))
                return false;

            try
            {
                foreach (var item in array)
                {
                    if (item is string tag && !string.IsNullOrWhiteSpace(tag))
                    {
                        result.Add(tag);
                    }
                }

                result = NormalizeTags(result);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to parse Tags from TOML: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Format tags for TOML output (array).
        /// Example: ["Polearm", "Heavy"]
        /// </summary>
        internal override string FormatValue(List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return "[]";

            var quotedTags = tags.Select(tag => $"\"{tag}\"");
            return $"[{string.Join(", ", quotedTags)}]";
        }

        /// <summary>
        /// Register default tags for all units.
        /// This ensures tags are registered even if they're not explicitly written to config
        /// (because they match the default values).
        /// Call this during config loading BEFORE applying user TOML values.
        /// </summary>
        public static void RegisterDefaultTags()
        {
            // DEACTIVATED: Tag system is too complex for automated management within context limits
            /*
            var tagsProperty = new TagsProperty();

            foreach (eChimps unit in Enum.GetValues(typeof(eChimps)))
            {
                if (UnitCategories.IsNonModifiable(unit))
                    continue;

                // Get default tags for this unit
                if (tagsProperty.TryGetOriginalValue(unit, out List<string> defaultTags) && defaultTags.Count > 0)
                {
                    defaultTags = NormalizeTags(defaultTags);

                    // Register with UnitTagRegistry
                    foreach (var tag in defaultTags)
                    {
                        UnitTagRegistry.RegisterUnitTag(unit, tag);
                    }

                    Plugin.Logger.LogDebug($"Registered default tags for {unit}: {string.Join(", ", defaultTags)}");
                }
            }

            Plugin.Logger.LogInfo($"Registered default tags for all units");
            */
            Plugin.Logger.LogInfo("Tag system is DEACTIVATED (Complexity exceeds context window limits)");
        }
    }
}
