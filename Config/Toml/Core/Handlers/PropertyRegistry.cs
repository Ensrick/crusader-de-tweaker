// Config/Toml/Core/PropertyRegistry.cs
//
// PURPOSE: Registry pattern for managing property handlers (extensible TOML system).
//
// USAGE:
// - Register(): Registers a property handler (called by UnitPropertyRegistry/StructurePropertyRegistry)
// - GetByName(): Looks up handler by property name (used during TOML loading)
// - GetAll(): Gets all handlers (used during config generation)
//
// IMPORTANT FOR AI AGENTS:
// - This implements the Registry pattern - allows extensible property system
// - To add a new property: create PropertyHandler subclass and register it
// - Used by both UnitPropertyRegistry and StructurePropertyRegistry
// - Type-safe via generics (works for both eChimps and eStructs)
// - Property handlers are looked up by name during TOML parsing
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Registry pattern implementation for managing property handlers.
    /// 
    /// This class maintains a collection of property handlers for a specific entity type
    /// (units or structures). Property handlers define how to read, write, and validate
    /// individual properties (e.g., Health, Speed, Cost).
    /// 
    /// The registry provides:
    /// - Registration of property handlers
    /// - Lookup by property name
    /// - Filtering by entity applicability
    /// - Type-safe property handling via generics
    /// 
    /// This pattern allows the TOML config system to be extensible: new properties
    /// can be added by simply registering a new handler, without modifying core loading logic.
    /// 
    /// Example usage:
    /// - UnitPropertyRegistry: Manages handlers for unit properties
    /// - StructurePropertyRegistry: Manages handlers for structure properties
    /// </summary>
    internal class PropertyRegistry<TEntity>
    {
        private readonly Dictionary<string, IPropertyHandler<TEntity>> _handlers;

        public PropertyRegistry()
        {
            _handlers = new Dictionary<string, IPropertyHandler<TEntity>>();
        }

        /// <summary>
        /// Register a property handler.
        /// </summary>
        public void Register<TValue>(PropertyHandler<TEntity, TValue> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(handler.Name))
            {
                Plugin.Logger.LogWarning($"Property handler '{handler.Name}' is already registered. Overwriting.");
            }

            _handlers[handler.Name] = new PropertyHandlerWrapper<TEntity, TValue>(handler);
        }

        /// <summary>
        /// Get all registered property handlers.
        /// </summary>
        public IEnumerable<IPropertyHandler<TEntity>> GetAll()
        {
            return _handlers.Values;
        }

        /// <summary>
        /// Get all property handlers that can apply to the given entity.
        /// </summary>
        public IEnumerable<IPropertyHandler<TEntity>> GetApplicable(TEntity entity)
        {
            return _handlers.Values.Where(h => h.CanApplyTo(entity));
        }

        /// <summary>
        /// Get a specific property handler by name.
        /// Returns null if not found.
        /// </summary>
        public IPropertyHandler<TEntity> GetByName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return null;

            _handlers.TryGetValue(propertyName, out var handler);
            return handler;
        }

        /// <summary>
        /// Check if a property handler is registered.
        /// </summary>
        public bool Contains(string propertyName)
        {
            return _handlers.ContainsKey(propertyName);
        }

        /// <summary>
        /// Get the count of registered handlers.
        /// </summary>
        public int Count => _handlers.Count;
    }

    /// <summary>
    /// Non-generic interface for property handlers to allow storage in registry.
    /// </summary>
    internal interface IPropertyHandler<TEntity>
    {
        string Name { get; }
        bool CanApplyTo(TEntity entity);
        bool TryGenerate(TEntity entity, System.Text.StringBuilder sb);
        bool TryGenerateWithOverride(TEntity entity, System.Text.StringBuilder sb, System.Collections.Generic.IDictionary<string, object> existingSection);
        bool TryLoadFromObject(TEntity entity, object value);
        /// <summary>
        /// Try to get the current property value from the API.
        /// Used for runtime validation to check if configured values match actual API values.
        /// </summary>
        bool TryGetValue(TEntity entity, out object value);
    }

    /// <summary>
    /// Wrapper to convert typed PropertyHandler into non-generic interface.
    /// </summary>
    internal class PropertyHandlerWrapper<TEntity, TValue> : IPropertyHandler<TEntity>
    {
        private readonly PropertyHandler<TEntity, TValue> _handler;

        public PropertyHandlerWrapper(PropertyHandler<TEntity, TValue> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public string Name => _handler.Name;

        public bool CanApplyTo(TEntity entity)
        {
            return _handler.CanApplyTo(entity);
        }

        public bool TryGenerate(TEntity entity, System.Text.StringBuilder sb)
        {
            return _handler.TryGenerate(entity, sb);
        }

        public bool TryGenerateWithOverride(TEntity entity, System.Text.StringBuilder sb, System.Collections.Generic.IDictionary<string, object> existingSection)
        {
            return _handler.TryGenerateWithOverride(entity, sb, existingSection);
        }

        public bool TryLoadFromObject(TEntity entity, object value)
        {
            if (value == null)
                return false;

            // "-1 = use the game default (no override)": leave the game's value untouched.
            // Checked on the raw value before parsing, so it works for uint/ushort handlers too
            // (where -1 can't be represented in TValue). Return true = intentionally handled
            // (a no-op), so it is not logged as a skipped/failed property.
            if (NoOpSentinel.IsNumeric(typeof(TValue)) && NoOpSentinel.IsRawSentinel(value))
                return true;

            try
            {
                // Use the handler's TryParseValue method for custom parsing logic
                if (!_handler.TryParseValue(value, out TValue typedValue))
                {
                    ErrorLogging.LogPropertySkipped(Name, entity);
                    return false;
                }

                return _handler.TryLoad(entity, typedValue);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogPropertyLoadException(Name, entity, ex);
                return false;
            }
        }

        public bool TryGetValue(TEntity entity, out object value)
        {
            value = null;
            try
            {
                if (_handler.TryGetValueFromAPI(entity, out TValue typedValue))
                {
                    value = typedValue;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}