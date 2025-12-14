// Config/DamageMatrix/Core/MatrixPaths.cs
using System.IO;
using BepInEx;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// File path management for damage matrix CSV files.
    /// </summary>
    internal static class MatrixPaths
    {
        private static string ConfigDir => Paths.ConfigPath;

        /// <summary>
        /// Unit vs Unit melee damage matrix (full NxN).
        /// Format: Rows = Defenders, Columns = Attackers
        /// </summary>
        internal static string MeleeDamage => Path.Combine(ConfigDir, "CrusaderDETweaker_MeleeDamage.csv");

        /// <summary>
        /// Ranged damage vs units.
        /// Format: Rows = Defenders, Columns = Projectile types (Arrows, Bolts, Slings, Javelins)
        /// </summary>
        internal static string RangedDamage => Path.Combine(ConfigDir, "CrusaderDETweaker_RangedDamage.csv");

        /// <summary>
        /// Eunuch AOE damage vs units.
        /// Format: Rows = Defenders, Column = EunuchAOE
        /// </summary>
        internal static string EunuchAoeDamage => Path.Combine(ConfigDir, "CrusaderDETweaker_EunuchAoeDamage.csv");
    }
}