// Config/DamageMatrix/Core/MatrixApplicationLoop.cs
//
// PURPOSE: Generic loop for applying matrix values to the game.
//
// USAGE:
// - ApplyMatrix(): Called by MatrixLoader.Load() to iterate through matrix and apply values
// - shouldSkip delegate: Determines if a matrix cell should be skipped (null, invalid, matches default, etc.)
// - tryApply delegate: Attempts to apply a matrix cell value to the game API
//
// IMPORTANT FOR AI AGENTS:
// - This is a helper class extracted from MatrixLoader to reduce method complexity
// - Generic implementation works for all matrix types (melee, ranged, AOE)
// - Returns counts of applied/skipped values for logging
// - All skip logic is in the shouldSkip delegate (null checks, validation, default comparison)
//
using System;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Helper class to apply matrix values to the game.
    /// 
    /// Extracted from MatrixLoader to reduce method size and improve readability.
    /// 
    /// Provides a generic loop that works for all matrix types (melee, ranged, AOE).
    /// The actual skip/apply logic is provided via delegates, allowing MatrixLoader
    /// to customize behavior while sharing the common iteration pattern.
    /// </summary>
    internal static class MatrixApplicationLoop
    {
        /// <summary>
        /// Apply matrix values to the game, tracking applied and skipped counts.
        /// </summary>
        public static (int applied, int skipped) ApplyMatrix<TAttacker, TDefender>(
            TAttacker?[] attackers,
            TDefender?[] defenders,
            int[,] matrix,
            Func<TAttacker?, TDefender?, int, bool> shouldSkip,
            Func<TAttacker, TDefender, int, bool> tryApply)
            where TAttacker : struct
            where TDefender : struct
        {
            int appliedCount = 0;
            int skippedCount = 0;

            for (int row = 0; row < defenders.Length; row++)
            {
                for (int col = 0; col < attackers.Length; col++)
                {
                    if (shouldSkip(attackers[col], defenders[row], matrix[row, col]))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (tryApply(attackers[col].Value, defenders[row].Value, matrix[row, col]))
                        appliedCount++;
                    else
                        skippedCount++;
                }
            }

            return (appliedCount, skippedCount);
        }
    }
}

