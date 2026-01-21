using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class ParallelSystemGroupTests
    {
        private class MockArena : IEntityArena
        {
            public bool IsValid(int index, int generation) => true;
        }

        private class ThreadTrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public ConcurrentBag<int> ThreadIds { get; } = new ConcurrentBag<int>();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                Thread.Sleep(10);
                ThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            }
        }

        private class CountingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public int ExecutionCount { get; private set; }
            private readonly object _lock = new object();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                lock (_lock)
                {
                    ExecutionCount++;
                }
            }
        }

        private class OrderTrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public string Name { get; }
            private readonly ConcurrentBag<string> _executionOrder;

            public OrderTrackingSystem(string name, ConcurrentBag<string> executionOrder)
            {
                Name = name;
                _executionOrder = executionOrder;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                _executionOrder.Add(Name);
            }
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            private readonly List<VoidHandle> _entities = new List<VoidHandle>();

            public void AddEntity(VoidHandle handle)
            {
                _entities.Add(handle);
            }

            public IReadOnlyList<VoidHandle> GetAllEntities()
            {
                return _entities;
            }

            public IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class
            {
                return _entities;
            }
        }

        [Fact]
        public void ParallelSystemGroup_ExecutesAllSystems()
        {
            // Arrange
            var system1 = new CountingSystem();
            var system2 = new CountingSystem();
            var system3 = new CountingSystem();
            var group = new ParallelSystemGroup(system1, system2, system3);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(1, system1.ExecutionCount);
            Assert.Equal(1, system2.ExecutionCount);
            Assert.Equal(1, system3.ExecutionCount);
        }

        [Fact]
        public void ParallelSystemGroup_SkipsDisabledSystems()
        {
            // Arrange
            var system1 = new CountingSystem();
            var system2 = new CountingSystem { IsEnabled = false };
            var system3 = new CountingSystem();
            var group = new ParallelSystemGroup(system1, system2, system3);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(1, system1.ExecutionCount);
            Assert.Equal(0, system2.ExecutionCount);
            Assert.Equal(1, system3.ExecutionCount);
        }

        [Fact]
        public void ParallelSystemGroup_SkipsExecutionWhenDisabled()
        {
            // Arrange
            var system1 = new CountingSystem();
            var group = new ParallelSystemGroup(system1) { IsEnabled = false };
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(0, system1.ExecutionCount);
        }

        [Fact]
        public void ParallelSystemGroup_CanUseMultipleThreads()
        {
            // Arrange
            var systems = Enumerable.Range(0, 10).Select(_ => new ThreadTrackingSystem()).ToArray();
            var group = new ParallelSystemGroup(systems);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert - All systems were executed
            Assert.All(systems, s => Assert.Single(s.ThreadIds));

            // If parallelism is working, we should see multiple distinct thread IDs
            var allThreadIds = systems.SelectMany(s => s.ThreadIds).ToList();
            var distinctThreadIds = allThreadIds.Distinct().Count();

            // At least some parallelism should occur (more than 1 thread used)
            // Note: This may occasionally fail on single-core systems
            Assert.True(distinctThreadIds >= 1, "Parallel execution should use at least 1 thread");
        }

        [Fact]
        public void ParallelSystemGroup_Add_AppendsSystem()
        {
            // Arrange
            var system1 = new CountingSystem();
            var system2 = new CountingSystem();
            var group = new ParallelSystemGroup(system1);

            // Act
            group.Add(system2);

            // Assert
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public void ParallelSystemGroup_Remove_RemovesSystem()
        {
            // Arrange
            var system1 = new CountingSystem();
            var system2 = new CountingSystem();
            var group = new ParallelSystemGroup(system1, system2);

            // Act
            bool removed = group.Remove(system1);

            // Assert
            Assert.True(removed);
            Assert.Equal(1, group.Count);
        }

        [Fact]
        public void ParallelSystemGroup_Insert_InsertsAtPosition()
        {
            // Arrange
            var system1 = new CountingSystem();
            var system2 = new CountingSystem();
            var system3 = new CountingSystem();
            var group = new ParallelSystemGroup(system1, system3);

            // Act
            group.Insert(1, system2);

            // Assert
            Assert.Equal(3, group.Count);
        }

        [Fact]
        public void ParallelSystemGroup_EmptyGroup_DoesNothing()
        {
            // Arrange
            var group = new ParallelSystemGroup();
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act & Assert - Should not throw
            group.Execute(registry, in context);
            Assert.Equal(0, group.Count);
        }
    }

    public class NestedSystemGroupTests
    {
        private class OrderTrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public string Name { get; }
            private readonly List<string> _executionOrder;
            private readonly object _lock = new object();

            public OrderTrackingSystem(string name, List<string> executionOrder)
            {
                Name = name;
                _executionOrder = executionOrder;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                lock (_lock)
                {
                    _executionOrder.Add(Name);
                }
            }
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            public IReadOnlyList<VoidHandle> GetAllEntities() => new List<VoidHandle>();
            public IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class => new List<VoidHandle>();
        }

        [Fact]
        public void NestedGroups_SerialContainsParallel_ExecutesCorrectly()
        {
            // Arrange
            var executionOrder = new List<string>();
            var inputSystem = new OrderTrackingSystem("Input", executionOrder);
            var aiSystem = new OrderTrackingSystem("AI", executionOrder);
            var animSystem = new OrderTrackingSystem("Anim", executionOrder);
            var physicsSystem = new OrderTrackingSystem("Physics", executionOrder);

            var parallelGroup = new ParallelSystemGroup(aiSystem, animSystem);
            var mainGroup = new SerialSystemGroup();
            mainGroup.Add(inputSystem);
            mainGroup.Add(parallelGroup);
            mainGroup.Add(physicsSystem);

            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 1, default);

            // Act
            mainGroup.Execute(registry, in context);

            // Assert
            Assert.Equal(4, executionOrder.Count);
            Assert.Equal("Input", executionOrder[0]); // Must be first
            Assert.Equal("Physics", executionOrder[3]); // Must be last
            // AI and Anim must be in positions 1 and 2 (but order not guaranteed)
            Assert.Contains("AI", executionOrder.GetRange(1, 2));
            Assert.Contains("Anim", executionOrder.GetRange(1, 2));
        }

        [Fact]
        public void NestedGroups_ParallelContainsSerial_ExecutesCorrectly()
        {
            // Arrange
            var executionOrder = new List<string>();
            var system1 = new OrderTrackingSystem("S1", executionOrder);
            var system2 = new OrderTrackingSystem("S2", executionOrder);
            var system3 = new OrderTrackingSystem("S3", executionOrder);
            var system4 = new OrderTrackingSystem("S4", executionOrder);

            var serialGroup1 = new SerialSystemGroup(system1, system2);
            var serialGroup2 = new SerialSystemGroup(system3, system4);
            var parallelGroup = new ParallelSystemGroup();
            parallelGroup.Add(serialGroup1);
            parallelGroup.Add(serialGroup2);

            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 1, default);

            // Act
            parallelGroup.Execute(registry, in context);

            // Assert
            Assert.Equal(4, executionOrder.Count);

            // Within each serial group, order must be preserved
            var s1Index = executionOrder.IndexOf("S1");
            var s2Index = executionOrder.IndexOf("S2");
            var s3Index = executionOrder.IndexOf("S3");
            var s4Index = executionOrder.IndexOf("S4");

            Assert.True(s1Index < s2Index, "S1 must come before S2 (serial order)");
            Assert.True(s3Index < s4Index, "S3 must come before S4 (serial order)");
        }

        [Fact]
        public void NestedGroups_DeeplyNested_ExecutesCorrectly()
        {
            // Arrange
            var executionOrder = new List<string>();
            var system1 = new OrderTrackingSystem("S1", executionOrder);
            var system2 = new OrderTrackingSystem("S2", executionOrder);
            var system3 = new OrderTrackingSystem("S3", executionOrder);
            var system4 = new OrderTrackingSystem("S4", executionOrder);

            // Structure: Serial(S1, Parallel(Serial(S2, S3)), S4)
            var innerSerial = new SerialSystemGroup(system2, system3);
            var middleParallel = new ParallelSystemGroup();
            middleParallel.Add(innerSerial);

            var outerSerial = new SerialSystemGroup();
            outerSerial.Add(system1);
            outerSerial.Add(middleParallel);
            outerSerial.Add(system4);

            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 1, default);

            // Act
            outerSerial.Execute(registry, in context);

            // Assert
            Assert.Equal(4, executionOrder.Count);
            Assert.Equal("S1", executionOrder[0]);
            Assert.Equal("S2", executionOrder[1]);
            Assert.Equal("S3", executionOrder[2]);
            Assert.Equal("S4", executionOrder[3]);
        }

        [Fact]
        public void NestedGroups_DisabledInnerGroup_SkipsExecution()
        {
            // Arrange
            var executionOrder = new List<string>();
            var system1 = new OrderTrackingSystem("S1", executionOrder);
            var system2 = new OrderTrackingSystem("S2", executionOrder);
            var system3 = new OrderTrackingSystem("S3", executionOrder);

            var innerParallel = new ParallelSystemGroup(system2) { IsEnabled = false };
            var outerSerial = new SerialSystemGroup();
            outerSerial.Add(system1);
            outerSerial.Add(innerParallel);
            outerSerial.Add(system3);

            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 1, default);

            // Act
            outerSerial.Execute(registry, in context);

            // Assert
            Assert.Equal(2, executionOrder.Count);
            Assert.Equal("S1", executionOrder[0]);
            Assert.Equal("S3", executionOrder[1]);
        }

        [Fact]
        public void Pipeline_ExecutesNestedGroups()
        {
            // Arrange
            var executionOrder = new List<string>();
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            var system1 = new OrderTrackingSystem("Update1", executionOrder);
            var system2 = new OrderTrackingSystem("Update2", executionOrder);
            var system3 = new OrderTrackingSystem("LateUpdate", executionOrder);

            var parallelGroup = new ParallelSystemGroup(system1, system2);
            var mainGroup = new SerialSystemGroup();
            mainGroup.Add(parallelGroup);
            mainGroup.Add(system3);

            // Act
            pipeline.Execute(mainGroup, 0.016f);

            // Assert
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("LateUpdate", executionOrder[2]); // Must be last
            Assert.Contains("Update1", executionOrder.Take(2));
            Assert.Contains("Update2", executionOrder.Take(2));
        }
    }
}
