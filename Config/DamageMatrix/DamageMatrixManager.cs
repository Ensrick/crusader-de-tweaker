// Config/DamageMatrix/DamageMatrixManager.cs
using CrusaderDETweaker.Config.DamageMatrix.BedouinHeal;
using CrusaderDETweaker.Config.DamageMatrix.Ballista;
using CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Config.DamageMatrix.EunuchAoe;
using CrusaderDETweaker.Config.DamageMatrix.Melee;
using CrusaderDETweaker.Config.DamageMatrix.Ranged;
using CrusaderDETweaker.Config.DamageMatrix.UnitFireDamage;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Config.DamageMatrix
{
    /// <summary>
    /// Top-level manager for all damage matrix operations.
    /// Orchestrates generation and loading of all matrix types.
    /// </summary>
    internal static class DamageMatrixManager
    {
        /// <summary>
        /// Initialize the damage matrix system.
        /// 1. Generate default CSV files if they don't exist
        /// 2. Load CSV files and apply to game
        /// </summary>
        public static void Initialize()
        {
            Plugin.Logger.LogInfo("Initializing damage matrix system...");

            // Step 1: Generate default matrices if they don't exist
            GenerateDefaults();

            // Step 2: Load and apply matrices
            LoadAll();

            Plugin.Logger.LogInfo("Damage matrix system initialized.");
        }

        /// <summary>
        /// Generate all default damage matrix CSV files.
        /// Only creates files that don't already exist.
        /// </summary>
        public static void GenerateDefaults()
        {
            Plugin.Logger.LogInfo("Generating default damage matrices...");

            // Generate ranged damage matrix
            var rangedGenerator = new RangedDamageMatrixGenerator();
            rangedGenerator.Generate();

            // Generate melee damage matrix
            var meleeGenerator = new MeleeDamageMatrixGenerator();
            meleeGenerator.Generate();

            // Generate Eunuch AOE damage matrix
            var eunuchAoeGenerator = new EunuchAoeDamageMatrixGenerator();
            eunuchAoeGenerator.Generate();

            // Generate Ballista damage matrix
            var ballistaGenerator = new BallistaDamageMatrixGenerator();
            ballistaGenerator.Generate();

            // Generate unit fire damage matrix
            var unitFireGenerator = new UnitFireDamageMatrixGenerator();
            unitFireGenerator.Generate();

            // Generate Bedouin heal matrix
            var bedouinHealGenerator = new BedouinHealMatrixGenerator();
            bedouinHealGenerator.Generate();

            // Generate building fire damage matrix
            var buildingFireGenerator = new BuildingFireDamageMatrixGenerator();
            buildingFireGenerator.Generate();

            Plugin.Logger.LogInfo("Default damage matrices generated.");
        }

        /// <summary>
        /// Load all damage matrices from CSV files and apply to game.
        /// CSV values override game defaults for specific matchups.
        /// </summary>
        public static void LoadAll()
        {
            Plugin.Logger.LogInfo("Loading damage matrices...");

            var rangedLoader = new RangedDamageMatrixLoader();
            rangedLoader.Load();

            var meleeLoader = new MeleeDamageMatrixLoader();
            meleeLoader.Load();

            // Load Eunuch AOE damage matrix
            var eunuchAoeLoader = new EunuchAoeDamageMatrixLoader();
            eunuchAoeLoader.Load();

            // Load Ballista damage matrix
            var ballistaLoader = new BallistaDamageMatrixLoader();
            ballistaLoader.Load();

            // Load unit fire damage matrix
            var unitFireLoader = new UnitFireDamageMatrixLoader();
            unitFireLoader.Load();

            // Load Bedouin heal matrix
            var bedouinHealLoader = new BedouinHealMatrixLoader();
            bedouinHealLoader.Load();

            // Load building fire damage matrix
            var buildingFireLoader = new BuildingFireDamageMatrixLoader();
            buildingFireLoader.Load();

            Plugin.Logger.LogInfo("Damage matrices loaded.");
        }

        /// <summary>
        /// Generate only ranged damage matrix.
        /// </summary>
        public static void GenerateRangedMatrix()
        {
            var generator = new RangedDamageMatrixGenerator();
            generator.Generate();
        }

        /// <summary>
        /// Generate only melee damage matrix.
        /// </summary>
        public static void GenerateMeleeMatrix()
        {
            var generator = new MeleeDamageMatrixGenerator();
            generator.Generate();
        }

        /// <summary>
        /// Load only ranged damage matrix.
        /// </summary>
        public static void LoadRangedMatrix()
        {
            var loader = new RangedDamageMatrixLoader();
            loader.Load();
        }

        /// <summary>
        /// Load only melee damage matrix.
        /// </summary>
        public static void LoadMeleeMatrix()
        {
            var loader = new MeleeDamageMatrixLoader();
            loader.Load();
        }
    }
}