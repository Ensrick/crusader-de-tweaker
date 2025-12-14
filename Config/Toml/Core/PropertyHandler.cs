// Config/Toml/Core/PropertyHandler.cs
using System;
using System.Text;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Base class for all property handlers using Template Method pattern.
    /// Defines the common workflow for generating and loading property values.
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
        /// Template Method: Generate this property for the given entity.
        /// Returns true if property was successfully written to config.
        /// </summary>
        public bool TryGenerate(TEntity entity, StringBuilder sb)
        {
            // Step 1: Check if this property applies to this entity
            if (!CanApplyTo(entity))
                return false;

            try
            {
                // Step 2: Get the value from the game API
                if (!TryGetFromAPI(entity, out TValue value))
                    return false;

                // Step 3: Validate the value
                if (!ValidateValue(value))
                    return false;

                // Step 4: Write to config file
                WriteToConfig(sb, value);
                return true;
            }
            catch (Exception ex)
            {
                // Step 5: Handle errors gracefully
                ConfigHelpers.ErrorLogging.LogGenerationException(Name, entity, ex);
                return false;
            }
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
                ConfigHelpers.ErrorLogging.LogValidationFailure(Name, entity, value);
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
                ConfigHelpers.ErrorLogging.LogPropertyLoadException(Name, entity, ex);
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
        /// Format the value for display in TOML
        /// Override if special formatting is needed
        /// </summary>
        internal virtual string FormatValue(TValue value)
        {
            // Strings need quotes, everything else is plain
            if (value is string)
                return $"\"{value}\"";
            return value.ToString();
        }

        // ============================================================
        // PRIVATE HELPER: Used by template method
        // ============================================================

        private void WriteToConfig(StringBuilder sb, TValue value)
        {
            string formattedValue = FormatValue(value);
            
            // For arrays or long values, put comment on separate line to avoid TOML parsing errors
            // TOML requires implicit keys to be on a single line
            if (IsLongValue(formattedValue))
            {
                sb.AppendLine($"{Name} = {formattedValue}");
                sb.AppendLine($"# Default: {formattedValue}");
            }
            else
            {
                sb.AppendLine($"{Name} = {formattedValue} # Default: {formattedValue}");
            }
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
}