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

            // Unit-specific modifiers for unique behaviors
            InitializeUnitSpecificModifiers();

            Plugin.Logger.LogInfo($"Unit Damage Registry initialized with {UnitDamageRegistry.GetRegisteredCount()} units");
        }

        // ============================================
        // ARMOR CATEGORY: HEAVY (ArmorValue = 3)
        // Takes half damage from most attacks
        // ============================================
        private static void InitializeHeavyArmorUnits()
        {
            // Knight - heavy cavalry
            RegisterUnit(eChimps.CHIMP_TYPE_KNIGHT, 80, 0.5f, 
                "Armor_Heavy", "Weapon_Lance", "Cavalry");

            // Swordsman - heavy infantry
            RegisterUnit(eChimps.CHIMP_TYPE_SWORDSMAN, 100, 0.5f, 
                "Armor_Heavy", "Weapon_Sword");
        }

        // ============================================
        // ARMOR CATEGORY: MEDIUM (ArmorValue = 2)
        // Takes normal damage
        // ============================================
        private static void InitializeMediumArmorUnits()
        {
            // Arab Swordsman
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, 100, 1.0f, 
                "Armor_Medium", "Weapon_Sword");

            // Maceman
            RegisterUnit(eChimps.CHIMP_TYPE_MACEMAN, 75, 1.0f, 
                "Armor_Medium", "Weapon_Mace");

            // Pikeman
            RegisterUnit(eChimps.CHIMP_TYPE_PIKEMAN, 20, 1.0f, 
                "Armor_Medium", "Weapon_Polearm");

            // Spearman
            RegisterUnit(eChimps.CHIMP_TYPE_SPEARMAN, 20, 1.0f, 
                "Armor_Medium", "Weapon_Polearm");

            // Crossbowman
            RegisterUnit(eChimps.CHIMP_TYPE_XBOWMAN, 10, 1.0f, 
                "Armor_Medium", "Ranged_Crossbow", "Weapon_Unarmed");

            // Monk
            RegisterUnit(eChimps.CHIMP_TYPE_MONK, 50, 1.0f, 
                "Armor_Medium", "Weapon_Mace");

            // Bedouin Heavy Camel
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL, 40, 1.0f, 
                "Armor_Medium", "Weapon_Lance", "Cavalry");

            // Bedouin Camel Lancer
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, 80, 1.0f, 
                "Armor_Medium", "Weapon_Lance", "Cavalry", "Ranged_Javelin");

            // Bedouin Demolisher
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER, 40, 1.0f, 
                "Armor_Medium", "Weapon_Mace");

            // Arab Horseman
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_HORSEMAN, 20, 1.0f, 
                "Armor_Medium", "Weapon_Sword", "Cavalry");

            // Arab Ballista (crew)
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_BALLISTA, 10, 1.0f, 
                "Armor_Medium", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: LIGHT (ArmorValue = 1.0-1.5)
        // Takes 50% more damage from most attacks
        // ============================================
        private static void InitializeLightArmorUnits()
        {
            // Arab Slinger - ArmorValue 1.5
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SLINGER, 20, 1.5f, 
                "Armor_Light", "Ranged_Sling", "Weapon_Unarmed");

            // Bedouin Eunuch - ArmorValue 1.5
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 10, 1.5f, 
                "Armor_Light", "Weapon_Sword");

            // Archers - ArmorValue 1.0 but Light armor category
            RegisterUnit(eChimps.CHIMP_TYPE_ARCHER, 10, 1.0f, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_BOW, 20, 1.0f, 
                "Armor_Light", "Ranged_Bow", "Weapon_Sword");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, 10, 1.0f, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, 15, 1.0f, 
                "Armor_Light", "Ranged_Javelin", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_HUNTER, 10, 1.0f, 
                "Armor_Light", "Ranged_Bow", "Weapon_Unarmed", "Hunter");

            // Arab Grenadier
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_GRENADIER, 15, 1.0f, 
                "Armor_Light", "Weapon_Unarmed");

            // Workers with weapons (Light protection)
            RegisterUnit(eChimps.CHIMP_TYPE_BLACKSMITH, 20, 1.0f, 
                "Armor_Light", "Weapon_Mace");
            
            RegisterUnit(eChimps.CHIMP_TYPE_WOODCUTTER, 20, 1.0f, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_GRUNT, 20, 1.0f, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_MASON, 20, 1.0f, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_TUNNELER, 25, 1.0f, 
                "Armor_Light", "Weapon_Axe");
            
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_SAPPER, 25, 1.0f, 
                "Armor_Light", "Weapon_Axe");

            // Engineer
            RegisterUnit(eChimps.CHIMP_TYPE_ENGINEER, 2, 1.0f, 
                "Armor_Light", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: UNARMORED (ArmorValue = 2.0)
        // Takes double damage
        // ============================================
        private static void InitializeUnarmoredUnits()
        {
            // Arab Slave
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_SLAVE, 10, 2.0f, 
                "Armor_None", "Weapon_Unarmed");

            // Bedouin Healer
            RegisterUnit(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 10, 2.0f, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // ARMOR CATEGORY: NONE (civilians, weak animals)
        // ArmorValue = 1.0, no protection bonuses
        // ============================================
        private static void InitializeNoArmorUnits()
        {
            // Standard civilians - all deal 2 base damage (weak attacker rule)
            RegisterUnit(eChimps.CHIMP_TYPE_PEASANT, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            
            RegisterUnit(eChimps.CHIMP_TYPE_LADDERMAN, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed", "Ladderman");

            // Workers
            RegisterUnit(eChimps.CHIMP_TYPE_FLETCHER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_ARMOURER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TANNER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_POLETURNER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BAKER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BREWER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MILLER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PITCHMAN, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MINER1, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MINER2, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");

            // Farmers
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_WHEAT, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_HOPS, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_APPLE, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FARMER_CATTLE, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");

            // Castle staff
            RegisterUnit(eChimps.CHIMP_TYPE_LADY, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PRIEST, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_HEALER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FIREMAN, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_DRUNKARD, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_INNKEEPER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TRADER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_TRADER_HORSE, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");

            // Entertainers
            RegisterUnit(eChimps.CHIMP_TYPE_JESTER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_JUGGLER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_FIREEATER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");

            // Weak animals (not beasts)
            RegisterUnit(eChimps.CHIMP_TYPE_DEER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_RABBIT, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed", "SmallPrey");
            RegisterUnit(eChimps.CHIMP_TYPE_CHICKEN, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_COW, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_GOAT, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_CROW, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_SEAGULL, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_QUARRY_OX, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // SIEGE UNITS
        // ============================================
        private static void InitializeSiegeUnits()
        {
            // Trebuchet - special SiegeDefense, ArmorValue 0.4
            RegisterUnit(eChimps.CHIMP_TYPE_TREBUCHET, 2, 0.4f, 
                "Armor_Siege", "Weapon_Unarmed", "SiegeDefense");

            // Other siege equipment
            RegisterUnit(eChimps.CHIMP_TYPE_BATTERING_RAM, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_SIEGE_TOWER, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_PORTABLE_SHIELD, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_CATAPULT, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_MANGONEL, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
            RegisterUnit(eChimps.CHIMP_TYPE_BALLISTA, 2, 1.0f, 
                "Armor_None", "Weapon_Unarmed");
        }

        // ============================================
        // BEAST UNITS - Natural weapons, flat damage
        // ============================================
        private static void InitializeBeastUnits()
        {
            // Large predators
            RegisterUnit(eChimps.CHIMP_TYPE_LION, 100, 1.0f, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_HYENA, 100, 1.0f, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_CROCODILE, 100, 1.0f, 
                "Armor_None", "Beast");
            RegisterUnit(eChimps.CHIMP_TYPE_CAMEL, 100, 1.0f, 
                "Armor_None", "Beast");

            // War Dog
            RegisterUnit(eChimps.CHIMP_TYPE_WAR_DOG, 50, 1.0f, 
                "Armor_None", "Beast");

            // Regular Dog - predator instinct
            RegisterUnit(eChimps.CHIMP_TYPE_DOG, 10, 1.0f, 
                "Armor_None", "Beast", "Predator");
        }

        // ============================================
        // ELITE UNITS - Special abilities
        // ============================================
        private static void InitializeEliteUnits()
        {
            // Lord - Armor piercing
            RegisterUnit(eChimps.CHIMP_TYPE_LORD, 150, 1.0f, 
                "Armor_Medium", "Weapon_Sword", "Armor_Piercing");

            // Arab Assassin - Dagger, high damage vs unarmored
            RegisterUnit(eChimps.CHIMP_TYPE_ARAB_ASSASIN, 80, 1.0f, 
                "Armor_Light", "Weapon_Dagger", "Assassin");
        }

        // ============================================
        // UNIT-SPECIFIC MODIFIERS
        // Units with unique behaviors that don't fit the standard formula
        // ============================================
        private static void InitializeUnitSpecificModifiers()
        {
            // ARAB_BOW: Uses a different melee weapon (possibly a scimitar or curved blade)
            // Has unique damage patterns that don't match standard Sword behavior
            // vs Medium armor (1.0): Most defenders take 0.5x damage, some take 0.75x
            // vs Light armor (1.5): Takes 1.5x damage (no cap)
            // vs Unarmored (2.0): Capped at 1.5x base
            var arabBow = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_BOW);
            if (arabBow != null)
            {
                // Most Medium armor defenders: 0.5x multiplier
                // This makes 20 * 1.0 * 1.0 * 0.5 = 10
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_ARCHER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_ARMOURER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_BAKER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_BALLISTA, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_BATTERING_RAM, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_BLACKSMITH, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_BREWER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_CAMEL, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_CATAPULT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_CHICKEN, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_COW, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_CROCODILE, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_CROW, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_DEER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_DOG, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_DRUNKARD, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_ENGINEER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FARMER_APPLE, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FARMER_CATTLE, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FARMER_HOPS, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FARMER_WHEAT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FIREEATER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FIREMAN, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_FLETCHER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_GOAT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_HEALER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_HUNTER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_HYENA, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_INNKEEPER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_JESTER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_JUGGLER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_LADDERMAN, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_LADY, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_LION, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_LORD, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MANGONEL, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MILLER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MINER1, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MINER2, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MONK, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_PEASANT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_PITCHMAN, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_POLETURNER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_PORTABLE_SHIELD, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_PRIEST, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_QUARRY_GRUNT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_QUARRY_MASON, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_QUARRY_OX, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_RABBIT, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_SEAGULL, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_SIEGE_TOWER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_TANNER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_TRADER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_TRADER_HORSE, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_TUNNELER, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_WAR_DOG, 0.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_WOODCUTTER, 0.5f);

                // Some Medium armor defenders: 0.75x multiplier (MACEMAN, PIKEMAN, SPEARMAN, XBOWMAN, ARCHER)
                // This makes 20 * 1.0 * 1.0 * 0.75 = 15
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_ARCHER, 0.75f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_MACEMAN, 0.75f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_PIKEMAN, 0.75f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_SPEARMAN, 0.75f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_XBOWMAN, 0.75f);

                // Heavy armor defenders (0.5): 1.5x multiplier
                // ARAB_BOW vs KNIGHT: Game=15, Calc=10 → 20 * 1.0 * 0.5 * 1.5 = 15
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 1.5f);
                arabBow.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 1.5f);

                // Light armor BEDOUIN_EUNUCH: cap at base * 1.25
                // ARAB_BOW vs BEDOUIN_EUNUCH: Game=25, Calc=30 → Cap at 20 * 1.25 = 25
                // This will be handled by a cap in DamageCalculator
            }

            // ARAB_SLAVE: Uses a torch - fire damage might have special properties
            // vs Light armor (1.5): Torch deals base * armorValue (not flat base)
            // ARAB_SLAVE vs ARAB_SLINGER (1.5): Game=20, Calc=10 → Should be 10 * 2.0 = 20
            // ARAB_SLAVE vs BEDOUIN_EUNUCH (1.5): Game=15, Calc=10 → Should be 10 * 1.5 = 15
            // ARAB_SLAVE vs BEDOUIN_HEALER (2.0): Game=20, Calc=10 → Should be 10 * 2.0 = 20
            var arabSlave = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_SLAVE);
            if (arabSlave != null)
            {
                // Torch vs ARAB_SLINGER: deals base * 2.0 (fire is very effective)
                arabSlave.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLINGER, 2.0f);
                // Torch vs BEDOUIN_EUNUCH: deals base * 1.5 (normal armor multiplier)
                arabSlave.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 1.5f);
                // Torch vs BEDOUIN_HEALER: deals base * 2.0 (double damage, not capped)
                arabSlave.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 2.0f);
            }

            // ARAB_ASSASIN: Special modifiers for Light armor defenders
            // vs ARAB_SLINGER (1.5): Game=250, Calc=200 → needs 1.25x (250/200 = 1.25)
            // vs BEDOUIN_EUNUCH (1.5): Game=150, Calc=200 → needs 0.75x (150/200 = 0.75)
            var arabAssassin = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            if (arabAssassin != null)
            {
                arabAssassin.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLINGER, 1.25f);
                arabAssassin.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 0.75f);
            }

            // ARAB_HORSEMAN: Weak Sword + Cavalry, needs special handling
            // vs Heavy: Game=20, Calc=10 → needs 2.0x multiplier
            // vs Unarmored/Light: handled by caps in DamageCalculator
            var arabHorseman = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_HORSEMAN);
            if (arabHorseman != null)
            {
                arabHorseman.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 2.0f);
                arabHorseman.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 2.0f);
            }

            // BEDOUIN_CAMEL_LANCER: Lance unit with Ranged_Javelin, needs unit-specific modifiers
            // vs ARAB_SWORDSMAN (1.0): Game=30, Calc=80 → needs 0.375x modifier
            // vs KNIGHT (0.5): Game=25, Calc=50 → needs 0.625x modifier
            // vs PIKEMAN (1.0): Game=40, Calc=80 → needs 0.5x modifier
            var camelLancer = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER);
            if (camelLancer != null)
            {
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, 0.375f);
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 0.625f);
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 0.625f);
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_PIKEMAN, 0.5f);
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_MACEMAN, 0.875f); // 70/80 = 0.875
                camelLancer.AddSpecialModifier(eChimps.CHIMP_TYPE_XBOWMAN, 0.875f); // 70/80 = 0.875
            }

            // BEDOUIN_HEAVY_CAMEL: Lance unit, needs unit-specific modifiers
            // vs Light armor: Game=40-50, Calc=60 → needs cap at base * 1.0-1.25
            // vs Unarmored: Game=60, Calc=40 → needs 1.5x modifier
            // vs Heavy: Game=40, Calc=25 → needs 1.6x modifier
            var heavyCamel = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL);
            if (heavyCamel != null)
            {
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLINGER, 0.667f); // 40/60 = 0.667
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 0.833f); // 50/60 = 0.833
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.5f); // 60/40 = 1.5
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 1.6f); // 40/25 = 1.6
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 1.6f); // 40/25 = 1.6
                heavyCamel.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 0.667f); // 10/15 = 0.667
            }

            // MONK: Mace unit, needs special handling vs Unarmored
            // vs ARAB_SLAVE (2.0): Game=80, Calc=50 → needs 1.6x modifier
            // vs BEDOUIN_HEALER (2.0): Game=100, Calc=50 → needs 2.0x modifier
            var monk = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_MONK);
            if (monk != null)
            {
                monk.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLAVE, 1.6f); // 80/50 = 1.6
                monk.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 2.0f); // 100/50 = 2.0
            }

            // BEDOUIN_EUNUCH: Weak Sword unit, needs caps vs Unarmored/Light
            // vs ARAB_SLAVE (2.0): Game=10, Calc=15 → needs 0.667x modifier
            // vs ARAB_SLINGER (1.5): Game=10, Calc=15 → needs 0.667x modifier
            // vs BEDOUIN_EUNUCH (1.5): Game=10, Calc=12 → needs 0.833x modifier
            // vs BEDOUIN_HEALER (2.0): Game=20, Calc=15 → needs 1.333x modifier
            // vs Heavy: Game=10, Calc=5 → needs 2.0x modifier
            var bedouinEunuch = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH);
            if (bedouinEunuch != null)
            {
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLAVE, 0.667f); // 10/15 = 0.667
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLINGER, 0.667f); // 10/15 = 0.667
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 0.833f); // 10/12 = 0.833
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.333f); // 20/15 = 1.333
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 2.0f);
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 2.0f);
                bedouinEunuch.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 0.5f); // 2/4 = 0.5
            }

            // PIKEMAN: Polearm unit, needs special modifiers
            // vs ARAB_SLINGER (1.5): Game=40, Calc=30 → needs 1.33x modifier
            // vs BEDOUIN_HEALER (2.0): Game=50, Calc=40 → needs 1.25x modifier
            // vs Heavy: Game=20, Calc=10 → needs 2.0x modifier (ignore armor reduction)
            var pikeman = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_PIKEMAN);
            if (pikeman != null)
            {
                pikeman.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLINGER, 1.33f); // 40/30 = 1.33
                pikeman.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.25f); // 50/40 = 1.25
                pikeman.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 2.0f); // 20/10 = 2.0
                pikeman.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 2.0f); // 20/10 = 2.0
                pikeman.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 0.6f); // 12/20 = 0.6
            }

            // SPEARMAN: Polearm unit, needs special modifiers
            // vs BEDOUIN_HEALER (2.0): Game=50, Calc=40 → needs 1.25x modifier
            // vs Heavy: Game=20, Calc=10 → needs 2.0x modifier
            var spearman = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_SPEARMAN);
            if (spearman != null)
            {
                spearman.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.25f); // 50/40 = 1.25
                spearman.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 2.0f); // 20/10 = 2.0
                spearman.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 2.0f); // 20/10 = 2.0
                spearman.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 0.3f); // 6/20 = 0.3
            }

            // ARAB_GRENADIER: Unarmed unit, needs special modifiers
            // vs ARAB_SLAVE (2.0): Game=25, Calc=30 → needs 0.833x modifier
            // vs BEDOUIN_EUNUCH (1.5): Game=20, Calc=15 → needs 1.33x modifier
            // vs BEDOUIN_HEALER (2.0): Game=25, Calc=15 → needs 1.67x modifier
            var arabGrenadier = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_GRENADIER);
            if (arabGrenadier != null)
            {
                arabGrenadier.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLAVE, 0.833f); // 25/30 = 0.833
                arabGrenadier.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 1.33f); // 20/15 = 1.33
                arabGrenadier.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.67f); // 25/15 = 1.67
            }

            // ARAB_SLINGER: Ranged unit in melee, needs special modifiers
            // vs BEDOUIN_HEALER (2.0): Game=30, Calc=25 → needs 1.2x modifier
            // vs Heavy: Game=20, Calc=30 → needs cap at base * 1.0
            var arabSlinger = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_ARAB_SLINGER);
            if (arabSlinger != null)
            {
                arabSlinger.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.2f); // 30/25 = 1.2
                arabSlinger.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 0.667f); // 20/30 = 0.667
                arabSlinger.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 0.667f); // 20/30 = 0.667
            }

            // BEDOUIN_SKIRMISHER: Ranged unit in melee, needs caps
            // vs ARAB_SLAVE (2.0): Game=15, Calc=19 → needs cap at base * 1.0
            // vs ARAB_SLINGER (1.5): Game=15, Calc=19 → needs cap at base * 1.0
            // vs BEDOUIN_EUNUCH (1.5): Game=20, Calc=19 → needs 1.05x modifier
            // vs BEDOUIN_HEALER (2.0): Game=30, Calc=19 → needs 1.58x modifier
            // vs Heavy: Game=15, Calc=22 → needs cap at base * 1.0
            var bedouinSkirmisher = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER);
            if (bedouinSkirmisher != null)
            {
                bedouinSkirmisher.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH, 1.05f); // 20/19 = 1.05
                bedouinSkirmisher.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.58f); // 30/19 = 1.58
                bedouinSkirmisher.AddSpecialModifier(eChimps.CHIMP_TYPE_KNIGHT, 0.682f); // 15/22 = 0.682
                bedouinSkirmisher.AddSpecialModifier(eChimps.CHIMP_TYPE_SWORDSMAN, 0.682f); // 15/22 = 0.682
            }

            // BEDOUIN_HEALER: Unarmed unit, needs special modifiers
            // vs ARAB_SLAVE (2.0): Game=10, Calc=20 → needs 0.5x modifier
            // vs BEDOUIN_HEALER (2.0): Game=20, Calc=10 → needs 2.0x modifier
            var bedouinHealer = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_HEALER);
            if (bedouinHealer != null)
            {
                bedouinHealer.AddSpecialModifier(eChimps.CHIMP_TYPE_ARAB_SLAVE, 0.5f); // 10/20 = 0.5
                bedouinHealer.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 2.0f); // 20/10 = 2.0
            }

            // BEDOUIN_DEMOLISHER: Mace unit, needs special modifier vs BEDOUIN_HEALER
            // vs BEDOUIN_HEALER (2.0): Game=60, Calc=40 → needs 1.5x modifier
            var bedouinDemolisher = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER);
            if (bedouinDemolisher != null)
            {
                bedouinDemolisher.AddSpecialModifier(eChimps.CHIMP_TYPE_BEDOUIN_HEALER, 1.5f); // 60/40 = 1.5
            }

            // HUNTER: Special unit with Hunter tag, needs special modifiers
            // vs ARAB_SLINGER (1.5): Game=15, Calc=20 → needs cap at base * 1.5
            // vs BEDOUIN_EUNUCH (1.5): Game=10, Calc=15 → needs cap at base * 1.0
            // vs BEDOUIN_HEALER (2.0): Game=15, Calc=20 → needs cap at base * 1.5
            // vs Beast units: Game=10, Calc=20 → needs cap at base * 1.0 (Hunter tag already gives 2x, but needs cap)
            // vs Heavy: Game=10, Calc=15 → needs cap at base * 1.0
            // vs TREBUCHET: Game=10, Calc=2 → needs 5.0x modifier (bypasses SiegeDefense)
            var hunter = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_HUNTER);
            if (hunter != null)
            {
                hunter.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 5.0f); // 10/2 = 5.0 (bypasses SiegeDefense)
            }

            // DOG: Small Beast unit, needs special modifiers
            // vs BEDOUIN_EUNUCH (1.5): Game=10, Calc=15 → needs cap at base * 1.0
            // vs BEDOUIN_HEALER (2.0): Game=10, Calc=20 → needs cap at base * 1.0
            // vs Heavy: Game=10, Calc=5 → needs cap at base * 1.0 (already handled)
            // vs TREBUCHET: Game=10, Calc=4 → needs 2.5x modifier
            var dog = UnitDamageRegistry.GetUnitData(eChimps.CHIMP_TYPE_DOG);
            if (dog != null)
            {
                dog.AddSpecialModifier(eChimps.CHIMP_TYPE_TREBUCHET, 2.5f); // 10/4 = 2.5
            }
        }

        // ============================================
        // HELPER METHOD
        // ============================================
        private static void RegisterUnit(eChimps unit, int baseDamage, float armorValue, params string[] tags)
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
