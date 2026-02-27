// Config/DamageMatrix/Core/MatrixPaths.cs
//
// PURPOSE: Centralizes file paths for damage matrix CSV files.
//
// CRITICAL - CSV FILE LOCATION:
// CSV files are in the GAME DIRECTORY, not AppData:
// - Path: {GameDir}\BepInEx\config\CrusaderDETweaker\DamageMatrices\
// - Example: C:\Program Files (x86)\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\config\CrusaderDETweaker\DamageMatrices\
// - Uses BepInEx.Paths.ConfigPath which resolves to game directory config folder
// - NOT in %APPDATA%\BepInEx\config\ (that's for BepInEx.cfg settings)
//
// USAGE:
// - All CSV file paths are under the DamageMatrices subfolder
//
// IMPORTANT FOR AI AGENTS:
// - All damage matrix file paths should use this class (don't hardcode paths)
// - Uses BepInEx Paths.ConfigPath for consistent config directory resolution
// - File paths are in GAME DIRECTORY: {GameDir}\BepInEx\config\CrusaderDETweaker\DamageMatrices\
//
using System.IO;
using BepInEx;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// File path management for damage matrix CSV files.
    ///
    /// All CSV files live in the DamageMatrices subfolder:
    ///   {GameDir}\BepInEx\config\CrusaderDETweaker\DamageMatrices\
    ///
    /// This uses BepInEx's Paths.ConfigPath which resolves to {GameDir}\BepInEx\config\CrusaderDETweaker\
    /// NOT %APPDATA%\BepInEx\config\ (that's for BepInEx.cfg settings only)
    /// </summary>
    internal static class MatrixPaths
    {
        /// <summary>
        /// Base config directory: {GameDir}\BepInEx\config\CrusaderDETweaker\
        /// </summary>
        private static string ConfigDir => Path.Combine(Paths.ConfigPath, "CrusaderDETweaker");

        /// <summary>
        /// Subfolder for all damage matrix CSV files: {ConfigDir}\DamageMatrices\
        /// </summary>
        internal static string MatrixDir => Path.Combine(ConfigDir, "DamageMatrices");

        /// <summary>
        /// Unit vs Unit melee damage matrix (full NxN).
        /// Format: Rows = Defenders, Columns = Attackers
        /// </summary>
        internal static string MeleeDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_MeleeDamage.csv");

        /// <summary>
        /// Ranged damage vs units.
        /// Format: Rows = Defenders, Columns = Projectile types (Arrows, Bolts, Slings, Javelins)
        /// </summary>
        internal static string RangedDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_RangedDamage.csv");

        /// <summary>
        /// Eunuch AOE damage vs units.
        /// Format: Rows = Defenders, Column = EunuchAOE
        /// </summary>
        internal static string EunuchAoeDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_EunuchAoeDamage.csv");

        /// <summary>
        /// Ballista damage Global Properties matrix.
        /// </summary>
        internal static string BallistaDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_BallistaDamage.csv");

        /// <summary>
        /// Per-unit fire damage (1 column: Fire, rows = unit types).
        /// </summary>
        internal static string UnitFireDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_UnitFireDamage.csv");

        /// <summary>
        /// Per-unit Bedouin heal amount (1 column: BedouinHeal, rows = unit types).
        /// </summary>
        internal static string BedouinHeal => Path.Combine(MatrixDir, "CrusaderDETweaker_BedouinHeal.csv");

        /// <summary>
        /// Per-building fire damage (1 column: Fire, rows = building types).
        /// </summary>
        internal static string BuildingFireDamage => Path.Combine(MatrixDir, "CrusaderDETweaker_BuildingFireDamage.csv");
    }
}