// Config/Toml/Units/Properties/UnitRangeProperties.cs
//
// PURPOSE: Per-unit-type range settings for RANGED units (v2.8.0; engage rewritten in 2.9.1, GitLab #7 / GitHub #4):
//   - AttackRange  (map tiles)  -> SE Set/Get/ResetUnitAttackRange (GameUnitManagerAPI): native target acquisition
//                                  (RVA 0x18E9A0) and fire check (0x19B630), both hooked by SE, and projectile reach
//                                  for supported ballistic projectiles. With EngageRange -1 it also moves the idle
//                                  engage distance in proportion (EngageDistancePatch), so a longer AttackRange makes
//                                  idle units start shooting farther away.
//   - EngageRange  (map tiles)  -> EngageDistancePatch: the distance at which idle ranged units notice an enemy,
//                                  written into the game's own constants. NOT SE's SetEngageRange: that hook misses
//                                  the idle state (units kept waking at 50 tiles) and crashes the Crossbowman and
//                                  Bedouin Heavy Camel (SE #195, SE 2.10.0 - 2.14.0). See EngageDistancePatch.cs.
//   InteractRange is deliberately NOT exposed: it only changes the player UI path, and its value is squared
//   in a signed 32-bit int inside SE (overflow above 46340 world units).
//
// SE FACTS THIS RELIES ON (SE 2.10.4 - 2.14.0 source, API/GameUnitManagerAPI.cs + Detours/BulkUnitDetours.cs):
//   - The AttackRange override is stored as a signed 16-bit value: > 32767 wraps. Values are clamped here, with a warning.
//   - It PERSISTS across map loads and is NOT cleared by SE's OnUnloadMap ClearOverrides. The TemplateBaseline
//     restore is therefore SE's Reset call (no override = game default), so launch / host-sync apply / host-sync
//     revert all end in the exact file state. The engage constants are restored the same way (ClearOverrides).
//   - GetUnitAttackRange maps ranged types to their projectile table; other types fall back to the archer
//     arrow table, which is meaningless for melee units. Hence the fixed unit list below.
//
// IMPORTANT FOR AI AGENTS:
// - Keep AttackRangeUnits in step with SE: GetDefaultAttackRangeProjectileType's explicit cases + the arrow users.
//   EngageRange applies to the subset whose update function has engage constants (EngageDistancePatch.Supports).
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

        /// <summary>Largest EngageRange in tiles (the game caps the nearest-enemy distance at 32000 world units).</summary>
        internal const int MaxEngageTiles = EngageDistancePatch.MaxWorld / WorldUnitsPerTile;   // 4000

        /// <summary>The game's AttackRange of a ranged unit type, in tiles (its projectile table entry).</summary>
        internal static int GameAttackTiles(eChimps unit) =>
            SHCDESE.API.GameProjectileManagerAPI.Instance.GetAttackRangeTiles(SHCDESE.API.GameUnitManagerAPI.GetDefaultAttackRangeProjectileType(unit));

        /// <summary>
        /// AttackRange through SE (target acquisition and fire checks, both hooked by SE) plus the idle engage
        /// distance when EngageRange is -1 (EngageDistancePatch). Logs the SE read-back.
        /// </summary>
        internal static void ApplyAttackRange(string property, eChimps unit, int value)
        {
            int tiles = ClampWithWarning(property, unit, value, MaxStoredValue);
            Plugin.UnitApi.SetUnitAttackRange(unit, tiles);
            int now = Plugin.UnitApi.GetUnitAttackRange(unit);
            int native = GameAttackTiles(unit);
            LogApplied(property, unit, $"{tiles} tiles (game default {native})", now == tiles, $"Script Extender reports {now}");
            EngageDistancePatch.SetAttackTiles(unit, tiles, native);
        }

        internal static void ResetAttackRange(eChimps unit)
        {
            Plugin.UnitApi.ResetUnitAttackRange(unit);
            EngageDistancePatch.ClearAttack(unit);
        }

        /// <summary>Clamp a value to <paramref name="max"/>, warning with the property and unit.</summary>
        internal static int ClampWithWarning(string property, eChimps unit, int value, int max)
        {
            if (value <= max) return value;
            Plugin.Logger.LogWarning($"[{property}] {unit}: {value} is above the maximum {max}; using {max}.");
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
            ErrorHandlingHelper.TryExecute($"Set {Name}", unit.ToString(), () => UnitRanges.ApplyAttackRange(Name, unit, value));
        }

        /// <summary>The game state before this mod wrote the cell is "no override": restore = Reset.</summary>
        protected override Action CaptureBaselineRestore(eChimps unit) =>
            () => UnitRanges.ResetAttackRange(unit);
    }

    /// <summary>
    /// EngageRange for ranged unit types, in map tiles: how far away an idle unit notices an enemy. -1 = game
    /// default (or, when AttackRange is set, the game value scaled with it). Applied by EngageDistancePatch.
    /// </summary>
    internal class EngageRangeProperty : PropertyHandler<eChimps, int>
    {
        public EngageRangeProperty() : base("EngageRange") { }

        internal override bool CanApplyTo(eChimps unit) => EngageDistancePatch.Supports(unit);

        internal override bool ValidateValue(int value) => value > 0;

        protected override bool TryGetFromAPI(eChimps unit, out int value)
        {
            value = EngageDistancePatch.TryGetCurrentWorld(unit, out int world) ? (int)Math.Round(world / (double)UnitRanges.WorldUnitsPerTile) : 0;
            return value > 0;
        }

        /// <summary>The game's own engage distance, read from its code (e.g. Archer 50 tiles).</summary>
        protected override bool TryGetOriginalValue(eChimps unit, out int defaultValue) =>
            EngageDistancePatch.TryGetGameTiles(unit, out defaultValue);

        protected override void SetToAPI(eChimps unit, int value)
        {
            int tiles = UnitRanges.ClampWithWarning(Name, unit, value, UnitRanges.MaxEngageTiles);
            ErrorHandlingHelper.TryExecute($"Set {Name}", unit.ToString(), () => EngageDistancePatch.SetEngageTiles(unit, tiles));
        }

        /// <summary>The game state before this mod wrote the cell is "no EngageRange".</summary>
        protected override Action CaptureBaselineRestore(eChimps unit) =>
            () => EngageDistancePatch.ClearEngage(unit);
    }
}
