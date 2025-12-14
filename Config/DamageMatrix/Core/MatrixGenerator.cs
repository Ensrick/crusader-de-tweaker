// Config/DamageMatrix/Core/MatrixGenerator.cs
using System;
using System.Text;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Base class for all matrix generators using Template Method pattern.
    /// Defines the common workflow for generating damage matrices.
    /// CSV Format: Rows = Defenders, Columns = Attackers
    /// </summary>
    /// <typeparam name="TAttacker">The attacker entity type (eChimps, projectile type, etc.)</typeparam>
    /// <typeparam name="TDefender">The defender entity type (eChimps)</typeparam>
    internal abstract class MatrixGenerator<TAttacker, TDefender>
    {
        /// <summary>
        /// The file path where this matrix will be saved.
        /// </summary>
        protected abstract string FilePath { get; }

        /// <summary>
        /// Template Method: Generate the complete damage matrix CSV.
        /// Returns true if generation was successful.
        /// </summary>
        public bool Generate()
        {
            try
            {
                // Step 1: Check if file already exists
                if (System.IO.File.Exists(FilePath))
                {
                    Plugin.Logger.LogInfo($"Matrix already exists: {FilePath}");
                    return false; // Don't overwrite existing config
                }

                Plugin.Logger.LogInfo($"Generating damage matrix: {FilePath}");

                // Step 2: Get the list of defenders and attackers
                var defenders = GetDefenders();
                var attackers = GetAttackers();

                if (defenders.Length == 0 || attackers.Length == 0)
                {
                    Plugin.Logger.LogWarning("No defenders or attackers found for matrix generation");
                    return false;
                }

                // Step 3: Build the damage matrix
                var matrix = BuildMatrix(defenders, attackers);

                // Step 4: Get header labels
                var defenderHeaders = GetDefenderHeaders(defenders);
                var attackerHeaders = GetAttackerHeaders(attackers);

                // Step 5: Write to CSV (rows = defenders, columns = attackers)
                WriteToFile(defenderHeaders, attackerHeaders, matrix);

                Plugin.Logger.LogInfo($"Successfully generated matrix with {defenders.Length} defenders x {attackers.Length} attackers");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to generate matrix {FilePath}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Get the list of attacker entities to include in the matrix.
        /// </summary>
        protected abstract TAttacker[] GetAttackers();

        /// <summary>
        /// Get the list of defender entities to include in the matrix.
        /// </summary>
        protected abstract TDefender[] GetDefenders();

        /// <summary>
        /// Get the damage value from the game API for a specific attacker-defender pair.
        /// Return -1 if the pair is invalid or damage cannot be determined.
        /// </summary>
        protected abstract int GetDamageValue(TAttacker attacker, TDefender defender);

        /// <summary>
        /// Get the header label for an attacker (e.g., unit name).
        /// Override to customize display names.
        /// </summary>
        protected virtual string GetAttackerHeader(TAttacker attacker)
        {
            return attacker.ToString();
        }

        /// <summary>
        /// Get the header label for a defender (e.g., unit name).
        /// Override to customize display names.
        /// </summary>
        protected virtual string GetDefenderHeader(TDefender defender)
        {
            return defender.ToString();
        }

        /// <summary>
        /// Validate a damage value before adding to matrix.
        /// Override to add custom validation rules.
        /// </summary>
        protected virtual bool ValidateDamageValue(int damage)
        {
            // By default, accept any value including -1 for invalid/N/A
            return true;
        }

        /// <summary>
        /// Get headers for all attackers.
        /// </summary>
        private string[] GetAttackerHeaders(TAttacker[] attackers)
        {
            var headers = new string[attackers.Length];
            for (int i = 0; i < attackers.Length; i++)
            {
                headers[i] = GetAttackerHeader(attackers[i]);
            }
            return headers;
        }

        /// <summary>
        /// Get headers for all defenders.
        /// </summary>
        private string[] GetDefenderHeaders(TDefender[] defenders)
        {
            var headers = new string[defenders.Length];
            for (int i = 0; i < defenders.Length; i++)
            {
                headers[i] = GetDefenderHeader(defenders[i]);
            }
            return headers;
        }

        /// <summary>
        /// Build the complete damage matrix by querying the API for each pair.
        /// Matrix format: [defender, attacker] to match CSV layout (rows = defenders, columns = attackers)
        /// </summary>
        private int[,] BuildMatrix(TDefender[] defenders, TAttacker[] attackers)
        {
            var matrix = new int[defenders.Length, attackers.Length];

            for (int row = 0; row < defenders.Length; row++)
            {
                for (int col = 0; col < attackers.Length; col++)
                {
                    try
                    {
                        int damage = GetDamageValue(attackers[col], defenders[row]);

                        if (!ValidateDamageValue(damage))
                        {
                            damage = -1; // Invalid damage
                        }

                        matrix[row, col] = damage;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogDebug($"Could not get damage for {attackers[col]} -> {defenders[row]}: {ex.Message}");
                        matrix[row, col] = -1; // Mark as invalid
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Write the matrix to a CSV file.
        /// </summary>
        private void WriteToFile(string[] defenderHeaders, string[] attackerHeaders, int[,] matrix)
        {
            CsvHelper.WriteMatrix(FilePath, defenderHeaders, attackerHeaders, matrix);
        }
    }
}