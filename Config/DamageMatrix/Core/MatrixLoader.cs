// Config/DamageMatrix/Core/MatrixLoader.cs
using System;
using System.Collections.Generic;
using System.Linq;

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
                int appliedCount = 0;
                int skippedCount = 0;

                for (int row = 0; row < defenders.Length; row++)
                {
                    for (int col = 0; col < attackers.Length; col++)
                    {
                        if (TryApplyMatrixCell(attackers[col], defenders[row], matrix[row, col]))
                            appliedCount++;
                        else
                            skippedCount++;
                    }
                }

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
        /// Only applies if the value differs from the calculated value (based on TOML configs).
        /// This prevents CSV from overriding TOML changes unless the CSV value is explicitly different.
        /// </summary>
        private bool TryApplyMatrixCell(TAttacker? attacker, TDefender? defender, int damage)
        {
            if (!attacker.HasValue || !defender.HasValue)
                return false;

            if (!ValidateDamageValue(damage))
                return false;

            // Calculate what the damage should be based on TOML configs (registry)
            // This is the "expected" value based on current unit properties
            int calculatedValue = GetCalculatedDamageValue(attacker.Value, defender.Value);
            
            // Only apply CSV value if it differs from calculated value
            // This allows CSV to override TOML for specific matchups, but won't override
            // if CSV has the same value as what TOML would produce
            if (calculatedValue >= 0 && damage == calculatedValue)
                return false; // Same as calculated (TOML), skip to avoid unnecessary override

            // Apply the CSV value (it's different from calculated, so user wants this override)
            return ApplyDamageValue(attacker.Value, defender.Value, damage);
        }

        /// <summary>
        /// Get the calculated damage value based on current TOML configs (registry).
        /// Returns -1 if calculation is not possible or not applicable.
        /// Override in subclasses to provide the appropriate calculation.
        /// </summary>
        protected virtual int GetCalculatedDamageValue(TAttacker attacker, TDefender defender)
        {
            // Default: Can't calculate, so always apply CSV values
            // Subclasses should override this to use their damage calculator
            return -1;
        }

        /// <summary>
        /// Get the current damage value from the game API.
        /// Override in subclasses to provide the appropriate API call.
        /// Returns true if successful, false if not supported.
        /// </summary>
        protected virtual bool TryGetCurrentValue(TAttacker attacker, TDefender defender, out int currentValue)
        {
            currentValue = 0;
            return false; // Default: don't check current value (apply all)
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