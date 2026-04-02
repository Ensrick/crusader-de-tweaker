// Config/DamageMatrix/Core/MatrixLoader.cs
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Base class for all matrix loaders using Template Method pattern.
    /// Defines the common workflow for loading and applying damage matrices.
    /// CSV Format: Rows = Defenders, Columns = Attackers
    /// </summary>
    /// <typeparam name="TAttacker">The attacker entity type (eChimps, projectile type, etc.)</typeparam>
    /// <typeparam name="TDefender">The defender entity type (eChimps)</typeparam>
    internal abstract class MatrixLoader<TAttacker, TDefender>
        where TAttacker : struct
        where TDefender : struct
    {
        /// <summary>
        /// The file path to load the matrix from.
        /// </summary>
        protected abstract string FilePath { get; }

        /// <summary>
        /// Template Method: Load the damage matrix and apply it to the game.
        /// Returns true if loading was successful.
        /// </summary>
        public bool Load()
        {
            if (!System.IO.File.Exists(FilePath))
            {
                Plugin.Logger.LogWarning($"Matrix file not found: {FilePath}");
                return false;
            }

            try
            {
                Plugin.Logger.LogInfo($"Loading damage matrix: {FilePath}");

                // Step 1: Read the CSV file (rows = defenders, columns = attackers)
                var (rowHeaders, columnHeaders, matrix) = CsvHelper.ReadMatrix(FilePath);

                // Step 2: Parse headers to entities
                var defenders = ParseDefenderHeaders(rowHeaders);
                var attackers = ParseAttackerHeaders(columnHeaders);

                // Step 3: Validate dimensions
                if (defenders.Length != matrix.GetLength(0) || attackers.Length != matrix.GetLength(1))
                {
                    Plugin.Logger.LogError("Matrix dimensions don't match parsed entities");
                    return false;
                }

                // Step 4: Apply each damage value to the game
                var (appliedCount, skippedCount) = MatrixApplicationLoop.ApplyMatrix(
                    attackers,
                    defenders,
                    matrix,
                    (attacker, defender, damage) => 
                        !attacker.HasValue || !defender.HasValue ||
                        ShouldSkipAttacker(attacker.Value) || ShouldSkipDefender(defender.Value) ||
                        !ValidateDamageValue(damage) ||
                        (GetOriginalDefaultValue(attacker.Value, defender.Value) >= 0 && damage == GetOriginalDefaultValue(attacker.Value, defender.Value)),
                    (attacker, defender, damage) => TryApplyMatrixCell(attacker, defender, damage)
                );

                Plugin.Logger.LogInfo($"Matrix loaded: Applied={appliedCount}, Skipped={skippedCount}");

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to load matrix {FilePath}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Attempt to apply a single matrix cell value.
        /// Returns true if applied successfully, false if skipped.
        /// 
        /// CRITICAL LOGIC (DO NOT CHANGE THIS):
        /// - CSV values are ONLY applied if they differ from ORIGINAL DEFAULT values
        /// - If CSV == original default, skip it (game default is already correct)
        /// - If CSV != original default, apply CSV (user explicitly modified this matchup)
        /// </summary>
        private bool TryApplyMatrixCell(TAttacker? attacker, TDefender? defender, int damage)
        {
            // Note: Null checks and skip checks are now done in Load() before calling this method
            // This method assumes valid, non-null, modifiable entities

            if (!ValidateDamageValue(damage))
                return false;

            // Get the ORIGINAL DEFAULT value (from game before any mods)
            // This is what the CSV file was generated from
            int originalDefault = GetOriginalDefaultValue(attacker.Value, defender.Value);
            
            if (originalDefault >= 0)
            {
                // Compare CSV value against ORIGINAL DEFAULT
                if (damage == originalDefault)
                {
                    // CSV matches original default - user hasn't modified this matchup
                    // Skip CSV, use game default
                    return false;
                }
                else
                {
                    // CSV differs from original default - user explicitly modified this matchup
                    // Apply CSV override (this overrides game default for this specific matchup)
                    Plugin.Logger.LogDebug($"CSV override: {attacker.Value} -> {defender.Value}: CSV={damage}, Original={originalDefault}");
                    return ApplyDamageValue(attacker.Value, defender.Value, damage);
                }
            }

            // No original default available - apply CSV value as-is
            return ApplyDamageValue(attacker.Value, defender.Value, damage);
        }

        /// <summary>
        /// Get the ORIGINAL DEFAULT damage value (from game before any mods).
        /// This is used to determine if CSV value was modified by user.
        /// Returns -1 if original default is not available.
        /// 
        /// CRITICAL: This MUST return the ORIGINAL game default (captured before any mods).
        /// This is used to compare against CSV to determine if the user explicitly modified a CSV value.
        /// 
        /// Override in subclasses to provide the appropriate lookup:
        /// - MeleeDamageMatrixLoader: Use CsvMatrixReader.GetOriginalMeleeDamage()
        /// - RangedDamageMatrixLoader: Use CsvMatrixReader.GetOriginalRangedDamage()
        /// - EunuchAoeDamageMatrixLoader: Use CsvMatrixReader.GetOriginalEunuchAoeDamage()
        /// 
        /// DO NOT:
        /// - Skip implementing this method (CSV comparison will fail)
        /// </summary>
        protected virtual int GetOriginalDefaultValue(TAttacker attacker, TDefender defender)
        {
            // Default: Can't get original default
            // Subclasses MUST override this to use CsvMatrixReader
            return -1;
        }



        /// <summary>
        /// Parse attacker headers from CSV into entity types.
        /// Returns nullable array - null entries indicate parsing failures.
        /// </summary>
        protected abstract Nullable<TAttacker>[] ParseAttackerHeaders(string[] headers);

        /// <summary>
        /// Parse defender headers from CSV into entity types.
        /// Returns nullable array - null entries indicate parsing failures.
        /// </summary>
        protected abstract Nullable<TDefender>[] ParseDefenderHeaders(string[] headers);

        /// <summary>
        /// Apply a damage value to the game via the API.
        /// Return true if successful, false if skipped/failed.
        /// </summary>
        protected abstract bool ApplyDamageValue(TAttacker attacker, TDefender defender, int damage);


        /// <summary>
        /// Validate a damage value before applying.
        /// Override to add custom validation rules.
        /// </summary>
        protected virtual bool ValidateDamageValue(int damage)
        {
            // By default, skip negative values (they indicate invalid/N/A in generated files)
            return damage >= 0;
        }

        /// <summary>
        /// Check if an entity should be skipped (e.g., non-modifiable units).
        /// </summary>
        protected virtual bool ShouldSkipAttacker(TAttacker attacker)
        {
            return false;
        }

        /// <summary>
        /// Check if an entity should be skipped (e.g., non-modifiable units).
        /// </summary>
        protected virtual bool ShouldSkipDefender(TDefender defender)
        {
            return false;
        }

        /// <summary>
        /// Helper to parse enum headers with error handling.
        /// </summary>
        protected Nullable<TEnum>[] ParseEnumHeaders<TEnum>(string[] headers) where TEnum : struct
        {
            var result = new Nullable<TEnum>[headers.Length];

            for (int i = 0; i < headers.Length; i++)
            {
                if (Enum.TryParse<TEnum>(headers[i], out var parsed))
                {
                    result[i] = parsed;
                }
                else
                {
                    Plugin.Logger.LogWarning($"Could not parse header '{headers[i]}' as {typeof(TEnum).Name}");
                    result[i] = null;
                }
            }

            return result;
        }
    }
}