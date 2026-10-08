// Config/Toml/Units/Properties/UnitRangeProperties.cs
//
// PURPOSE: Per-unit-type range settings for RANGED units (v2.8.0), on top of the SHCDE-SE 2.10 range APIs
//          (GameUnitManagerAPI; SE docs/guides/unit-api.md "Unit-Type Range Overrides"):
//   - AttackRange  (map tiles)  -> Set/Get/ResetUnitAttackRange: native target selection and, for supported
//                                  ballistic projectiles, projectile reach.
//   - EngageRange  (map tiles in the TOML; SE takes WORLD units, 8 per tile) -> Set/Get/ResetEngageRange:
//                                  distance at which the ranged-unit AI engages / disengages nearby enemies.
//   InteractRange is deliberately NOT exposed: it only changes the player UI path, and its value is squared
//   in a signed 32-bit int inside SE (overflow above 46340 world units).
//
// SE FACTS THIS RELIES ON (SE 2.10.4 source, API/GameUnitManagerAPI.cs + Detours/BulkUnitDetours.cs):
//   - Both overrides are stored as signed 16-bit values: > 32767 wraps. Values are clamped here, with a warning.
//   - Both overrides PERSIST across map loads and are NOT cleared by SE's OnUnloadMap ClearOverrides. The
//     TemplateBaseline restore for these cells is therefore SE's Reset* call (no override = game default),
//     so launch / host-sync apply / host-sync revert all end in the exact file state.
//   - GetDefaultEngageRange is observed lazily (0 until the unit's native update ran once) and approximate,
//     so at generation the "# Default:" comment usually reads "game default (auto)".
//   - GetUnitAttackRange maps ranged types to their projectile table; other types fall back to the archer
//     arrow table, which is meaningless for melee units. Hence the fixed unit lists below.
//
// IMPORTANT FOR AI AGENTS:
// - Keep the unit lists in step with SE: AttackRangeUnits = GetDefaultAttackRangeProjectileType's explicit
//   cases + the arrow users; EngageRangeUnits = the update functions BulkUnitDetours hooks for engage range.
// - Writes go through PropertyHandler.TryLoad -> TemplateBaseline (restore = Reset*), i.e. only through
//   ConfigLoader.ReapplyTemplateConfigs.
//
using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.Toml.Core;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal static class UnitRanges
    {
        /// <summary>World-coordinate units per map tile (SE docs: "One map tile equals eight world-coordinate units").</summary>
        internal const int WorldUnitsPerTile = 8;

        /// <summary>SE stores the overrides as signed 16-bit values.</summary>
        internal const int MaxStoredValue = short.MaxValue;

        /// <summary>
        /// Ranged unit types with a real attack-range table: the explicit cases of SE's
        /// GetDefaultAttackRangeProjectileType plus the arrow users (its fallback is the archer arrow table).
        /// </summary>
        internal static readonly HashSet<eChimps> AttackRangeUnits = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_ARAB_BOW, eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_XBOWMAN, eChimps.CHIMP_TYPE_ARAB_SLINGER, eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER, eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BALLISTA, eChimps.CHIMP_TYPE_ARAB_BALLISTA
        };

        /// <summary>Unit types whose native update function SE hooks for the engage range (BulkUnitDetours).</summary>
        internal static readonly HashSet<eChimps> EngageRangeUnits = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARCHER, eChimps.CHIMP_TYPE_ARAB_BOW, eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_XBOWMAN, eChimps.CHIMP_TYPE_ARAB_SLINGER, eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BALLISTA, eChimps.CHIMP_TYPE_ARAB_BALLISTA
        };

        /// <summary>Largest EngageRange in tiles whose world value still fits in SE's signed 16-bit store.</summary>
        internal const int MaxEngageTiles = MaxStoredValue / WorldUnitsPerTile;   // 4095

        /// <summary>Clamp a value to <paramref name="max"/>, warning with the property and unit.</summary>
        internal static int ClampWithWarning(string property, eChimps unit, int value, int max)
        {
            if (value <= max) return value;
            Plugin.Logger.LogWarning($"[{property}] {unit}: {value} is above the maximum {max} (the Script Extender stores it as a signed 16-bit value); using {max}.");
            return max;
        }

        /// <summary>One line per applied range override, with the Script Extender's read-back (a mismatch is a warning).</summary>
        internal static void LogApplied(string property, eChimps unit, string applied, bool readBackOk, string readBack)
        {
            if (readBackOk) Plugin.Logger.LogInfo($"[{property}] {unit}: {applied}; {readBack}.");
            else Plugin.Logger.LogWarning($"[{property}] {unit}: {applied}, but {readBack}. The override did not take.");
        }
    }

    /// <summary>AttackRange (map tiles) for ranged unit types. -1 = game default (no override).</summary>
    internal class AttackRangeProperty : PropertyHandler<eChimps, int>
    {
        public AttackRangeProperty() : base("AttackRange") { }

        internal override bool CanApplyTo(eChimps unit) => UnitRanges.AttackRangeUnits.Contains(unit);

        internal override bool ValidateValue(int value) => value > 0;

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            return ErrorHandlingHelper.TryGetValueWithResult(
                $"Get {Name}", unit.ToString(),
                () => Plugin.UnitApi.GetUnitAttackRange(unit),
                out value, defaultValue: 0) && value > 0;
        }

        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue) => TryGetFromAPI(unit, out defaultValue);

        protected override void SetToAPI(eChimps unit, int value)
        {
            int tiles = UnitRanges.ClampWithWarning(Name, unit, value, UnitRanges.MaxStoredValue);
            ErrorHandlingHelper.TryExecute($"Set {Name}", unit.ToString(), () =>
            {
                Plugin.UnitApi.SetUnitAttackRange(unit, tiles);
                // Read back through SE so the log proves what the game's range hooks will see.
                int now = Plugin.UnitApi.GetUnitAttackRange(unit);
                int native = SHCDESE.API.GameProjectileManagerAPI.Instance.GetAttackRangeTiles(
                    SHCDESE.API.GameUnitManagerAPI.GetDefaultAttackRangeProjectileType(unit));
                UnitRanges.LogApplied(Name, unit, $"{tiles} tiles (game default {native})", now == tiles, $"Script Extender reports {now}");
            });
        }

        /// <summary>The game state before this mod wrote the cell is "no override": restore = Reset.</summary>
        protected override Action CaptureBaselineRestore(eChimps unit) =>
            () => Plugin.UnitApi.ResetUnitAttackRange(unit);
    }

    /// <summary>
    /// EngageRange for ranged unit types, in map tiles in the TOML (x8 = SE world units). -1 = game default.
    /// </summary>
    internal class EngageRangeProperty : PropertyHandler<eChimps, int>
    {
        public EngageRangeProperty() : base("EngageRange") { }

        internal override bool CanApplyTo(eChimps unit) => UnitRanges.EngageRangeUnits.Contains(unit);

        internal override bool ValidateValue(int value) => value > 0;

        /// <summary>SE observes the native default lazily; before a unit update ran it is unknown.</summary>
        protected override string UnknownDefaultComment => "game default (auto)";

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            bool ok = ErrorHandlingHelper.TryGetValueWithResult(
                $"Get {Name}", unit.ToString(),
                () => Plugin.UnitApi.GetEngageRange(unit),
                out int world, defaultValue: 0);
            value = ok && world > 0 ? (int)Math.Round(world / (double)UnitRanges.WorldUnitsPerTile) : 0;
            return value > 0;
        }

        /// <summary>The lazily observed native default (0 = not observed yet -> "game default (auto)").</summary>
        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue)
        {
            bool ok = ErrorHandlingHelper.TryGetValueWithResult(
                $"Get default {Name}", unit.ToString(),
                () => Plugin.UnitApi.GetDefaultEngageRange(unit),
                out int world, defaultValue: 0);
            defaultValue = ok && world > 0 ? (int)Math.Round(world / (double)UnitRanges.WorldUnitsPerTile) : 0;
            return defaultValue > 0;
        }

        protected override void SetToAPI(eChimps unit, int value)
        {
            int tiles = UnitRanges.ClampWithWarning(Name, unit, value, UnitRanges.MaxEngageTiles);
            int world = tiles * UnitRanges.WorldUnitsPerTile;
            ErrorHandlingHelper.TryExecute($"Set {Name}", unit.ToString(), () =>
            {
                Plugin.UnitApi.SetEngageRange(unit, world);
                int now = Plugin.UnitApi.GetEngageRange(unit);
                int observed = Plugin.UnitApi.GetDefaultEngageRange(unit);
                string native = observed > 0 ? $"game default {observed / (double)UnitRanges.WorldUnitsPerTile:0.#}" : "game default not observed yet";
                UnitRanges.LogApplied(Name, unit, $"{tiles} tiles = {world} world units ({native})", now == world, $"Script Extender reports {now} world units");
            });
        }

        /// <summary>The game state before this mod wrote the cell is "no override": restore = Reset.</summary>
        protected override Action CaptureBaselineRestore(eChimps unit) =>
            () => Plugin.UnitApi.ResetUnitEngageRange(unit);
    }
}
