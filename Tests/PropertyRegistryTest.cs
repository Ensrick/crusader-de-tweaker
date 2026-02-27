// Tests/PropertyRegistryTest.cs
//
// PURPOSE: Unit tests for PropertyRegistry<TEntity> class.
//
// TESTS COVER:
// - Register() handler registration
// - GetByName() lookup functionality
// - GetAll() enumeration
// - GetApplicable() entity-specific filtering
// - Contains() existence check
// - Count property
// - Duplicate registration handling
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrusaderDETweaker.Config.Toml.Core;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Unit tests for PropertyRegistry class.
    /// </summary>
    internal static class PropertyRegistryTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- PropertyRegistry Test Suite ---");
            
            _passedCount = 0;
            _failedCount = 0;

            // Test registration
            Test_Register_SingleHandler();
            Test_Register_MultipleHandlers();
            Test_Register_DuplicateOverwrites();
            Test_Register_NullThrows();

            // Test lookup
            Test_GetByName_Exists();
            Test_GetByName_NotFound();
            Test_GetByName_NullReturnsNull();
            Test_GetByName_EmptyReturnsNull();

            // Test enumeration
            Test_GetAll_ReturnsAllHandlers();
            Test_GetAll_EmptyRegistry();

            // Test filtering
            Test_GetApplicable_FiltersByEntity();

            // Test contains
            Test_Contains_True();
            Test_Contains_False();

            // Test count
            Test_Count_Empty();
            Test_Count_AfterAdding();

            int totalTests = _passedCount + _failedCount;
            bool allPassed = _failedCount == 0;
            
            Plugin.Logger.LogInfo($"  PropertyRegistry: {_passedCount}/{totalTests} tests passed");
            
            return (allPassed, totalTests);
        }

        // ===================================================
        // Register Tests
        // ===================================================

        private static void Test_Register_SingleHandler()
        {
            var registry = new PropertyRegistry<TestEntity>();
            var handler = new MockPropertyHandler("Health");
            
            registry.Register(handler);
            
            AssertTrue("Register_SingleHandler", registry.Count == 1);
        }

        private static void Test_Register_MultipleHandlers()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            registry.Register(new MockPropertyHandler("Health"));
            registry.Register(new MockPropertyHandler("Speed"));
            registry.Register(new MockPropertyHandler("Damage"));
            
            AssertTrue("Register_MultipleHandlers", registry.Count == 3);
        }

        private static void Test_Register_DuplicateOverwrites()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            var handler1 = new MockPropertyHandler("Health");
            var handler2 = new MockPropertyHandler("Health");
            
            registry.Register(handler1);
            registry.Register(handler2);
            
            // Count should still be 1 (duplicate overwrites)
            AssertTrue("Register_DuplicateOverwrites", registry.Count == 1);
        }

        private static void Test_Register_NullThrows()
        {
            var registry = new PropertyRegistry<TestEntity>();
            bool threw = false;
            
            try
            {
                registry.Register<int>(null);
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }
            
            AssertTrue("Register_NullThrows", threw);
        }

        // ===================================================
        // GetByName Tests
        // ===================================================

        private static void Test_GetByName_Exists()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            registry.Register(new MockPropertyHandler("Speed"));
            
            var handler = registry.GetByName("Speed");
            
            AssertTrue("GetByName_Exists", handler != null && handler.Name == "Speed");
        }

        private static void Test_GetByName_NotFound()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            
            var handler = registry.GetByName("NonExistent");
            
            AssertTrue("GetByName_NotFound", handler == null);
        }

        private static void Test_GetByName_NullReturnsNull()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            
            var handler = registry.GetByName(null);
            
            AssertTrue("GetByName_NullReturnsNull", handler == null);
        }

        private static void Test_GetByName_EmptyReturnsNull()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            
            var handler = registry.GetByName("");
            
            AssertTrue("GetByName_EmptyReturnsNull", handler == null);
        }

        // ===================================================
        // GetAll Tests
        // ===================================================

        private static void Test_GetAll_ReturnsAllHandlers()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            registry.Register(new MockPropertyHandler("Speed"));
            registry.Register(new MockPropertyHandler("Damage"));
            
            var all = registry.GetAll().ToList();
            var names = all.Select(h => h.Name).OrderBy(n => n).ToList();
            
            bool hasAll = names.Count == 3 
                && names[0] == "Damage" 
                && names[1] == "Health" 
                && names[2] == "Speed";
            
            AssertTrue("GetAll_ReturnsAllHandlers", hasAll);
        }

        private static void Test_GetAll_EmptyRegistry()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            var all = registry.GetAll().ToList();
            
            AssertTrue("GetAll_EmptyRegistry", all.Count == 0);
        }

        // ===================================================
        // GetApplicable Tests
        // ===================================================

        private static void Test_GetApplicable_FiltersByEntity()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            // Handler that applies to all
            registry.Register(new MockPropertyHandler("Health"));
            
            // Handler that only applies to EntityB
            registry.Register(new MockFilteredHandler("SpecialAbility", TestEntity.EntityB));
            
            var applicableToA = registry.GetApplicable(TestEntity.EntityA).ToList();
            var applicableToB = registry.GetApplicable(TestEntity.EntityB).ToList();
            
            // EntityA should only have Health
            bool aCorrect = applicableToA.Count == 1 && applicableToA[0].Name == "Health";
            
            // EntityB should have both
            bool bCorrect = applicableToB.Count == 2;
            
            AssertTrue("GetApplicable_FiltersByEntity", aCorrect && bCorrect);
        }

        // ===================================================
        // Contains Tests
        // ===================================================

        private static void Test_Contains_True()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            
            AssertTrue("Contains_True", registry.Contains("Health"));
        }

        private static void Test_Contains_False()
        {
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new MockPropertyHandler("Health"));
            
            AssertTrue("Contains_False", !registry.Contains("Speed"));
        }

        // ===================================================
        // Count Tests
        // ===================================================

        private static void Test_Count_Empty()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            AssertTrue("Count_Empty", registry.Count == 0);
        }

        private static void Test_Count_AfterAdding()
        {
            var registry = new PropertyRegistry<TestEntity>();
            
            registry.Register(new MockPropertyHandler("A"));
            bool count1 = registry.Count == 1;
            
            registry.Register(new MockPropertyHandler("B"));
            bool count2 = registry.Count == 2;
            
            AssertTrue("Count_AfterAdding", count1 && count2);
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
        // Test Types
        // ===================================================

        internal enum TestEntity
        {
            EntityA,
            EntityB,
            EntityC
        }

        /// <summary>
        /// Basic mock handler for testing registry.
        /// </summary>
        private class MockPropertyHandler : PropertyHandler<TestEntity, int>
        {
            public MockPropertyHandler(string name) : base(name) { }

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = 100;
                return true;
            }

            protected override void SetToAPI(TestEntity entity, int value) { }
        }

        /// <summary>
        /// Mock handler that only applies to specific entity.
        /// </summary>
        private class MockFilteredHandler : PropertyHandler<TestEntity, int>
        {
            private readonly TestEntity _allowedEntity;

            public MockFilteredHandler(string name, TestEntity allowedEntity) : base(name)
            {
                _allowedEntity = allowedEntity;
            }

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = 100;
                return true;
            }

            protected override void SetToAPI(TestEntity entity, int value) { }

            internal override bool CanApplyTo(TestEntity entity)
            {
                return entity.Equals(_allowedEntity);
            }
        }
    }
}
