// Config/Toml/Core/TomlFormatter.cs
//
// PURPOSE: TOML value formatting and syntax writing utilities.
//
// USAGE:
// - FormatValue(): Formats values for TOML output (strings get quotes, etc.)
// - WriteProperty(): Writes a TOML property with optional default value comment
// - WriteSection(): Writes a TOML section header
// - WriteComment(): Writes a TOML comment
//
// IMPORTANT FOR AI AGENTS:
// - This is a pure utility class - no business logic, just TOML formatting
// - Handles TOML syntax correctly (quoted strings, boolean values, etc.)
// - Used by ConfigGenerator to write TOML files
// - Extracted from ConfigHelpers to improve modularity
//
using System.Text;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Utilities for formatting values and writing TOML syntax.
    /// 
    /// Provides low-level TOML formatting operations:
    /// - Value formatting (quotes, boolean strings, etc.)
    /// - Property writing (with optional default value comments)
    /// - Section headers and comments
    /// 
    /// Used by ConfigGenerator to write TOML files.
    /// </summary>
    internal static class TomlFormatter
    {
        /// <summary>
        /// Format a value for TOML output.
        /// Strings get quotes, everything else is plain.
        /// </summary>
        public static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string)
                return $"\"{value}\"";

            if (value is bool b)
                return b ? "true" : "false";

            if (value is float f)
                return f.ToString("0.0"); // 1 decimal place (sufficient for armor multipliers)

            if (value is double d)
                return d.ToString("0.0"); // 1 decimal place

            return value.ToString();
        }

        /// <summary>
        /// Write a TOML property line with optional comment.
        /// </summary>
        public static void WriteProperty(StringBuilder sb, string name, object value, object defaultValue = null)
        {
            string formattedValue = FormatValue(value);

            if (defaultValue != null)
            {
                string formattedDefault = FormatValue(defaultValue);
                sb.AppendLine($"{name} = {formattedValue} # Default: {formattedDefault}");
            }
            else
            {
                sb.AppendLine($"{name} = {formattedValue}");
            }
        }

        /// <summary>
        /// Write a TOML section header.
        /// </summary>
        public static void WriteSection(StringBuilder sb, string sectionName)
        {
            sb.AppendLine($"[{sectionName}]");
        }

        /// <summary>
        /// Write a blank line for spacing.
        /// </summary>
        public static void WriteBlankLine(StringBuilder sb)
        {
            sb.AppendLine();
        }

        /// <summary>
        /// Write a comment line.
        /// </summary>
        public static void WriteComment(StringBuilder sb, string comment)
        {
            sb.AppendLine($"# {comment}");
        }

        /// <summary>
        /// Create a section divider comment for readability.
        /// </summary>
        public static void WriteSectionDivider(StringBuilder sb, string sectionTitle)
        {
            sb.AppendLine();
            sb.AppendLine($"# ========================================");
            sb.AppendLine($"# {sectionTitle}");
            sb.AppendLine($"# ========================================");
            sb.AppendLine();
        }
    }
}

