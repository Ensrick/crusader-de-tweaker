// Tests/EntityProcessorTest.cs
//
// PURPOSE: Unit tests for EntityProcessor static class.
//
// TESTS COVER:
// - ProcessEntities() main workflow
// - Entity parsing and validation
// - Non-modifiable entity filtering
// - Property application error handling
// - Count tracking (processed, skipped, errors)
//
// NOTE: EntityProcessor works with Tomlyn's TomlTable which requires
// constructing test data structures. Tests use mock implementations.
//
using System;
using System.Collections.Generic;
using System.Text;
using CrusaderDETweaker.Config.Toml.Core;
using Tomlyn.Model;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Unit tests for EntityProcessor class.
    /// </summary>
    internal static class EntityProcessorTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- EntityProcessor Test Suite ---");
            
            _passedCount = 0;
            _failedCount = 0;

            // Test ProcessEntities
            Test_ProcessEntities_EmptyModel();
            Test_ProcessEntities_SingleEntity();
            Test_ProcessEntities_MultipleEntities();
            Test_ProcessEntities_SkipsNonModifiable();
            Test_ProcessEntities_UnknownEntitySkipped();
            Test_ProcessEntities_PropertyProcessing();
            Test_ProcessEntities_UnknownPropertySkipped();

            int totalTests = _passedCount + _failedCount;
            bool allPassed = _failedCount == 0;
            
            Plugin.Logger.LogInfo($"  EntityProcessor: {_passedCount}/{totalTests} tests passed");
            
            return (allPassed, totalTests);
        }

        // ===================================================
        // ProcessEntities Tests
        // ===================================================

        private static void Test_ProcessEntities_EmptyModel()
        {
            var tomlModel = new TomlTable();
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            AssertTrue("ProcessEntities_EmptyModel", 
                processed == 0 && skipped == 0 && errors == 0);
        }

        private static void Test_ProcessEntities_SingleEntity()
        {
            var tomlModel = new TomlTable();
            var entityTable = new TomlTable();
            entityTable["Health"] = 1000L;
            tomlModel["EntityA"] = entityTable;
            
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            AssertTrue("ProcessEntities_SingleEntity", 
                processed == 1 && skipped == 0 && errors == 0);
        }

        private static void Test_ProcessEntities_MultipleEntities()
        {
            var tomlModel = new TomlTable();
            
            var entityA = new TomlTable();
            entityA["Health"] = 1000L;
            tomlModel["EntityA"] = entityA;
            
            var entityB = new TomlTable();
            entityB["Health"] = 2000L;
            tomlModel["EntityB"] = entityB;
            
            var entityC = new TomlTable();
            entityC["Health"] = 3000L;
            tomlModel["EntityC"] = entityC;
            
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            AssertTrue("ProcessEntities_MultipleEntities", 
                processed == 3 && skipped == 0 && errors == 0);
        }

        private static void Test_ProcessEntities_SkipsNonModifiable()
        {
            var tomlModel = new TomlTable();
            
            var entityA = new TomlTable();
            entityA["Health"] = 1000L;
            tomlModel["EntityA"] = entityA;
            
            var entityB = new TomlTable();
            entityB["Health"] = 2000L;
            tomlModel["EntityB"] = entityB;
            
            var registry = CreateTestRegistry();
            
            // EntityB is non-modifiable
            var nonModifiable = new TestEntity[] { TestEntity.EntityB };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            AssertTrue("ProcessEntities_SkipsNonModifiable", 
                processed == 1 && skipped == 1 && errors == 0);
        }

        private static void Test_ProcessEntities_UnknownEntitySkipped()
        {
            var tomlModel = new TomlTable();
            
            var entityA = new TomlTable();
            entityA["Health"] = 1000L;
            tomlModel["EntityA"] = entityA;
            
            // Unknown entity - won't parse
            var unknownEntity = new TomlTable();
            unknownEntity["Health"] = 9999L;
            tomlModel["UnknownEntity"] = unknownEntity;
            
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            // Unknown entity is neither processed nor skipped, just logged
            AssertTrue("ProcessEntities_UnknownEntitySkipped", 
                processed == 1 && skipped == 0 && errors == 0);
        }

        private static void Test_ProcessEntities_PropertyProcessing()
        {
            var tomlModel = new TomlTable();
            
            var entityA = new TomlTable();
            entityA["Health"] = 1000L;
            entityA["Speed"] = 50L;
            tomlModel["EntityA"] = entityA;
            
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            // Verify both properties were processed (via the mock handler)
            bool healthSet = TestPropertyHandler.GetLastValue(TestEntity.EntityA, "Health") == 1000;
            bool speedSet = TestPropertyHandler.GetLastValue(TestEntity.EntityA, "Speed") == 50;
            
            AssertTrue("ProcessEntities_PropertyProcessing", 
                processed == 1 && healthSet && speedSet);
        }

        private static void Test_ProcessEntities_UnknownPropertySkipped()
        {
            var tomlModel = new TomlTable();
            
            var entityA = new TomlTable();
            entityA["Health"] = 1000L;
            entityA["UnknownProperty"] = 9999L;  // Not registered in registry
            tomlModel["EntityA"] = entityA;
            
            var registry = CreateTestRegistry();
            var nonModifiable = new TestEntity[] { };
            
            var (processed, skipped, errors) = EntityProcessor.ProcessEntities(
                tomlModel,
                registry,
                nonModifiable,
                "TestEntity",
                ParseTestEntity
            );
            
            // Entity still processed, unknown property just logged
            AssertTrue("ProcessEntities_UnknownPropertySkipped", 
                processed == 1 && errors == 0);
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

        private static TestEntity? ParseTestEntity(string name)
        {
            switch (name)
            {
                case "EntityA": return TestEntity.EntityA;
                case "EntityB": return TestEntity.EntityB;
                case "EntityC": return TestEntity.EntityC;
                default: return null;
            }
        }

        private static PropertyRegistry<TestEntity> CreateTestRegistry()
        {
            TestPropertyHandler.ClearValues();
            
            var registry = new PropertyRegistry<TestEntity>();
            registry.Register(new TestPropertyHandler("Health"));
            registry.Register(new TestPropertyHandler("Speed"));
            registry.Register(new TestPropertyHandler("Damage"));
            return registry;
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
        /// Mock property handler that tracks set values for verification.
        /// </summary>
        private class TestPropertyHandler : PropertyHandler<TestEntity, int>
        {
            // Static storage to track values set across all handler instances
            private static readonly Dictionary<(TestEntity, string), int> _setValues 
                = new Dictionary<(TestEntity, string), int>();

            public static void ClearValues() => _setValues.Clear();
            
            public static int GetLastValue(TestEntity entity, string propName)
            {
                return _setValues.TryGetValue((entity, propName), out var val) ? val : -1;
            }

            public TestPropertyHandler(string name) : base(name) { }

            protected override bool TryGetFromAPI(TestEntity entity, out int value)
            {
                value = 100;
                return true;
            }

            protected override void SetToAPI(TestEntity entity, int value)
            {
                _setValues[(entity, Name)] = value;
            }
        }
    }
}
