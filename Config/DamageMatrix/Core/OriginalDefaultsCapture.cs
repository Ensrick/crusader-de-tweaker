// Config/DamageMatrix/Core/OriginalDefaultsCapture.cs
//
// PURPOSE: Orchestrates capturing all original game default damage values.
//
// USAGE:
// - CaptureAll(): Called by ConfigManager.Initialize() BEFORE any configs load
// - Delegates to specialized capture classes: MeleeDefaultsCapture, RangedDefaultsCapture, EunuchAoeDefaultsCapture
//
// IMPORTANT FOR AI AGENTS:
// - This MUST be called BEFORE TOML configs load (ensures we capture true game defaults)
// - Safe to call multiple times (only captures once, has internal flag)
// - Only captures modifiable units (skips UI placeholders, special units, etc.)
// - Used by CSV matrix loaders to compare CSV values against original defaults
//
using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDETweaker.Config.DamageMatrix.BedouinHeal;
using CrusaderDETweaker.Config.DamageMatrix.BuildingFireDamage;
using CrusaderDETweaker.Config.DamageMatrix.UnitFireDamage;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Orchestrates capturing all original game default damage values from the API.
    /// 
    /// This MUST be called BEFORE any mods are applied to ensure CSV comparison logic
    /// always compares against the true original game values.
    /// 
    /// Delegates to specialized capture classes for each damage type:
    /// - MeleeDefaultsCapture: Captures melee damage defaults
    /// - RangedDefaultsCapture: Captures ranged damage defaults
    /// - EunuchAoeDefaultsCapture: Captures AOE damage defaults
    /// </summary>
    internal static class OriginalDefaultsCapture
    {
        private static bool _isCaptured = false;
        private static eChimps[] _modifiableUnits;

        /// <summary>
        /// Capture all original game defaults (melee, ranged, Eunuch AOE).
        /// Safe to call multiple times - only captures once.
        /// </summary>
        public static void CaptureAll()
        {
            if (_isCaptured)
                return;

            _modifiableUnits = GetModifiableUnits();
            
            MeleeDefaultsCapture.Capture(_modifiableUnits);
            RangedDefaultsCapture.Capture(_modifiableUnits);
            EunuchAoeDefaultsCapture.Capture(_modifiableUnits);
            BallistaDefaultsCapture.Capture();
            UnitFireDefaultsCapture.Capture(_modifiableUnits);
            BedouinHealDefaultsCapture.Capture(_modifiableUnits);
            BuildingFireDefaultsCapture.Capture();

            _isCaptured = true;
        }

        private static eChimps[] GetModifiableUnits()
        {
            return Enum.GetValues(typeof(eChimps))
                .Cast<eChimps>()
                .Where(unit => !UnitCategories.IsNonModifiable(unit))
                .ToArray();
        }
    }
}

