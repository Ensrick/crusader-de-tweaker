// Data/UnitDamageData.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Holds all damage-related properties for a single unit.
    /// This is the data model that represents what we'll store in TOML config.
    /// 
    /// Armor is now handled via tags:
    /// - Armor_None: Civilians, animals - no protection
    /// - Armor_Light: Light armor units - takes bonus damage from most weapons
    /// - Armor_Medium: Standard armor - normal damage
    /// - Armor_Heavy: Heavy armor (Knight, Swordsman) - reduced damage
    /// - Armor_Siege: Siege equipment (Trebuchet) - special protection
    /// </summary>
    internal class UnitDamageData
    {
        private readonly HashSet<string> _tags;
        private readonly Dictionary<eChimps, float> _specialModifiers;
        private float _armorValue = 1.0f; // Default: Standard armor (1.0 = takes normal damage)

        /// <summary>
        /// The unit this data applies to.
        /// </summary>
        public eChimps Unit { get; set; }

        /// <summary>
        /// Base melee damage output (before armor/modifiers).
        /// Range: 2-150 based on CSV analysis.
        /// </summary>
        public int BaseMeleeDamage { get; set; }

        /// <summary>
        /// Armor value - multiplier for incoming damage (damage taken multiplier).
        /// This is the core armor mechanic: FinalDamage = BaseDamage × ArmorValue × WeaponVsArmorMultiplier × Tags
        /// 
        /// - 0.4 = Very Heavy (Trebuchet) - takes 40% damage
        /// - 0.5 = Heavy armor (Knight, Swordsman) - takes 50% damage
        /// - 1.0 = Standard armor (most units) - takes 100% damage
        /// - 1.5 = Light armor (Arab Slinger, Eunuch) - takes 150% damage
        /// - 2.0 = Unarmored (Arab Slave, Bedouin Healer) - takes 200% damage
        /// </summary>
        public float ArmorValue
        {
            get => _armorValue;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ArmorValue), "ArmorValue cannot be negative");
                _armorValue = value;
            }
        }

        /// <summary>
        /// Tags for special behavior and damage rules.
        /// 
        /// Armor category tags (defender):
        /// - "Armor_None" - Civilians, animals
        /// - "Armor_Light" - Arab Slinger, Archers, Eunuch
        /// - "Armor_Medium" - Most military units
        /// - "Armor_Heavy" - Knight, Swordsman
        /// - "Armor_Siege" - Trebuchet
        /// 
        /// Melee weapon tags (attacker):
        /// - "Weapon_Sword" - Standard blade (Swordsman, Arab Swordsman, Lord)
        /// - "Weapon_Mace" - Blunt weapon (Maceman, Monk, Blacksmith)
        /// - "Weapon_Polearm" - Spears/pikes (Spearman, Pikeman)
        /// - "Weapon_Lance" - Cavalry lance (Knight, Camel Lancer)
        /// - "Weapon_Axe" - Axes (Woodcutter, Tunneler, Quarry workers)
        /// - "Weapon_Dagger" - Light blade (Assassin)
        /// - "Weapon_Unarmed" - No weapon (civilians, ranged units in melee)
        /// 
        /// Ranged weapon tags:
        /// - "Ranged_Bow" - Uses Arrow projectiles
        /// - "Ranged_Crossbow" - Uses Bolt projectiles
        /// - "Ranged_Sling" - Uses Slinger projectiles
        /// - "Ranged_Javelin" - Uses Javelin projectiles
        /// 
        /// Combat modifier tags:
        /// - "Cavalry" - Mounted unit
        /// - "Beast" - Natural weapons, deals FLAT damage ignoring armor table
        /// - "Armor_Piercing" - Ignores armor reduction
        /// - "Assassin" - Uses special damage formula vs unarmored
        /// - "Ladderman" - Takes 5x from Weapon_Polearm
        /// - "Hunter" - 2x damage vs Beast units
        /// - "Predator" - 5x damage vs SmallPrey units (Dog vs Rabbit)
        /// - "SmallPrey" - Takes 5x from Predator units
        /// 
        /// Defense tags:
        /// - "SiegeDefense" - Trebuchet's special defense, bypassed by some weapons
        /// </summary>
        public IReadOnlyCollection<string> Tags => _tags;

        /// <summary>
        /// Unit-specific damage modifiers against specific defenders.
        /// Key = Defender unit type, Value = Multiplier
        /// Used for special cases that don't fit the formula.
        /// </summary>
        public IReadOnlyDictionary<eChimps, float> SpecialModifiers => _specialModifiers;

        public UnitDamageData()
        {
            _tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _specialModifiers = new Dictionary<eChimps, float>();

            BaseMeleeDamage = 2;       // Safe default (civilian)
            ArmorValue = 1.0f;         // Safe default (standard armor)
        }

        /// <summary>
        /// Add a tag to this unit.
        /// </summary>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _tags.Add(tag);
        }

        /// <summary>
        /// Remove a tag from this unit.
        /// </summary>
        public void RemoveTag(string tag)
        {
            _tags.Remove(tag);
        }

        /// <summary>
        /// Check if this unit has a specific tag (case-insensitive).
        /// </summary>
        public bool HasTag(string tag)
        {
            return _tags.Contains(tag);
        }

        /// <summary>
        /// Check if this unit has any tag starting with the given prefix.
        /// Useful for checking "Ranged_*" or "Weapon_*" tags.
        /// </summary>
        public bool HasTagWithPrefix(string prefix)
        {
            return _tags.Any(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get the first tag starting with the given prefix.
        /// Returns null if not found.
        /// </summary>
        public string GetTagWithPrefix(string prefix)
        {
            return _tags.FirstOrDefault(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if this unit is a ranged unit (has any Ranged_* tag).
        /// </summary>
        public bool IsRangedUnit => HasTagWithPrefix("Ranged_");

        /// <summary>
        /// Check if this unit has armor piercing capability.
        /// Armor piercing units ignore armor reduction and get reduced bonus vs unarmored.
        /// </summary>
        public bool IsArmorPiercing => HasTag("Armor_Piercing");

        /// <summary>
        /// Get the ranged weapon type (Bow, Crossbow, Sling, Javelin) or null if not ranged.
        /// </summary>
        public string RangedWeaponType
        {
            get
            {
                var tag = GetTagWithPrefix("Ranged_");
                return tag?.Substring("Ranged_".Length);
            }
        }

        /// <summary>
        /// Get the melee weapon type (Sword, Mace, Polearm, etc.) or null if unarmed.
        /// Returns null for Weapon_Unarmed.
        /// </summary>
        public string MeleeWeaponType
        {
            get
            {
                var tag = GetTagWithPrefix("Weapon_");
                if (tag == null)
                    return null;

                var weaponType = tag.Substring("Weapon_".Length);

                // Unarmed is not a weapon type for modifier purposes
                if (weaponType.Equals("Unarmed", StringComparison.OrdinalIgnoreCase))
                    return null;

                return weaponType;
            }
        }

        /// <summary>
        /// Add a special damage modifier against a specific defender.
        /// </summary>
        public void AddSpecialModifier(eChimps defender, float multiplier)
        {
            _specialModifiers[defender] = multiplier;
        }

        /// <summary>
        /// Get the damage modifier against a specific defender.
        /// Returns 1.0 if no special modifier exists.
        /// </summary>
        public float GetModifierAgainst(eChimps defender)
        {
            return _specialModifiers.TryGetValue(defender, out float modifier) ? modifier : 1.0f;
        }
    }
}
