// Tests/PropertyHandlerTest.cs
//
// PURPOSE: Unit tests for PropertyHandler<TEntity, TValue> base class.
//
// TESTS COVER:
// - TryGenerate() template method workflow
// - TryLoad() template method workflow  
// - ValidateValue() default and custom behavior
// - CanApplyTo() entity filtering
// - TryParseValue() type conversion
// - FormatValue() TOML formatting
//
using System;
using System.Text;
using CrusaderDETweaker.Config.Toml.Core;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Unit tests for PropertyHandler base class.
    /// Uses mock implementations to test template method patterns.
    /// </summary>
    internal static class PropertyHandlerTest
    {
        // Counter for tracking test results
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- PropertyHandler Test Suite ---");
            
            _passedCount = 0;
            _failedCount = 0;

            // Test TryParseValue
            Test_TryParseValue_IntToInt();
            Test_TryParseValue_LongToInt();
            Test_TryParseValue_DoubleToFloat();
            Test_TryParseValue_StringToString();
            Test_TryParseValue_InvalidType();

            // Test FormatValue
            Test_FormatValue_String();
            Test_FormatValue_Boolean();
            Test_FormatValue_Float();
            Test_FormatValue_Integer();

            // Test ValidateValue
            Test_ValidateValue_Default();
            Test_ValidateValue_Custom();

            // Test CanApplyTo
            Test_CanApplyTo_Default();
            Test_CanApplyTo_Filtered();

            // Test TryGenerate
            Test_TryGenerate_Success();
            Test_TryGenerate_EntityFiltered();
            Test_TryGenerate_ApiFailure();

            // Test migration (TryGenerateWithOverride) — the "-1 = use default" sentinel logic
            Test_Migration_DefaultValueBecomesSentinel();
            Test_Migration_OverridePreserved();
            Test_Migration_ExistingSentinelKept();
            Test_Migration_UnparseableFallsToSentinel();
            Test_Migration_Float_DefaultBecomesSentinel();
            Test_Migration_Float_OverridePreserved();

            // Test TryLoad
            Test_TryLoad_Success();
            Test_TryLoad_ValidationFailure();
            Test_TryLoad_EntityFiltered();

            int totalTests = _passedCount + _failedCount;
            bool allPassed = _failedCount == 0;
            
            Plugin.Logger.LogInfo($"  PropertyHandler: {_passedCount}/{totalTests} tests passed");
            
            return (allPassed, totalTests);
        }

        // ===================================================
        // TryParseValue Tests
        // ===================================================

        private static void Test_TryParseValue_IntToInt()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            bool success = handler.TryParseValue(42, out int result);
            
            AssertTrue("TryParseValue_IntToInt", success && result == 42);
        }

        private static void Test_TryParseValue_LongToInt()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            // Tomlyn parses integers as long, so we need to handle long → int conversion
            bool success = handler.TryParseValue(42L, out int result);
            
            AssertTrue("TryParseValue_LongToInt", success && result == 42);
        }

        private static void Test_TryParseValue_DoubleToFloat()
        {
            var handler = new MockFloatPropertyHandler("TestProp");
            bool success = handler.TryParseValue(3.14159, out float result);
            
            AssertTrue("TryParseValue_DoubleToFloat", success && Math.Abs(result - 3.14159f) < 0.0001f);
        }

        private static void Test_TryParseValue_StringToString()
        {
            var handler = new MockStringPropertyHandler("TestProp");
            bool success = handler.TryParseValue("hello", out string result);
            
            AssertTrue("TryParseValue_StringToString", success && result == "hello");
        }

        private static void Test_TryParseValue_InvalidType()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            // Try to parse a non-convertible type
            bool success = handler.TryParseValue("not a number", out int result);
            
            AssertTrue("TryParseValue_InvalidType", !success);
        }

        // ===================================================
        // FormatValue Tests
        // ===================================================

        private static void Test_FormatValue_String()
        {
            var handler = new MockStringPropertyHandler("TestProp");
            string formatted = handler.FormatValue("hello");
            
            AssertTrue("FormatValue_String", formatted == "\"hello\"");
        }

        private static void Test_FormatValue_Boolean()
        {
            var handler = new MockBoolPropertyHandler("TestProp");
            string formattedTrue = handler.FormatValue(true);
            string formattedFalse = handler.FormatValue(false);
            
            AssertTrue("FormatValue_Boolean", formattedTrue == "true" && formattedFalse == "false");
        }

        private static void Test_FormatValue_Float()
        {
            var handler = new MockFloatPropertyHandler("TestProp");
            string formatted = handler.FormatValue(3.14159f);
            
            // Should be limited to 3 decimal places
            AssertTrue("FormatValue_Float", formatted == "3.142");
        }

        private static void Test_FormatValue_Integer()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            string formatted = handler.FormatValue(42);
            
            AssertTrue("FormatValue_Integer", formatted == "42");
        }

        // ===================================================
        // ValidateValue Tests
        // ===================================================

        private static void Test_ValidateValue_Default()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            // Default validation accepts all values
            bool valid = handler.ValidateValue(12345);
            
            AssertTrue("ValidateValue_Default", valid);
        }

        private static void Test_ValidateValue_Custom()
        {
            var handler = new MockValidatingPropertyHandler("TestProp", minValue: 0, maxValue: 100);
            
            bool valid50 = handler.ValidateValue(50);
            bool validNegative = handler.ValidateValue(-10);
            bool validOverMax = handler.ValidateValue(200);
            
            AssertTrue("ValidateValue_Custom", valid50 && !validNegative && !validOverMax);
        }

        // ===================================================
        // CanApplyTo Tests
        // ===================================================

        private static void Test_CanApplyTo_Default()
        {
            var handler = new MockIntPropertyHandler("TestProp");
            // Default applies to all entities
            bool applies = handler.CanApplyTo(TestEntity.EntityA);
            
            AssertTrue("CanApplyTo_Default", applies);
        }

        private static void Test_CanApplyTo_Filtered()
        {
            var handler = new MockFilteredPropertyHandler("TestProp", TestEntity.EntityB);
            
            bool appliesA = handler.CanApplyTo(TestEntity.EntityA);
            bool appliesB = handler.CanApplyTo(TestEntity.EntityB);
            
            AssertTrue("CanApplyTo_Filtered", !appliesA && appliesB);
        }

        // ===================================================
        // TryGenerate Tests
        // ===================================================

        private static void Test_TryGenerate_Success()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiValue(1000);
            
            var sb = new StringBuilder();
            bool success = handler.TryGenerate(TestEntity.EntityA, sb);
            
            string output = sb.ToString();
            // Numeric properties now generate the "-1 = use game default (no override)" sentinel,
            // with the live game value in the "# Default:" comment.
            AssertTrue("TryGenerate_Success", success && output.Contains("Health = -1") && output.Contains("# Default: 1000"));
        }

        private static void Test_TryGenerate_EntityFiltered()
        {
            var handler = new MockFilteredPropertyHandler("Health", TestEntity.EntityB);
            handler.SetApiValue(1000);
            
            var sb = new StringBuilder();
            bool success = handler.TryGenerate(TestEntity.EntityA, sb);
            
            // Should return false because entity is filtered out
            AssertTrue("TryGenerate_EntityFiltered", !success && sb.Length == 0);
        }

        private static void Test_TryGenerate_ApiFailure()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiFailure(true);
            
            var sb = new StringBuilder();
            bool success = handler.TryGenerate(TestEntity.EntityA, sb);
            
            AssertTrue("TryGenerate_ApiFailure", !success);
        }

        // ===================================================
        // Migration Tests (TryGenerateWithOverride)
        // ===================================================

        private static System.Collections.Generic.Dictionary<string, object> Existing(string key, object value)
            => new System.Collections.Generic.Dictionary<string, object> { { key, value } };

        private static void Test_Migration_DefaultValueBecomesSentinel()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiValue(1000); // game default
            var sb = new StringBuilder();
            // Existing value equals the game default → should migrate to the -1 sentinel.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("Health", 1000L));
            string output = sb.ToString();
            AssertTrue("Migration_DefaultBecomesSentinel", ok && output.Contains("Health = -1") && output.Contains("# Default: 1000"));
        }

        private static void Test_Migration_OverridePreserved()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiValue(1000);
            var sb = new StringBuilder();
            // Existing value differs from default → preserved as a genuine override.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("Health", 5000L));
            string output = sb.ToString();
            AssertTrue("Migration_OverridePreserved", ok && output.Contains("Health = 5000") && output.Contains("# Default: 1000"));
        }

        private static void Test_Migration_ExistingSentinelKept()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiValue(1000);
            var sb = new StringBuilder();
            // Existing value is already the sentinel → kept as a no-op.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("Health", -1L));
            string output = sb.ToString();
            AssertTrue("Migration_ExistingSentinelKept", ok && output.Contains("Health = -1") && output.Contains("# Default: 1000"));
        }

        private static void Test_Migration_UnparseableFallsToSentinel()
        {
            var handler = new MockIntPropertyHandler("Health");
            handler.SetApiValue(1000);
            var sb = new StringBuilder();
            // Unparseable existing value → fall through to fresh sentinel generation.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("Health", "garbage"));
            AssertTrue("Migration_UnparseableFallsToSentinel", ok && sb.ToString().Contains("Health = -1"));
        }

        private static void Test_Migration_Float_DefaultBecomesSentinel()
        {
            var handler = new MockFloatPropertyHandler("MeleeArmorMultiplier");
            handler.SetApiValue(1.0f);
            var sb = new StringBuilder();
            // Stored 1.0 equals the default within epsilon → migrate to -1.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("MeleeArmorMultiplier", 1.0));
            AssertTrue("Migration_Float_DefaultBecomesSentinel", ok && sb.ToString().Contains("MeleeArmorMultiplier = -1"));
        }

        private static void Test_Migration_Float_OverridePreserved()
        {
            var handler = new MockFloatPropertyHandler("MeleeArmorMultiplier");
            handler.SetApiValue(1.0f);
            var sb = new StringBuilder();
            // A real override well outside epsilon → preserved.
            bool ok = handler.TryGenerateWithOverride(TestEntity.EntityA, sb, Existing("MeleeArmorMultiplier", 1.5));
            AssertTrue("Migration_Float_OverridePreserved", ok && sb.ToString().Contains("MeleeArmorMultiplier = 1.5"));
        }

        // ===================================================
        // TryLoad Tests
        // ===================================================

        private static void Test_TryLoad_Success()
        {
            var handler = new MockIntPropertyHandler("Health");
            
            bool success = handler.TryLoad(TestEntity.EntityA, 1000);
            
            AssertTrue("TryLoad_Success", success && handler.LastSetValue == 1000);
        }

        private static void Test_TryLoad_ValidationFailure()
        {
            var handler = new MockValidatingPropertyHandler("Health", minValue: 0, maxValue: 100);
            
            bool success = handler.TryLoad(TestEntity.EntityA, 200);
            
            // Should fail validation (200 > max of 100)
            AssertTrue("TryLoad_ValidationFailure", !success);
        }

        private static void Test_TryLoad_EntityFiltered()
        {
            var handler = new MockFilteredPropertyHandler("Health", TestEntity.EntityB);
            
            bool success = handler.TryLoad(TestEntity.EntityA, 1000);
            
            // Should fail because entity is filtered
            AssertTrue("TryLoad_EntityFiltered", !success);
        }

        // ===================================================
        // Helpers
        // ===================================================

        private static void AssertTrue(string testName, bool condition)
        {
            if (condition)
            {
                _passedCount++;
                Plugin.Logger.LogDebug($"    [PASS] {testName}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"    [FAIL] {testName}");
            }
        }

        // ===================================================
        // Test Entity Enum
        // ===================================================

        internal enum TestEntity
        {
            EntityA,
            EntityB,
            EntityC
        }

        // ===================================================
        // Mock Property Handlers
        // ===================================================

        /// <summary>
        /// Basic integer property handler for testing.
        /// </summary>
        private class MockIntPropertyHandler : PropertyHandler<TestEntity, int>
        {
            private int _apiValue;
            private bool _apiFailure;
            public int LastSetValue { get; private set; }

            public MockIntPropertyHandler(string name) : base(name) { }

            public void SetApiValue(int value) => _apiValue = value;
            public void SetApiFailure(bool fail) => _apiFailure = fail;

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = _apiValue;
                return !_apiFailure;
            }

            protected override void SetToAPI(TestEntity entity, int value)
            {
                LastSetValue = value;
            }

            // Real numeric handlers expose the game value as the default (for the "# Default:" comment).
            protected override bool TryGetOriginalValue(TestEntity entity, out int defaultValue)
                => TryGetFromAPI(entity, out defaultValue);

            // Expose internal methods for testing
            public new string FormatValue(int value) => base.FormatValue(value);
            public new bool TryParseValue(object tomlValue, out int result) => base.TryParseValue(tomlValue, out result);
            public new bool ValidateValue(int value) => base.ValidateValue(value);
            public new bool CanApplyTo(TestEntity entity) => base.CanApplyTo(entity);
        }

        /// <summary>
        /// Float property handler for testing.
        /// </summary>
        private class MockFloatPropertyHandler : PropertyHandler<TestEntity, float>
        {
            private float _apiValue;
            private bool _hasApiValue;

            public MockFloatPropertyHandler(string name) : base(name) { }

            public void SetApiValue(float value) { _apiValue = value; _hasApiValue = true; }

            protected override bool TryGetFromAPI(TestEntity entity, out float value)
            {
                value = _apiValue;
                return _hasApiValue;
            }

            protected override bool TryGetOriginalValue(TestEntity entity, out float value)
                => TryGetFromAPI(entity, out value);

            protected override void SetToAPI(TestEntity entity, float value) { }

            public new string FormatValue(float value) => base.FormatValue(value);
            public new bool TryParseValue(object tomlValue, out float result) => base.TryParseValue(tomlValue, out result);
        }

        /// <summary>
        /// String property handler for testing.
        /// </summary>
        private class MockStringPropertyHandler : PropertyHandler<TestEntity, string>
        {
            public MockStringPropertyHandler(string name) : base(name) { }

            protected override bool TryGetFromAPI(TestEntity entity, out string value)
            {
                value = null;
                return false;
            }

            protected override void SetToAPI(TestEntity entity, string value) { }

            public new string FormatValue(string value) => base.FormatValue(value);
            public new bool TryParseValue(object tomlValue, out string result) => base.TryParseValue(tomlValue, out result);
        }

        /// <summary>
        /// Boolean property handler for testing.
        /// </summary>
        private class MockBoolPropertyHandler : PropertyHandler<TestEntity, bool>
        {
            public MockBoolPropertyHandler(string name) : base(name) { }

            protected override bool TryGetFromAPI(TestEntity entity, out bool value)
            {
                value = false;
                return false;
            }

            protected override void SetToAPI(TestEntity entity, bool value) { }

            public new string FormatValue(bool value) => base.FormatValue(value);
        }

        /// <summary>
        /// Property handler with custom validation.
        /// </summary>
        private class MockValidatingPropertyHandler : PropertyHandler<TestEntity, int>
        {
            private readonly int _minValue;
            private readonly int _maxValue;
            private int _apiValue;
            public int LastSetValue { get; private set; }

            public MockValidatingPropertyHandler(string name, int minValue, int maxValue) : base(name)
            {
                _minValue = minValue;
                _maxValue = maxValue;
            }

            public void SetApiValue(int value) => _apiValue = value;

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = _apiValue;
                return true;
            }

            protected override void SetToAPI(TestEntity entity, int value)
            {
                LastSetValue = value;
            }

            internal override bool ValidateValue(int value)
            {
                return value >= _minValue && value <= _maxValue;
            }
        }

        /// <summary>
        /// Property handler that only applies to specific entity.
        /// </summary>
        private class MockFilteredPropertyHandler : PropertyHandler<TestEntity, int>
        {
            private readonly TestEntity _allowedEntity;
            private int _apiValue;
            public int LastSetValue { get; private set; }

            public MockFilteredPropertyHandler(string name, TestEntity allowedEntity) : base(name)
            {
                _allowedEntity = allowedEntity;
            }

            public void SetApiValue(int value) => _apiValue = value;

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = _apiValue;
                return true;
            }

            protected override void SetToAPI(TestEntity entity, int value)
            {
                LastSetValue = value;
            }

            internal override bool CanApplyTo(TestEntity entity)
            {
                return entity.Equals(_allowedEntity);
            }
        }
    }
}
