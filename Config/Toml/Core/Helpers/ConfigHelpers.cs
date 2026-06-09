// Config/Toml/Core/ConfigHelpers.cs
//
// PURPOSE: Legacy compatibility wrapper for backward compatibility.
//
// STATUS: DEPRECATED - kept for backward compatibility only.
//
// USAGE:
// - All methods redirect to specialized classes (TomlFormatter, TypeConverter, ValueValidator, ErrorLogging)
// - Existing code that uses ConfigHelpers will continue to work
// - New code should use specialized classes directly
//
// IMPORTANT FOR AI AGENTS:
// - DO NOT add new methods to this class - use specialized classes instead
// - This is a compatibility layer only - all logic is in specialized classes
// - Will be removed in a future version once all code is migrated
//
namespace CrusaderDETweaker.Config.Toml.Core
{
    /// <summary>
    /// Legacy compatibility class - redirects to new specialized classes.
    /// 
    /// This class is kept for backward compatibility with existing code.
    /// New code should use the specialized classes directly:
    /// - TomlFormatter: TOML formatting
    /// - TypeConverter: Type conversion
    /// - ValueValidator: Value validation
    /// - ErrorLogging: Error logging
    /// </summary>
    internal static class ConfigHelpers
    {
        // Redirect to new specialized classes for backward compatibility
        public static string FormatValueForToml(object value) => TomlFormatter.FormatValue(value);
        public static void WriteTomlProperty(System.Text.StringBuilder sb, string name, object value, object defaultValue = null) 
            => TomlFormatter.WriteProperty(sb, name, value, defaultValue);
        public static void WriteTomlSection(System.Text.StringBuilder sb, string sectionName) 
            => TomlFormatter.WriteSection(sb, sectionName);
        public static void WriteBlankLine(System.Text.StringBuilder sb) 
            => TomlFormatter.WriteBlankLine(sb);
        public static void WriteComment(System.Text.StringBuilder sb, string comment) 
            => TomlFormatter.WriteComment(sb, comment);
        public static bool TryConvertValue<T>(object value, out T result) 
            => TypeConverter.TryConvert(value, out result);
        public static bool IsInRange<T>(T value, T min, T max) where T : System.IComparable<T> 
            => ValueValidator.IsInRange(value, min, max);
        public static bool IsPositive(int value) => ValueValidator.IsPositive(value);
        public static bool IsPositive(uint value) => ValueValidator.IsPositive(value);
        public static bool IsPositive(float value) => ValueValidator.IsPositive(value);
        public static bool IsNonNegative(int value) => ValueValidator.IsNonNegative(value);
        public static bool IsNonNegative(float value) => ValueValidator.IsNonNegative(value);
        public static bool TryConvertToFloat(object value, out float result) 
            => TypeConverter.TryConvertToFloat(value, out result);
        public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct 
            => TypeConverter.TryParseEnum(value, out result);
        public static void WriteSectionDivider(System.Text.StringBuilder sb, string sectionTitle) 
            => TomlFormatter.WriteSectionDivider(sb, sectionTitle);

        // ErrorLogging is now a separate class
        public static class ErrorLogging
        {
            public static void LogGenerationSkipped(string propertyName, object entity, string reason) 
                => Core.ErrorLogging.LogGenerationSkipped(propertyName, entity, reason);
            public static void LogGenerationException(string propertyName, object entity, System.Exception ex) 
                => Core.ErrorLogging.LogGenerationException(propertyName, entity, ex);
            public static void LogValidationFailure(string propertyName, object entity, object value, string reason = null) 
                => Core.ErrorLogging.LogValidationFailure(propertyName, entity, value, reason);
            public static void LogPropertyLoadException(string propertyName, object entity, System.Exception ex) 
                => Core.ErrorLogging.LogPropertyLoadException(propertyName, entity, ex);
            public static void LogConfigLoadException(string configName, System.Exception ex) 
                => Core.ErrorLogging.LogConfigLoadException(configName, ex);
            public static void LogConfigGenerationException(string configName, System.Exception ex) 
                => Core.ErrorLogging.LogConfigGenerationException(configName, ex);
            public static void LogMissingConfigFile(string filePath, string fallbackMessage = "Using default values") 
                => Core.ErrorLogging.LogMissingConfigFile(filePath, fallbackMessage);
            public static void LogUnknownEntity(string entityTypeName, string entityName) 
                => Core.ErrorLogging.LogUnknownEntity(entityTypeName, entityName);
            public static void LogUnknownProperty(string entityTypeName, object entity, string propertyName) 
                => Core.ErrorLogging.LogUnknownProperty(entityTypeName, entity, propertyName);
            public static void LogPropertySkipped(string propertyName, object entity) 
                => Core.ErrorLogging.LogPropertySkipped(propertyName, entity);
        }
    }
}
