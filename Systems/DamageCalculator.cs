// Systems/DamageCalculator.cs
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    /// <summary>
    /// Armor categories for the weapon vs armor lookup table.
    /// These correspond to defender tags: Armor_None, Armor_Light, etc.
    /// </summary>
    public enum ArmorCategory
    {
        None,       // Civilians, animals - Armor_None tag
        Light,      // Arab Slinger, Eunuch - Armor_Light tag  
        Medium,     // Most military units - Armor_Medium tag
        Heavy,      // Knight, Swordsman - Armor_Heavy tag
        Siege       // Trebuchet - Armor_Siege tag
    }

    /// <summary>
    /// Weapon categories for the weapon vs armor lookup table.
    /// These correspond to attacker tags.
    /// </summary>
    public enum WeaponCategory
    {
        Unarmed,    // Civilians, weak units - Weapon_Unarmed tag
        Sword,      // Swords - Weapon_Sword tag
        Mace,       // Blunt weapons - Weapon_Mace tag
        Polearm,    // Spears, pikes - Weapon_Polearm tag
        Lance,      // Cavalry lances - Weapon_Lance tag
        Axe,        // Axes - Weapon_Axe tag
        Dagger,     // Assassin - Weapon_Dagger tag
        Ranged,     // Bow/Crossbow/Sling in melee - Ranged_* tags
        Beast       // Animals - Beast tag
    }

    /// <summary>
    /// Calculates melee damage between units using a weapon vs armor lookup table.
    /// 
    /// Core Formula: BaseMeleeDamage × WeaponVsArmorMultiplier × SpecialModifiers
    /// 
    /// The lookup table maps (WeaponCategory, ArmorCategory) to damage multipliers,
    /// matching the game's actual damage calculations.
    /// </summary>
    internal static class DamageCalculator
    {
        /// <summary>
        /// Minimum damage floor - all attacks deal at least this much damage.
        /// </summary>
        public const int MinimumDamage = 2;

        /// <summary>
        /// Weak attacker threshold - attackers with base damage at or below this
        /// deal flat damage, ignoring ALL modifiers.
        /// </summary>
        public const int WeakAttackerThreshold = 2;

        /// <summary>
        /// Weapon vs Armor multiplier lookup table.
        /// Key: (WeaponCategory, ArmorCategory), Value: Damage multiplier
        /// 
        /// Derived from analyzing CrusaderDETweaker_MeleeDamage.csv game data.
        /// </summary>
        private static readonly Dictionary<(WeaponCategory, ArmorCategory), float> WeaponVsArmorTable = new Dictionary<(WeaponCategory, ArmorCategory), float>
        {
            // UNARMED (civilians, weak units) - flat base damage to everyone
            { (WeaponCategory.Unarmed, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Unarmed, ArmorCategory.Siege), 1.0f },

            // SWORD - good all-around
            // Formula: base * multiplier * armorValue
            // ARAB_BOW (20) vs ARAB_ASSASIN (1.0 armor, Light): 20 = 20 * 1.0 * 1.0
            // ARAB_BOW (20) vs ARAB_SLINGER (1.5 armor, Light): 30 = 20 * 1.0 * 1.5
            // SWORDSMAN (100) vs KNIGHT (0.5 armor, Heavy): 50 = 100 * 1.0 * 0.5
            { (WeaponCategory.Sword, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Light), 1.0f },  // Fixed: was 1.5, should be 1.0
            { (WeaponCategory.Sword, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Sword, ArmorCategory.Siege), 0.4f },

            // MACE - terrible vs heavy armor
            { (WeaponCategory.Mace, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Light), 1.0f },  // Fixed: was 1.5, should be 1.0
            { (WeaponCategory.Mace, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Mace, ArmorCategory.Heavy), 0.33f },
            { (WeaponCategory.Mace, ArmorCategory.Siege), 0.5f },

            // POLEARM - ignores heavy armor
            { (WeaponCategory.Polearm, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Light), 1.0f },  // Fixed: was 1.5, should be 1.0
            { (WeaponCategory.Polearm, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Polearm, ArmorCategory.Siege), 0.4f },

            // LANCE (Cavalry) - bonus vs heavy
            // Formula: base * multiplier * armorValue
            // KNIGHT (80) vs KNIGHT (0.5): 50 = 80 * 1.25 * 0.5
            { (WeaponCategory.Lance, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Light), 1.0f },  // Fixed: was 1.5, should be 1.0
            { (WeaponCategory.Lance, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Lance, ArmorCategory.Heavy), 1.25f },  // Fixed: was 0.625, should be 1.25
            { (WeaponCategory.Lance, ArmorCategory.Siege), 0.4f },

            // AXE - good vs siege
            { (WeaponCategory.Axe, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Light), 1.0f },  // Fixed: was 1.5, should be 1.0
            { (WeaponCategory.Axe, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Axe, ArmorCategory.Siege), 1.0f },  // Bypasses siege defense

            // DAGGER (Assassin) - huge bonus vs unarmored, ignores heavy reduction
            { (WeaponCategory.Dagger, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Light), 2.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Dagger, ArmorCategory.Siege), 0.025f },  // 2/80 from data

            // RANGED (bows, crossbows, slings in melee) - bonus vs heavy and light
            { (WeaponCategory.Ranged, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Light), 1.5f },
            { (WeaponCategory.Ranged, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Ranged, ArmorCategory.Heavy), 1.5f },  // Ranged gets bonus vs heavy!
            { (WeaponCategory.Ranged, ArmorCategory.Siege), 0.2f },

            // BEAST - flat damage, ignores all armor
            { (WeaponCategory.Beast, ArmorCategory.None), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Light), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Medium), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Heavy), 1.0f },
            { (WeaponCategory.Beast, ArmorCategory.Siege), 1.0f },
        };

        /// <summary>
        /// Special multipliers for specific attacker tags vs defender tags.
        /// Applied after the weapon vs armor lookup.
        /// </summary>
        private static readonly Dictionary<(string, string), float> TagVsTagModifiers = new Dictionary<(string, string), float>
        {
            // Polearm vs Ladderman: 5x damage
            { ("Weapon_Polearm", "Ladderman"), 5.0f },
            
            // Hunter vs Beast: 2x damage
            { ("Hunter", "Beast"), 2.0f },
            
            // Predator vs SmallPrey: 5x damage (Dog vs Rabbit)
            { ("Predator", "SmallPrey"), 5.0f },
        };

        /// <summary>
        /// Calculate melee damage from attacker to defender.
        /// Returns -1 if calculation fails (missing data).
        /// </summary>
        public static int CalculateMeleeDamage(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null || defenderData == null)
            {
                return -1;
            }

            // RULE 1: Weak attackers (Base ≤ 2) deal flat damage
            if (attackerData.BaseMeleeDamage <= WeakAttackerThreshold)
            {
                return attackerData.BaseMeleeDamage;
            }

            // Get weapon and armor categories early (needed for SiegeDefense check)
            var weaponCategory = GetWeaponCategory(attackerData);
            var armorCategory = GetArmorCategory(defenderData);

            // RULE 2: SiegeDefense - Specific units deal minimum damage (2) vs Trebuchet
            // Most weak/ranged/unarmed units deal 2, but strong melee (Sword, Mace, Lance, Axe, Polearm) deal normal damage
            // Exception: Weak Sword units (Base <= 20) with Ranged_Bow or Cavalry tags also deal 2
            if (defenderData.HasTag("SiegeDefense") || defenderData.HasTag("Armor_Siege"))
            {
                // Exception: Weak Sword units with Ranged_Bow or Cavalry deal 2 damage
                if (weaponCategory == WeaponCategory.Sword && attackerData.BaseMeleeDamage <= 20)
                {
                    if (attackerData.HasTag("Ranged_Bow") || (attackerData.HasTag("Cavalry") && attackerData.BaseMeleeDamage <= 20))
                    {
                        return MinimumDamage; // 2
                    }
                }
                
                // Beast units bypass SiegeDefense and deal normal damage
                if (weaponCategory == WeaponCategory.Beast)
                {
                    // These deal normal calculated damage (will be calculated below)
                }
                // Strong melee weapons (Sword, Mace, Lance, Axe, Polearm) bypass SiegeDefense and deal normal damage
                else if (weaponCategory == WeaponCategory.Sword || 
                    weaponCategory == WeaponCategory.Mace || 
                    weaponCategory == WeaponCategory.Lance || 
                    weaponCategory == WeaponCategory.Axe || 
                    weaponCategory == WeaponCategory.Polearm)
                {
                    // These deal normal calculated damage (will be calculated below)
                }
                // All other units (Assassin, Ranged, Unarmed) deal minimum damage (2)
                else
                {
                    return MinimumDamage; // 2
                }
            }

            // Core formula varies by weapon type:
            // - Most weapons: BaseDamage × WeaponVsArmorMultiplier × ArmorValue
            // - Assassin: Special formula (ignores lookup table)
            // - Ranged in melee: BaseDamage × ArmorValue × 2.0 (special multiplier)
            // - Unarmed: BaseDamage × ArmorValue (no lookup table)
            
            float damage;
            
            // Special handling for Assassin - uses special formula
            if (weaponCategory == WeaponCategory.Dagger)
            {
                damage = CalculateAssassinDamage(attackerData, defenderData);
            }
            // Special handling for Ranged units in melee
            else if (weaponCategory == WeaponCategory.Ranged)
            {
                damage = CalculateRangedMeleeDamage(attackerData, defenderData);
            }
            // Special handling for Unarmed units
            else if (weaponCategory == WeaponCategory.Unarmed)
            {
                // Unarmed units: flat base damage to most, double to unarmored (but cap at base for some units)
                // ARAB_BALLISTA (10) vs ARAB_SLINGER (1.5): 10 (flat)
                // ARAB_BALLISTA (10) vs ARAB_SLAVE (2.0): 20 (double)
                // ARAB_BALLISTA (10) vs BEDOUIN_HEALER (2.0): 10 (flat, not double!)
                // ARAB_BALLISTA (10) vs KNIGHT (0.5): 10 (flat, not reduced)
                // ARAB_SLAVE (10) vs ARAB_SLINGER (1.5): 20 (handled by unit-specific modifier)
                // ARAB_SLAVE (10) vs BEDOUIN_EUNUCH (1.5): 15 (handled by unit-specific modifier)
                
                if (defenderData.ArmorValue >= 2.0f)
                {
                    // Unarmored: double damage for most, but cap at base for some defenders
                    // BEDOUIN_HEALER seems to be an exception - cap at base
                    if (defender == eChimps.CHIMP_TYPE_BEDOUIN_HEALER)
                    {
                        damage = attackerData.BaseMeleeDamage; // Cap at base
                    }
                    else
                    {
                        damage = attackerData.BaseMeleeDamage * 2.0f; // Double damage
                    }
                }
                else
                {
                    // Everyone else: flat base damage
                    // Unit-specific modifiers will be applied later for special cases (e.g., ARAB_SLAVE vs Light)
                    damage = attackerData.BaseMeleeDamage;
                }
            }
            // Normal weapons: BaseDamage × WeaponVsArmorMultiplier × ArmorValue
            else
            {
                // Special case: Strong melee vs SiegeDefense bypasses lookup table
                // ARAB_SWORDSMAN (100) vs TREBUCHET: Game=40 = 100 * 0.4
                // BEDOUIN_DEMOLISHER (40) vs TREBUCHET: Game=40 = 40 * 1.0
                // KNIGHT (80) vs TREBUCHET: Game=30 = 80 * 0.375
                // LORD (150) vs TREBUCHET: Game=150 = 150 * 1.0
                if ((defenderData.HasTag("SiegeDefense") || defenderData.HasTag("Armor_Siege")) &&
                    (weaponCategory == WeaponCategory.Sword || weaponCategory == WeaponCategory.Mace || 
                     weaponCategory == WeaponCategory.Lance || weaponCategory == WeaponCategory.Axe || 
                     weaponCategory == WeaponCategory.Polearm))
                {
                    // Strong melee vs SiegeDefense: use weapon-specific multipliers
                    float siegeMultiplier;
                    if (attackerData.IsArmorPiercing)
                    {
                        siegeMultiplier = 1.0f; // Lord: base * 1.0
                    }
                    else if (weaponCategory == WeaponCategory.Sword)
                    {
                        siegeMultiplier = 0.4f; // Sword: base * 0.4
                    }
                    else if (weaponCategory == WeaponCategory.Mace)
                    {
                        siegeMultiplier = 1.0f; // Mace: base * 1.0
                    }
                    else if (weaponCategory == WeaponCategory.Lance)
                    {
                        siegeMultiplier = 0.375f; // Lance: base * 0.375
                    }
                    else
                    {
                        siegeMultiplier = 1.0f; // Axe/Polearm: base * 1.0
                    }
                    
                    damage = attackerData.BaseMeleeDamage * siegeMultiplier;
                }
                else
                {
                    float weaponVsArmorMultiplier = GetWeaponVsArmorMultiplier(weaponCategory, armorCategory);
                    
                    // Armor_Piercing ignores armor reduction vs Heavy/Medium, but has cap vs Unarmored
                    float effectiveArmorValue;
                    if (attackerData.IsArmorPiercing)
                    {
                        // Lord vs Heavy/Medium: ignores armor (treats as 1.0)
                        // Lord vs Unarmored: capped at 1.33x (200/150 = 1.33)
                        if (defenderData.ArmorValue >= 2.0f)
                            effectiveArmorValue = 1.33f;  // Cap vs unarmored
                        else
                            effectiveArmorValue = 1.0f;  // Ignore armor reduction
                    }
                    else
                    {
                        effectiveArmorValue = defenderData.ArmorValue;
                    }
                    
                    damage = attackerData.BaseMeleeDamage * weaponVsArmorMultiplier * effectiveArmorValue;
                }
                
                // Apply damage caps for specific weapon types vs unarmored/weak targets
                if (weaponCategory == WeaponCategory.Sword && attackerData.BaseMeleeDamage <= 20)
                {
                    // ARAB_BOW (20) vs Unarmored (2.0): Game=30, Calc=40 → Cap at base * 1.5
                    // ARAB_BOW (20) vs Medium (1.0): Game=10-15, Calc=20 → Cap at base * 0.5-0.75
                    // ARAB_BOW (20) vs Light (1.5): Game=30, Calc=30 → No cap needed (20 * 1.0 * 1.5 = 30)
                    if (defenderData.ArmorValue >= 2.0f)
                    {
                        damage = Math.Min(damage, attackerData.BaseMeleeDamage * 1.5f); // Cap vs unarmored
                    }
                    else if (defenderData.ArmorValue >= 1.0f && defenderData.ArmorValue < 1.5f)
                    {
                        // Medium armor: cap varies by defender (0.5-0.75), use 0.75 as default
                        damage = Math.Min(damage, attackerData.BaseMeleeDamage * 0.75f);
                    }
                    // Light armor (1.5): no cap needed - calculation is already correct
                }
                else if (weaponCategory == WeaponCategory.Mace && defenderData.ArmorValue >= 2.0f)
                {
                    // Mace vs Unarmored: cap at base * 1.0 for medium-strength units (40-50)
                    // BEDOUIN_DEMOLISHER (40) vs ARAB_SLAVE (2.0): Game=40, Calc=80 → Cap at base * 1.0
                    // MACEMAN (75) vs ARAB_SLAVE (2.0): Game=150, Calc=150 → No cap for strong units
                    if (attackerData.BaseMeleeDamage > 20 && attackerData.BaseMeleeDamage <= 50)
                    {
                        damage = Math.Min(damage, attackerData.BaseMeleeDamage * 1.0f); // Cap at base
                    }
                    // Strong Mace units (75+): no cap, deal full damage (base * 2.0)
                }
                else if (weaponCategory == WeaponCategory.Lance && attackerData.HasTag("Cavalry") && defenderData.ArmorValue >= 2.0f)
                {
                    // Lance Cavalry vs Unarmored
                    // BEDOUIN_CAMEL_LANCER (80) vs ARAB_SLAVE (2.0): Game=160, Calc=80 → Should be base * 2.0 (no cap)
                    // BEDOUIN_HEAVY_CAMEL (40) vs ARAB_SLAVE (2.0): Game=40, Calc=80 → Cap at base * 1.0
                    if (attackerData.BaseMeleeDamage > 20 && attackerData.BaseMeleeDamage <= 50)
                    {
                        damage = Math.Min(damage, attackerData.BaseMeleeDamage * 1.0f); // Cap at base
                    }
                    // Strong Lance units (80+): no cap, deal full damage (base * 2.0)
                }
                else if (weaponCategory == WeaponCategory.Axe && defenderData.ArmorValue >= 2.0f && attackerData.BaseMeleeDamage <= 20)
                {
                    // Small Axe units vs Unarmored: deal full damage (base * 2.0), no cap
                    // QUARRY_GRUNT (20) vs ARAB_SLAVE: Game=40, Calc=40 → No cap needed
                    // The calculation 20 * 1.0 * 2.0 = 40 is correct, so no cap
                }
                else if ((weaponCategory == WeaponCategory.Mace || 
                         (weaponCategory == WeaponCategory.Lance && attackerData.HasTag("Cavalry"))) &&
                         defenderData.ArmorValue >= 2.0f && attackerData.BaseMeleeDamage <= 20)
                {
                    // Small Mace/Lance units vs Unarmored: cap at base * 1.25
                    // ARAB_HORSEMAN (20) vs ARAB_SLAVE (2.0): Game=25, Calc=40 → Cap at base * 1.25
                    damage = Math.Min(damage, attackerData.BaseMeleeDamage * 1.25f);
                }
                else if (weaponCategory == WeaponCategory.Beast)
                {
                    // Beast units vs Unarmored: cap at base damage (not double)
                    // CAMEL (100) vs ARAB_SLAVE (2.0): Game=100, Calc=200 → Cap at base * 1.0
                    // CROCODILE (100) vs ARAB_SLAVE (2.0): Game=100, Calc=200 → Cap at base * 1.0
                    // WAR_DOG (50) vs ARAB_SLAVE (2.0): Game=50, Calc=100 → Cap at base * 1.0
                    if (defenderData.ArmorValue >= 2.0f)
                    {
                        damage = Math.Min(damage, attackerData.BaseMeleeDamage * 1.0f); // Cap at base damage
                    }
                }
            }

            // Apply tag vs tag modifiers (Polearm vs Ladderman, etc.)
            float tagModifier = GetTagVsTagModifier(attackerData, defenderData);
            damage *= tagModifier;

            // Apply unit-specific modifier if exists
            float specialModifier = attackerData.GetModifierAgainst(defender);
            damage *= specialModifier;

            // Round and apply minimum damage floor
            int finalDamage = (int)Math.Round(damage);
            return Math.Max(MinimumDamage, finalDamage);
        }

        /// <summary>
        /// Get the weapon category for an attacker based on their tags.
        /// Priority: Beast > Weapon tags > Ranged tags > Unarmed
        /// Units with both Weapon and Ranged tags use Weapon category for melee.
        /// </summary>
        private static WeaponCategory GetWeaponCategory(UnitDamageData attacker)
        {
            // Check in order of specificity
            if (attacker.HasTag("Beast"))
                return WeaponCategory.Beast;

            // Weapon tags take priority over Ranged tags (for melee damage)
            if (attacker.HasTag("Weapon_Dagger") || attacker.HasTag("Assassin"))
                return WeaponCategory.Dagger;

            if (attacker.HasTag("Weapon_Lance"))
                return WeaponCategory.Lance;

            if (attacker.HasTag("Weapon_Polearm"))
                return WeaponCategory.Polearm;

            if (attacker.HasTag("Weapon_Axe"))
                return WeaponCategory.Axe;

            if (attacker.HasTag("Weapon_Mace"))
                return WeaponCategory.Mace;

            if (attacker.HasTag("Weapon_Sword"))
                return WeaponCategory.Sword;

            // Only check Ranged tags if no Weapon tag was found
            // (Ranged units without melee weapons use Ranged category in melee)
            if (attacker.HasTag("Ranged_Bow") || attacker.HasTag("Ranged_Crossbow") ||
                attacker.HasTag("Ranged_Sling") || attacker.HasTag("Ranged_Javelin"))
                return WeaponCategory.Ranged;

            // Default to unarmed
            return WeaponCategory.Unarmed;
        }

        /// <summary>
        /// Get the armor category for a defender based on their tags.
        /// </summary>
        private static ArmorCategory GetArmorCategory(UnitDamageData defender)
        {
            if (defender.HasTag("Armor_Siege") || defender.HasTag("SiegeDefense"))
                return ArmorCategory.Siege;

            if (defender.HasTag("Armor_Heavy"))
                return ArmorCategory.Heavy;

            if (defender.HasTag("Armor_Light"))
                return ArmorCategory.Light;

            if (defender.HasTag("Armor_None"))
                return ArmorCategory.None;

            // Default to Medium if no armor tag specified
            return ArmorCategory.Medium;
        }

        /// <summary>
        /// Look up the weapon vs armor multiplier from the table.
        /// </summary>
        private static float GetWeaponVsArmorMultiplier(WeaponCategory weapon, ArmorCategory armor)
        {
            if (WeaponVsArmorTable.TryGetValue((weapon, armor), out float multiplier))
            {
                return multiplier;
            }
            return 1.0f; // Default if not found
        }

        /// <summary>
        /// Get modifier for specific tag vs tag combinations.
        /// </summary>
        private static float GetTagVsTagModifier(UnitDamageData attacker, UnitDamageData defender)
        {
            float modifier = 1.0f;

            foreach (var entry in TagVsTagModifiers)
            {
                var (attackerTag, defenderTag) = entry.Key;
                if (attacker.HasTag(attackerTag) && defender.HasTag(defenderTag))
                {
                    modifier *= entry.Value;
                }
            }

            return modifier;
        }

        /// <summary>
        /// Special damage calculation for Assassin (Dagger).
        /// Assassin has unique rules that don't follow the standard lookup table.
        /// </summary>
        private static float CalculateAssassinDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;

            // Assassin formula from game data:
            // vs Medium (1.0): flat base damage (80)
            // vs Heavy (0.5): flat base damage (80) - ignores armor reduction
            // vs Light (1.5): varies by unit (250 for Slinger, 150 for Eunuch)
            // vs Unarmored (2.0): 250 = base * 3.125
            // vs Siege (0.4): 2 (minimum damage)
            
            if (armorValue <= 0.5f)
            {
                // Heavy or Siege: flat base damage (but minimum damage applies)
                return baseDamage;
            }
            else if (armorValue <= 1.0f)
            {
                // Medium: flat base damage
                return baseDamage;
            }
            else if (armorValue >= 2.0f)
            {
                // Unarmored: 3.125x multiplier
                return baseDamage * 3.125f;
            }
            else
            {
                // Light armor (1.5): varies by defender type
                // ARAB_SLINGER: 250 = 80 * 3.125
                // BEDOUIN_EUNUCH: 150 = 80 * 1.875
                // Use average: base * 2.5
                return baseDamage * 2.5f;
            }
        }

        /// <summary>
        /// Special damage calculation for Ranged units in melee combat.
        /// Ranged units use a different formula when fighting in melee.
        /// Formula: base * multiplier * armorValue, with damage caps.
        /// </summary>
        private static float CalculateRangedMeleeDamage(UnitDamageData attacker, UnitDamageData defender)
        {
            float baseDamage = attacker.BaseMeleeDamage;
            float armorValue = defender.ArmorValue;

            // Ranged melee formula from CSV data:
            // ARCHER (10) vs ARAB_ASSASIN (1.0): 10 = 10 * 1.0 * 1.0
            // ARCHER (10) vs ARAB_BOW (1.0, Ranged): 15 = 10 * 1.5 * 1.0
            // ARCHER (10) vs ARAB_SLAVE (2.0): 20 = 10 * 1.0 * 2.0
            // ARCHER (10) vs ARAB_SLINGER (1.5, Ranged): 20 = 10 * 1.33 * 1.5
            // ARAB_SLINGER (20) vs ARAB_SLAVE (2.0): 25 = 20 * 1.0 * 2.0 = 40, but capped at 25
            // ARAB_SLINGER (20) vs ARAB_SLINGER (1.5): 25 = 20 * 1.0 * 1.5 = 30, but capped at 25
            
            float multiplier;
            bool defenderIsRanged = defender.IsRangedUnit;
            
            if (armorValue >= 2.0f)
            {
                // Unarmored: 1.0x multiplier, but capped at base * 1.5 for some units
                multiplier = 1.0f;
            }
            else if (armorValue >= 1.5f)
            {
                // Light armor: 1.33x if defender is ranged, 1.0x if not
                multiplier = defenderIsRanged ? 1.33f : 1.0f;
            }
            else if (armorValue >= 1.0f)
            {
                // Medium armor (1.0): Always 1.0x multiplier
                // ARCHER (10) vs ARAB_BOW (1.0): 10 = 10 * 1.0 * 1.0
                // ARCHER (10) vs ARCHER (1.0): 10 = 10 * 1.0 * 1.0
                multiplier = 1.0f;
            }
            else
            {
                // Heavy (0.5): 3.0x
                multiplier = 3.0f;
            }
            
            float damage = baseDamage * armorValue * multiplier;
            
            // Apply damage cap based on armor value
            // ARCHER (10) vs ARAB_SLAVE (2.0): 20 = 10 * 2.0 (cap = base * 2.0)
            // ARAB_SLINGER (20) vs ARAB_SLAVE (2.0): 25 = 20 * 1.25 (cap = base * 1.25)
            // ARAB_SLINGER (20) vs ARAB_SLINGER (1.5): 25 = 20 * 1.25 (cap = base * 1.25)
            // ARCHER (10) vs ARAB_SLINGER (1.5): 20 = 10 * 2.0 (cap = base * 2.0)
            
            float cap;
            if (armorValue >= 2.0f)
            {
                // Unarmored: cap at base * 2.0 for small units, base * 1.25 for larger
                cap = baseDamage <= 10 ? baseDamage * 2.0f : baseDamage * 1.25f;
            }
            else if (armorValue >= 1.5f)
            {
                // Light armor: cap at base * 1.25 for larger units
                cap = baseDamage <= 10 ? baseDamage * 2.0f : baseDamage * 1.25f;
            }
            else
            {
                // Medium/Heavy: no cap
                cap = float.MaxValue;
            }
            
            return Math.Min(damage, cap);
        }

        /// <summary>
        /// Verify a melee damage calculation against expected value.
        /// </summary>
        public static bool VerifyMeleeDamage(eChimps attacker, eChimps defender, int expectedDamage)
        {
            int calculated = CalculateMeleeDamage(attacker, defender);
            return calculated == expectedDamage;
        }

        /// <summary>
        /// Diagnostic for a specific attacker-defender pair.
        /// </summary>
        public static string DiagnoseMeleeDamage(eChimps attacker, eChimps defender)
        {
            var attackerData = UnitDamageRegistry.GetUnitData(attacker);
            var defenderData = UnitDamageRegistry.GetUnitData(defender);

            if (attackerData == null)
                return $"{attacker}: No data in registry";
            if (defenderData == null)
                return $"{defender}: No data in registry";

            var weaponCat = GetWeaponCategory(attackerData);
            var armorCat = GetArmorCategory(defenderData);
            float multiplier = GetWeaponVsArmorMultiplier(weaponCat, armorCat);
            float tagMod = GetTagVsTagModifier(attackerData, defenderData);

            return $"{attacker} -> {defender}:\n" +
                   $"  Base: {attackerData.BaseMeleeDamage}\n" +
                   $"  Weapon: {weaponCat}, Armor: {armorCat}\n" +
                   $"  WeaponVsArmor: {multiplier:F3}\n" +
                   $"  TagModifier: {tagMod:F3}\n" +
                   $"  Final: {CalculateMeleeDamage(attacker, defender)}";
        }
    }
}
