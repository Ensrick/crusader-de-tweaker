// Config/Toml/Core/PropertyHandler.cs
//
// PURPOSE: Base class for all TOML property handlers using Template Method pattern.
//
// USAGE:
// - Subclasses: HealthProperty, SpeedProperty, GoldCostProperty, etc.
// - TryGenerate(): Template method for generating property in config file
// - TryLoadFromObject(): Template method for loading property from TOML file
// - Subclasses implement: TryGetFromAPI(), SetToAPI(), ValidateValue(), CanApplyTo()
//
// IMPORTANT FOR AI AGENTS:
// - This is a base class - don't instantiate directly, use subclasses
// - Template Method pattern: base class defines workflow, subclasses implement steps
// - All property handlers follow the same pattern (read from API, validate, write to config)
// - To add a new property: create a subclass and register it in PropertyRegistry
//
using System;
using System.Collections.Generic;
using System.Text;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Base class for all property handlers using Template Method pattern.
    /// 
    /// Defines the common workflow for generating and loading property values:
    /// - Generation: Read from API → Validate → Write to config
    /// - Loading: Parse from TOML → Validate → Apply to API
    /// 
    /// Subclasses implement the specific steps (API calls, validation rules, etc.).
    /// </summary>
    /// <typeparam name="TEntity">The entity type (eChimps or eStructs)</typeparam>
    /// <typeparam name="TValue">The property value type (int, uint, float, string, etc.)</typeparam>
    internal abstract class PropertyHandler<TEntity, TValue>
    {
        /// <summary>
        /// The name of the property as it appears in the TOML file
        /// </summary>
        public string Name { get; }

        protected PropertyHandler(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Template Method: Generate this property for the given entity using game API value.
        /// Returns true if property was successfully written to config.
        /// </summary>
        public bool TryGenerate(TEntity entity, StringBuilder sb)
            => TryGenerateCore(entity, sb, null);

        /// <summary>
        /// Generate this property, using the user's existing value from a prior config file
        /// if present, otherwise falling back to the game API value.
        /// Used during migration to preserve user edits while adding/removing entries.
        /// </summary>
        public bool TryGenerateWithOverride(TEntity entity, StringBuilder sb, IDictionary<string, object> existingSection)
            => TryGenerateCore(entity, sb, existingSection);

        private bool TryGenerateCore(TEntity entity, StringBuilder sb, IDictionary<string, object> existingSection)
        {
            // CanApplyTo is the ONLY legitimate reason to exclude a property from the config.
            // All other failures (API errors, unexpected values) are written as placeholders/warnings
            // so the user always sees every property their entity can have.
            if (!CanApplyTo(entity))
                return false;

            // Numeric properties use the "-1 = use game default (no override)" sentinel as their
            // value; the real game value lives in the "# Default:" comment (refreshed each launch).
            // Non-numeric properties (WeaponType/ArmorType/RequiresHorse) keep writing the game value.
            bool numeric = NoOpSentinel.IsNumeric(typeof(TValue));

            try
            {
                // The game default for the "# Default:" comment. Read live from the API; generation
                // runs early in init (before configs are applied), so this is the true default.
                TValue gameDefault;
                bool hasDefault = TryGetOriginalValue(entity, out gameDefault);

                // --- Existing user value present (migration of a prior config file) ---
                if (existingSection != null && existingSection.TryGetValue(Name, out var rawValue))
                {
                    // Already the "-1" sentinel → keep it as a no-op.
                    if (numeric && NoOpSentinel.IsRawSentinel(rawValue))
                    {
                        WriteSentinelLine(sb, gameDefault, hasDefault);
                        return true;
                    }

                    if (TryParseValue(rawValue, out var userValue))
                    {
                        // A value that just equals the game default is migrated to the no-op
                        // sentinel, so unmodified stats stop overriding and read as "untouched".
                        if (numeric && hasDefault && AreValuesEqual(userValue, gameDefault))
                        {
                            WriteSentinelLine(sb, gameDefault, true);
                            return true;
                        }

                        // A genuine override (or any non-numeric value) — preserve it verbatim.
                        WriteToConfig(sb, userValue, hasDefault ? gameDefault : default(TValue), hasDefault);
                        return true;
                    }
                    // Unparseable existing value → fall through to fresh handling.
                }

                // --- Fresh entry ---
                if (numeric)
                {
                    // Default to the no-op sentinel; the game value lives in the "# Default:" comment.
                    // No default available → property is not applicable to this entity (e.g. a
                    // 0-health structure) → skip it, matching prior behaviour.
                    if (!hasDefault)
                        return false;
                    WriteSentinelLine(sb, gameDefault, true);
                    return true;
                }

                // --- Non-numeric (string/bool): keep the original "write the game value" behaviour ---
                TValue value;
                if (!TryGetFromAPI(entity, out value))
                {
                    // Intentional "skip" signal (property not applicable, STORED_NULL slot, etc.).
                    return false;
                }

                if (!ValidateValue(value))
                {
                    // API returned a value that fails current validation. Write it anyway with a note.
                    string formattedVal = FormatValue(value);
                    string defaultComment = hasDefault
                        ? $" # Default: {FormatValue(gameDefault)} (unexpected — check game API)"
                        : "  # Unexpected value — check game API";
                    sb.AppendLine($"{Name} = {formattedVal}{defaultComment}");
                    ErrorLogging.LogGenerationInvalidApiValue(Name, entity, value);
                    return true;
                }

                WriteToConfig(sb, value, hasDefault ? gameDefault : default(TValue), hasDefault);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogGenerationException(Name, entity, ex);
                sb.AppendLine($"# {Name} = ???  # Error during generation: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Writes a numeric property at the "-1 = use game default (no override)" sentinel, with the
        /// real game value shown in the "# Default:" comment.
        /// </summary>
        private void WriteSentinelLine(StringBuilder sb, TValue gameDefault, bool hasDefault)
        {
            if (hasDefault)
                sb.AppendLine($"{Name} = -1 # Default: {FormatValueForToml(gameDefault)}");
            else
                sb.AppendLine($"{Name} = -1");
        }

        /// <summary>
        /// Try to get the original/default value for this property.
        /// Override to provide original values from registry snapshots or API defaults.
        /// Returns true if original value is available, false otherwise.
        /// </summary>
        protected virtual bool TryGetOriginalValue(TEntity entity, out TValue defaultValue)
        {
            defaultValue = default(TValue);
            return false; // Default: no original value available
        }

        /// <summary>
        /// Template Method: Load this property value for the given entity.
        /// Returns true if property was successfully applied.
        /// </summary>
        public bool TryLoad(TEntity entity, TValue value)
        {
            // Step 1: Check if this property applies to this entity
            if (!CanApplyTo(entity))
                return false;

            // Step 2: Validate the value
            if (!ValidateValue(value))
            {
                ErrorLogging.LogValidationFailure(Name, entity, value);
                return false;
            }

            try
            {
                // Step 3: Set the value via the game API
                SetToAPI(entity, value);
                return true;
            }
            catch (Exception ex)
            {
                // Step 4: Handle errors
                ErrorLogging.LogPropertyLoadException(Name, entity, ex);
                return false;
            }
        }

        // ============================================================
        // ABSTRACT METHODS: Subclasses MUST implement these
        // ============================================================

        /// <summary>
        /// Try to get the property value from the game API
        /// Return true if successful, false if the property doesn't exist or can't be retrieved
        /// </summary>
        protected abstract bool TryGetFromAPI(TEntity entity, out TValue value);

        /// <summary>
        /// Set the property value via the game API
        /// </summary>
        protected abstract void SetToAPI(TEntity entity, TValue value);

        /// <summary>
        /// Try to get the property value from the game API (public wrapper for validation).
        /// </summary>
        internal bool TryGetValueFromAPI(TEntity entity, out TValue value)
        {
            return TryGetFromAPI(entity, out value);
        }

        // ============================================================
        // VIRTUAL METHODS: Subclasses CAN override these (optional)
        // ============================================================

        /// <summary>
        /// Determine if this property can be applied to the given entity
        /// Override to filter entities (e.g., GoldCost only for military units)
        /// </summary>
        internal virtual bool CanApplyTo(TEntity entity)
        {
            return true; // By default, applies to all entities
        }

        /// <summary>
        /// Validate the property value
        /// Override to add custom validation rules
        /// </summary>
        internal virtual bool ValidateValue(TValue value)
        {
            return true; // By default, all values are valid
        }

        /// <summary>
        /// Try to parse a value from a TOML object.
        /// Override for complex types (structs, arrays, etc.) that need custom parsing.
        /// Default implementation uses Convert.ChangeType for simple types.
        /// </summary>
        internal virtual bool TryParseValue(object tomlValue, out TValue result)
        {
            result = default(TValue);

            if (tomlValue == null)
                return false;

            try
            {
                // If value is already the correct type, just cast
                if (tomlValue is TValue alreadyTyped)
                {
                    result = alreadyTyped;
                    return true;
                }

                // Otherwise use Convert.ChangeType for numeric/string conversions
                result = (TValue)Convert.ChangeType(tomlValue, typeof(TValue));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Format the value for display in TOML
        /// Override if special formatting is needed
        /// </summary>
        internal virtual string FormatValue(TValue value)
        {
            // Strings need quotes
            if (value is string)
                return $"\"{value}\"";

            // Booleans need lowercase (TOML requires "true"/"false", not "True"/"False")
            if (value is bool b)
                return b ? "true" : "false";

            // Floats/doubles - limit to 3 decimal places (sufficient given Round5 rounding)
            if (value is float f)
                return f.ToString("0.###");

            if (value is double d)
                return d.ToString("0.###");

            // Everything else uses ToString()
            return value.ToString();
        }

        // ============================================================
        // PRIVATE HELPER: Used by template method
        // ============================================================

        private void WriteToConfig(StringBuilder sb, TValue value, TValue defaultValue, bool hasDefault)
        {
            string formattedValue = FormatValueForToml(value);
            
            // For arrays or long values, put comment on separate line to avoid TOML parsing errors
            // TOML requires implicit keys to be on a single line
            if (IsLongValue(formattedValue))
            {
                if (hasDefault)
                {
                    string formattedDefault = FormatValueForToml(defaultValue);
                    sb.AppendLine($"{Name} = {formattedValue}");
                    sb.AppendLine($"# Default: {formattedDefault}");
                }
                else
                {
                    sb.AppendLine($"{Name} = {formattedValue}");
                }
            }
            else
            {
                // Add default value comment if available
                if (hasDefault)
                {
                    string formattedDefault = FormatValueForToml(defaultValue);
                    sb.AppendLine($"{Name} = {formattedValue} # Default: {formattedDefault}");
                }
                else
                {
                    sb.AppendLine($"{Name} = {formattedValue}");
                }
            }
        }

        /// <summary>
        /// Format value for TOML output.
        /// Uses the overridable FormatValue() method to allow custom formatting.
        /// </summary>
        private string FormatValueForToml(TValue value)
        {
            return FormatValue(value);
        }

        /// <summary>
        /// Compare two values for equality.
        /// </summary>
        private bool AreValuesEqual(TValue value1, TValue value2)
        {
            if (value1 == null && value2 == null)
                return true;
            if (value1 == null || value2 == null)
                return false;
            // Float/double: tolerate the small gap between the stored display precision and the
            // freshly-read API value, so a value that matches the default within rounding still
            // migrates to the "-1 = use default" sentinel. The tolerance is far tighter than any
            // real override step, so genuine overrides are never swallowed.
            if (value1 is float f1 && value2 is float f2)
                return Math.Abs(f1 - f2) < 0.001f;
            if (value1 is double d1 && value2 is double d2)
                return Math.Abs(d1 - d2) < 0.001;
            return value1.Equals(value2);
        }

        /// <summary>
        /// Check if a formatted value is too long to put on the same line as a comment.
        /// Arrays and long strings should be on separate lines.
        /// </summary>
        private bool IsLongValue(string formattedValue)
        {
            // Arrays start with '[' and can be long
            if (formattedValue.StartsWith("["))
                return true;
            
            // Strings longer than 80 characters should be on separate line
            if (formattedValue.Length > 80)
                return true;
            
            return false;
        }
    }

    /// <summary>
    /// The universal "-1 = use the game default (no override)" sentinel for numeric properties.
    /// TOML has no null type, so -1 is the visible marker. It is matched on the RAW parsed value
    /// (Tomlyn yields integers as long, floats as double) BEFORE conversion to the handler's value
    /// type, so it works even for uint/ushort handlers where -1 cannot be represented.
    /// </summary>
    internal static class NoOpSentinel
    {
        public static bool IsNumeric(System.Type t) =>
            t == typeof(int)   || t == typeof(uint)  || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(long)  || t == typeof(ulong) || t == typeof(byte)  || t == typeof(sbyte)  ||
            t == typeof(float) || t == typeof(double);

        public static bool IsRawSentinel(object raw)
        {
            switch (raw)
            {
                case long l:   return l == -1L;
                case int i:    return i == -1;
                case double d: return d == -1.0;
                case float f:  return f == -1f;
                default:       return false;
            }
        }
    }
}