// Config/DamageMatrix/Core/CsvMatrixReader.cs
//
// PURPOSE: Facade for accessing original game default damage values.
//
// USAGE:
// - CaptureOriginalDefaults(): Called by ConfigManager to capture all defaults
// - GetOriginalMeleeDamage(): Used by MeleeDamageMatrixLoader for CSV comparison
// - GetOriginalRangedDamage(): Used by RangedDamageMatrixLoader for CSV comparison
// - GetOriginalEunuchAoeDamage(): Used by EunuchAoeDamageMatrixLoader for CSV comparison
//
// IMPORTANT FOR AI AGENTS:
// - This is a FACADE - it delegates to specialized classes
// - First tries captured defaults (from API), then falls back to CSV file
// - Returns -1 if value not found (indicates should skip or use game default)
// - Used by MatrixLoader to compare CSV values against original defaults
//
using CrusaderDETweaker.Config.DamageMatrix.Ballista;
using CrusaderDETweaker.Config.DamageMatrix.BedouinHeal;
using CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage;
using CrusaderDETweaker.Config.DamageMatrix.UnitFireDamage;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Facade for accessing original game default damage values.
    /// 
    /// Provides a unified interface for getting original defaults:
    /// 1. First tries captured defaults (from API)
    /// 2. Falls back to CSV file if capture failed
    /// 3. Returns -1 if not found
    /// 
    /// Used by matrix loaders to compare CSV values against original defaults.
    /// </summary>
    internal static class CsvMatrixReader
    {
        /// <summary>
        /// Capture original game defaults directly from the API BEFORE any mods are applied.
        /// </summary>
        public static void CaptureOriginalDefaults()
        {
            OriginalDefaultsCapture.CaptureAll();
        }

        /// <summary>
        /// Get the original game default melee damage.
        /// First tries captured defaults, then falls back to CSV file.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalMeleeDamage(eChimps attacker, eChimps defender)
        {
            int damage = MeleeDefaultsCapture.Get(attacker, defender);
            return damage >= 0 ? damage : CsvMatrixFallback.GetMeleeDamage(attacker, defender);
        }

        /// <summary>
        /// Get the original game default ranged damage.
        /// First tries captured defaults, then falls back to CSV file.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalRangedDamage(ProjectileType projectile, eChimps defender)
        {
            int damage = RangedDefaultsCapture.Get(projectile, defender);
            return damage >= 0 ? damage : CsvMatrixFallback.GetRangedDamage(projectile, defender);
        }

        /// <summary>
        /// Get the original game default Eunuch AOE damage.
        /// First tries captured defaults, then falls back to CSV file.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalEunuchAoeDamage(eChimps defender)
        {
            int damage = EunuchAoeDefaultsCapture.Get(defender);
            return damage >= 0 ? damage : CsvMatrixFallback.GetEunuchAoeDamage(defender);
        }

        /// <summary>
        /// Get the original ballista damage defaults.
        /// </summary>
        public static int GetOriginalBallistaDamage(BallistaDamageTarget target)
        {
            int damage = BallistaDefaultsCapture.Get(target);
            return damage >= 0 ? damage : CsvMatrixFallback.GetBallistaDamage(target);
        }

        /// <summary>
        /// Get the original unit fire damage default for a unit type.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalUnitFireDamage(eChimps unit)
        {
            return UnitFireDefaultsCapture.Get(unit);
        }

        /// <summary>
        /// Get the original Bedouin heal default for a unit type.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalBedouinHeal(eChimps unit)
        {
            return BedouinHealDefaultsCapture.Get(unit);
        }

        /// <summary>
        /// Get the original building fire damage default for a building type.
        /// Returns -1 if not found.
        /// </summary>
        public static int GetOriginalBuildingFireDamage(eStructs building)
        {
            return BuildingFireDefaultsCapture.Get(building);
        }
    }
}


