// Config/DamageMatrix/Core/CsvHelper.cs
//
// PURPOSE: Low-level CSV file I/O operations for damage matrix files.
//
// USAGE:
// - WriteMatrix(): Used by all matrix generators to write CSV files with headers
// - ReadMatrix(): Used by all matrix loaders to read CSV files
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just CSV parsing/writing
// - Handles CSV format: first row = column headers, first column = row headers
// - Handles quoted values and basic CSV escaping
// - Returns -1 or throws exceptions on errors (no silent failures)
//
using CrusaderDETweaker.Data;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    /// <summary>
    /// Utility class for reading and writing CSV files.
    /// 
    /// Provides low-level CSV I/O operations for damage matrix files.
    /// Handles CSV format with row and column headers, quoted values, and basic escaping.
    /// 
    /// Used by all matrix generators (to write CSV files) and loaders (to read CSV files).
    /// </summary>
    internal static class CsvHelper
    {
        /// <summary>
        /// Write a matrix to a CSV file with row and column headers.
        /// </summary>
        /// <param name="filePath">Full path to the CSV file</param>
        /// <param name="rowHeaders">Labels for each row (attackers)</param>
        /// <param name="columnHeaders">Labels for each column (defenders)</param>
        /// <param name="matrix">2D array of values [row, column]</param>
        public static void WriteMatrix(string filePath, string[] rowHeaders, string[] columnHeaders, int[,] matrix)
        {
            if (rowHeaders == null || columnHeaders == null || matrix == null)
                throw new ArgumentNullException("Headers and matrix cannot be null");

            if (rowHeaders.Length != matrix.GetLength(0))
                throw new ArgumentException("Row headers count must match matrix row count");

            if (columnHeaders.Length != matrix.GetLength(1))
                throw new ArgumentException("Column headers count must match matrix column count");

            try
            {
                // Ensure directory exists (e.g. BepInEx/config/CrusaderDETweaker/)
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var sb = new StringBuilder();

                // Write header comments explaining the CSV format
                WriteCsvHeaderComments(sb, filePath);

                // Pre-compute column widths for aligned output
                int rowHeaderWidth = rowHeaders.Max(h => h.Length);
                var colWidths = new int[columnHeaders.Length];
                for (int col = 0; col < columnHeaders.Length; col++)
                {
                    colWidths[col] = columnHeaders[col].Length;
                    for (int row = 0; row < rowHeaders.Length; row++)
                        colWidths[col] = Math.Max(colWidths[col], matrix[row, col].ToString().Length);
                }

                // Write header row (empty top-left cell, then column headers)
                sb.Append(new string(' ', rowHeaderWidth));
                sb.Append(", ");
                sb.AppendLine(string.Join(", ", columnHeaders.Select((h, i) => h.PadRight(colWidths[i]))));

                // Write data rows (row header + values)
                for (int row = 0; row < rowHeaders.Length; row++)
                {
                    sb.Append(rowHeaders[row].PadRight(rowHeaderWidth));
                    sb.Append(", ");

                    var rowValues = new List<string>();
                    for (int col = 0; col < columnHeaders.Length; col++)
                        rowValues.Add(matrix[row, col].ToString().PadRight(colWidths[col]));

                    sb.AppendLine(string.Join(", ", rowValues));
                }

                File.WriteAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to write CSV to {filePath}", ex);
            }
        }

        /// <summary>
        /// Read a matrix from a CSV file with headers.
        /// Returns: (rowHeaders, columnHeaders, matrix)
        /// </summary>
        public static (string[] rowHeaders, string[] columnHeaders, int[,] matrix) ReadMatrix(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV file not found: {filePath}");

            try
            {
                var lines = File.ReadAllLines(filePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                    .ToArray();

                if (lines.Length < 2)
                    throw new FormatException("CSV must have at least a header row and one data row");

                // Parse header row
                var headerParts = ParseCsvLine(lines[0]);
                var columnHeaders = headerParts.Skip(1).ToArray(); // Skip first empty cell

                // Parse data rows
                var rowHeaders = new List<string>();
                var matrixRows = new List<int[]>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = ParseCsvLine(lines[i]);

                    if (parts.Length < 2)
                        continue; // Skip invalid rows

                    rowHeaders.Add(parts[0]); // First cell is row header

                    var rowValues = parts.Skip(1)
                        .Select(v => ParseIntSafe(v, 0))
                        .ToArray();

                    matrixRows.Add(rowValues);
                }

                // Convert to 2D array
                int rows = matrixRows.Count;
                int cols = columnHeaders.Length;
                var matrix = new int[rows, cols];

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols && col < matrixRows[row].Length; col++)
                    {
                        matrix[row, col] = matrixRows[row][col];
                    }
                }

                return (rowHeaders.ToArray(), columnHeaders, matrix);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to read CSV from {filePath}", ex);
            }
        }

        /// <summary>
        /// Parse a CSV line, handling quoted values.
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        /// <summary>
        /// Safely parse an integer, returning defaultValue if parsing fails.
        /// </summary>
        private static int ParseIntSafe(string value, int defaultValue)
        {
            if (int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Read an existing CSV file and rewrite it with aligned column formatting.
        /// Preserves all values exactly — purely cosmetic reformatting.
        /// </summary>
        public static void ReformatMatrix(string filePath)
        {
            var (rowHeaders, columnHeaders, matrix) = ReadMatrix(filePath);
            WriteMatrix(filePath, rowHeaders, columnHeaders, matrix);
        }

        /// <summary>
        /// Write header comments explaining the CSV file format.
        /// </summary>
        private static void WriteCsvHeaderComments(StringBuilder sb, string filePath)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            
            sb.AppendLine("# ========================================");
            sb.AppendLine($"# Crusader DE Tweaker - {GetMatrixTypeName(fileName)}");
            sb.AppendLine("# ========================================");
            sb.AppendLine("#");
            sb.AppendLine("# This CSV file contains damage values in matrix format:");
            sb.AppendLine("#   - Rows (first column) = Defenders (units being attacked)");
            sb.AppendLine("#   - Columns (header row) = Attackers (units/projectiles doing damage)");
            sb.AppendLine("#   - Values = Damage amount (integer)");
            sb.AppendLine("#");
            sb.AppendLine("# HOW TO EDIT:");
            sb.AppendLine("#   1. Find the defender unit in the first column");
            sb.AppendLine("#   2. Find the attacker unit/projectile in the header row");
            sb.AppendLine("#   3. Change the value at the intersection to modify damage");
            sb.AppendLine("#   4. Values matching original game defaults are skipped (not applied)");
            sb.AppendLine("#   5. Only modified values (different from defaults) are applied to the game");
            sb.AppendLine("#");
            sb.AppendLine("# Note: This file can be edited in Excel, Google Sheets, or any text editor.");
            sb.AppendLine("# ========================================");
            sb.AppendLine();
        }

        /// <summary>
        /// Get a user-friendly name for the matrix type based on filename.
        /// </summary>
        private static string GetMatrixTypeName(string fileName)
        {
            if (fileName.Contains("MeleeDamage"))
                return "Melee Damage Matrix";
            if (fileName.Contains("RangedDamage"))
                return "Ranged Damage Matrix";
            if (fileName.Contains("EunuchAoeDamage"))
                return "Eunuch AOE Damage Matrix";
            return "Damage Matrix";
        }
    }
}
