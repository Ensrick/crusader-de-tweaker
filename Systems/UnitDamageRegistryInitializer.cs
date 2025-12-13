// Systems/UnitDamageRegistryInitializer.cs
using System;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Systems
{
    /// <summary>
    /// Initializes the UnitDamageRegistry with unit damage data.
    /// 
    /// Uses a Tag-based system with Weapon vs Armor lookup table:
    /// - Armor Category Tags: Armor_None, Armor_Light, Armor_Medium, Armor_Heavy, Armor_Siege
    /// - Weapon Category Tags: Weapon_Sword, Weapon_Mace, Weapon_Polearm, Weapon_Lance, Weapon_Axe, Weapon_Dagger, Weapon_Unarmed
    /// - Ranged Tags: Ranged_Bow, Ranged_Crossbow, Ranged_Sling, Ranged_Javelin
    /// - Special Tags: Beast, Cavalry, Ladderman, Hunter, Predator, SmallPrey, SiegeDefense
    /// </summary>
    internal static class UnitDamageRegistryInitializer
    {
        public static void Initialize()
        {
            Plugin.Logger.LogInfo("Initializing Unit Damage Registry...");

            // Heavy armor units (ArmorValue = 0.5)
            InitializeHeavyArmorUnits();
            
            // Medium armor units (ArmorValue = 1.0)
            InitializeMediumArmorUnits();
            
            // Light armor units (ArmorValue = 1.0-1.5)
            InitializeLightArmorUnits();
            
            // Unarmored units (ArmorValue = 2.0)
            InitializeUnarmoredUnits();
            
            // No armor units (civilians, animals)
            InitializeNoArmorUnits();
            
            // Special units (siege, beasts, elites)
            InitializeSiegeUnits();
            InitializeBeastUnits();
            InitializeEliteUnits();

            Plugin.Logger.LogInfo($"Unit Damage Registry initialized with {UnitDamageRegistry.GetRegisteredCount()} units");
        }

        // ============================================
        // ARMOR CATEGORY: HEAVY (ArmorValue = 3)
        // Takes half damage from most attacks
        // ============================================
        private static void InitializeHeavyArmorUnits()
        {
            // Knight - heavy cavalry
            RegisterUnit(eChimps.CHIMP_TYPE_KNIGHT, 80, 3, 
                "Armor_Heavy", "Weapon_Lance", "Cavalry");

            // Swordsman - heavy infantry
            RegisterUnit(eChimps.CHIMP_TYPE_SWORDSMAN, 100, 3, 
                "Armor_Heavy", "Weapon_Sword");
        }

        // ============================================
        // ARMOR CATEGORY: MEDIUM (ArmorValue = 2)
        // Takes normal damage
        // ============================================
        private static void InitializeMediumArmorUnits()
        {
            // Arab Swordsman
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, 100, 2, 
                "Armor_Medium", "Weapon_Sword");

            // Maceman
            RegisterUnit(eChimps.CHIMP_TYPE_MACEMAN, 75, 2, 
                "Armor_Medium", "Weapon_Mace");

            // Pikeman
            RegisterUnit(eChimps.CHIMP_TYPE_PIKEMAN, 20, 2, 
                "Armor_Medium", "Weapon_Polearm");

            // Spearman
            RegisterUnit(eChimps.CHIMP_TYPE_SPEARMAN, 20, 2, 
                "Armor_Medium", "Weapon_Polearm");

            // Crossbowman
            RegisterUnit(eChimps.CHIMP_TYPE_XBOWMAN, 10, 2, 
                "Armor_Medium", "Ranged_Crossbow", "Weapon_Unarmed");

            // Monk
            RegisterUnit(eChimps.CHIMP_TYPE_MONK, 50, 2, 
                "Armor_Medium", "Weapon_Mace");

            // Bedouin Heavy Camel
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, 40, 2, 
                "Armor_Medium", "Weapon_Lance", "Cavalry");

            // Bedouin Camel Lancer
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, 80, 2, 
                "Armor_Medium", "Weapon_Lance", "Cavalry", "Ranged_Javelin");

            // Bedouin Demolisher
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, 40, 2, 
                "Armor_Medium", "Weapon_Mace");

            // Arab Horseman
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, 20, 2, 
                "Armor_Medium", "Weapon_Sword", "Cavalry");

            // Arab Ballista (crew)
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_BALLISTA, 10, 2, 
                "Armor_Medium", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: LIGHT (ArmorValue = 1.0-1.5)
        // Takes 50% more damage from most attacks
        // ============================================
        private static void InitializeLightArmorUnits()
        {
            // Arab Slinger - ArmorValue 1.5
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SLINGER, 20, 1, 
                "Armor_Light", "Ranged_Sling", "Weapon_Unarmed");

            // Bedouin Eunuch - ArmorValue 1.5
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 10, 1, 
                "Armor_Light", "Weapon_Sword");

            // Archers - ArmorValue 1.0 but Light armor category
            RegisterUnit(eChimps.CHIMP_TYPE_ARCHER, 10, 2, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_BOW, 10, 2, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, 10, 2, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, 15, 2, 
                "Armor_Light", "Ranged_Javelin", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_HUNTER, 10, 2, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed", "Hunter");

            // Arab Grenadier
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_GRENADIER, 15, 2, 
                "Armor_Light", "Weapon_Unarmed");

            // Workers with weapons (Light protection)
            RegisterUnit(eChimps.CHIMP_TYPE_BLACKSMITH, 20, 2, 
                "Armor_Light", "Weapon_Mace");
            
            RegisterUnit(eChimps.CHIMP_TYPE_WOODCUTTER, 20, 2, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_GRUNT, 20, 2, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_MASON, 20, 2, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_TUNNELER, 25, 2, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, 25, 2, 
                "Armor_Light", "Weapon_Axe");

            // Engineer
            RegisterUnit(eChimps.CHIMP_TYPE_ENGINEER, 2, 2, 
                "Armor_Light", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: UNARMORED (ArmorValue = 2.0)
        // Takes double damage
        // ============================================
        private static void InitializeUnarmoredUnits()
        {
            // Arab Slave
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SLAVE, 10, 0, 
                "Armor_None", "Weapon_Unarmed");

            // Bedouin Healer
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 10, 0, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: NONE (civilians, weak animals)
        // ArmorValue = 1.0, no protection bonuses
        // ============================================
        private static void InitializeNoArmorUnits()
        {
            // Standard civilians - all deal 2 base damage (weak attacker rule)
            RegisterUnit(eChimps.CHIMP_TYPE_PEASANT, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_LADDERMAN, 2, 2, 
                "Armor_None", "Weapon_Unarmed", "Ladderman");

            // Workers
            RegisterUnit(eChimps.CHIMP_TYPE_FLETCHER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_ARMOURER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TANNER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_POLETURNER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BAKER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BREWER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MILLER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PITCHMAN, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MINER1, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MINER2, 2, 2, 
                "Armor_None", "Weapon_Unarmed");

            // Farmers
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_WHEAT, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_HOPS, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_APPLE, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_CATTLE, 2, 2, 
                "Armor_None", "Weapon_Unarmed");

            // Castle staff
            RegisterUnit(eChimps.CHIMP_TYPE_LADY, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PRIEST, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_HEALER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FIREMAN, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_DRUNKARD, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_INNKEEPER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TRADER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TRADER_HORSE, 2, 2, 
                "Armor_None", "Weapon_Unarmed");

            // Entertainers
            RegisterUnit(eChimps.CHIMP_TYPE_JESTER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_JUGGLER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FIREEATER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");

            // Weak animals (not beasts)
            RegisterUnit(eChimps.CHIMP_TYPE_DEER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_RABBIT, 2, 2, 
                "Armor_None", "Weapon_Unarmed", "SmallPrey");
            RegisterUnit(eChimps.CHIMP_TYPE_CHICKEN, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_COW, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_GOAT, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_CROW, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_SEAGULL, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_OX, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // SIEGE UNITS
        // ============================================
        private static void InitializeSiegeUnits()
        {
            // Trebuchet - special SiegeDefense, ArmorValue 0.4
            RegisterUnit(eChimps.CHIMP_TYPE_TREBUCHET, 2, 4, 
                "Armor_Siege", "Weapon_Unarmed", "SiegeDefense");

            // Other siege equipment
            RegisterUnit(eChimps.CHIMP_TYPE_BATTERING_RAM, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_SIEGE_TOWER, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PORTABLE_SHIELD, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_CATAPULT, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MANGONEL, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BALLISTA, 2, 2, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // BEAST UNITS - Natural weapons, flat damage
        // ============================================
        private static void InitializeBeastUnits()
        {
            // Large predators
            RegisterUnit(eChimps.CHIMP_TYPE_LION, 100, 2, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_HYENA, 100, 2, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_CROCODILE, 100, 2, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_CAMEL, 100, 2, 
                "Armor_None", "Beast");

            // War Dog
            RegisterUnit(eChimps.CHIMP_TYPE_WAR_DOG, 50, 2, 
                "Armor_None", "Beast");

            // Regular Dog - predator instinct
            RegisterUnit(eChimps.CHIMP_TYPE_DOG, 10, 2, 
                "Armor_None", "Beast", "Predator");
        }

        // ============================================
        // ELITE UNITS - Special abilities
        // ============================================
        private static void InitializeEliteUnits()
        {
            // Lord - Armor piercing
            RegisterUnit(eChimps.CHIMP_TYPE_LORD, 150, 2, 
                "Armor_Medium", "Weapon_Sword", "Armor_Piercing");

            // Arab Assassin - Dagger, high damage vs unarmored
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_ASSASIN, 80, 2, 
                "Armor_Light", "Weapon_Dagger", "Assassin");
        }

        // ============================================
        // HELPER METHOD
        // ============================================
        private static void RegisterUnit(eChimps unit, int baseDamage, int armorValue, params string[] tags)
        {
            var data = new UnitDamageData
            {
                Unit = unit,
                BaseMeleeDamage = baseDamage,
                ArmorValue = armorValue
            };

            foreach (var tag in tags)
            {
                data.AddTag(tag);
            }

            UnitDamageRegistry.RegisterUnit(data);
        }
    }
}
