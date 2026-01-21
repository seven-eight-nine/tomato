using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class SystemGroupTests
    {
        private class MockArena : IEntityArena
        {
            public bool IsValid(int index, int generation) => true;
        }

        private class TestSerialSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> SharedProcessedOrder { get; }
            public int SystemId { get; }

            public TestSerialSystem(int id, List<int> sharedOrder)
            {
                SystemId = id;
                SharedProcessedOrder = sharedOrder;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                SharedProcessedOrder.Add(SystemId);
            }
        }

        private class TestParallelSystem : IParallelSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public int ProcessedCount { get; private set; }
            private readonly object _lock = new object();

            public void ProcessEntity(AnyHandle handle, in SystemContext context)
            {
                lock (_lock)
                {
                    ProcessedCount++;
                }
            }
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            private readonly List<AnyHandle> _entities = new List<AnyHandle>();

            public void AddEntity(AnyHandle handle)
            {
                _entities.Add(handle);
            }

            public IReadOnlyList<AnyHandle> GetAllEntities()
            {
                return _entities;
            }

            public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class
            {
                return _entities;
            }
        }

        [Fact]
        public void SystemGroup_ExecutesSystems_InOrder()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var system2 = new TestSerialSystem(2, processedOrder);
            var system3 = new TestSerialSystem(3, processedOrder);

            var group = new SerialSystemGroup(system1, system2, system3);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, processedOrder);
        }

        [Fact]
        public void SystemGroup_SkipsDisabledSystems()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var system2 = new TestSerialSystem(2, processedOrder) { IsEnabled = false };
            var system3 = new TestSerialSystem(3, processedOrder);

            var group = new SerialSystemGroup(system1, system2, system3);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(new[] { 1, 3 }, processedOrder);
        }

        [Fact]
        public void SystemGroup_SkipsExecutionWhenDisabled()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var group = new SerialSystemGroup(system1) { IsEnabled = false };
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Empty(processedOrder);
        }

        [Fact]
        public void ParallelSystem_ProcessesAllEntities()
        {
            // Arrange
            var system = new TestParallelSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var arena = new MockArena();

            // Add some entities
            for (int i = 0; i < 10; i++)
            {
                registry.AddEntity(new AnyHandle(arena, i, 0));
            }

            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(10, system.ProcessedCount);
        }

        [Fact]
        public void SystemGroup_Add_AppendsSystem()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var system2 = new TestSerialSystem(2, processedOrder);
            var group = new SerialSystemGroup(system1);

            // Act
            group.Add(system2);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(2, group.Count);
            Assert.Equal(new[] { 1, 2 }, processedOrder);
        }

        [Fact]
        public void SystemGroup_Insert_InsertsAtPosition()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var system2 = new TestSerialSystem(2, processedOrder);
            var system3 = new TestSerialSystem(3, processedOrder);
            var group = new SerialSystemGroup(system1, system3);

            // Act
            group.Insert(1, system2);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);
            group.Execute(registry, in context);

            // Assert
            Assert.Equal(3, group.Count);
            Assert.Equal(new[] { 1, 2, 3 }, processedOrder);
        }

        [Fact]
        public void SystemGroup_Remove_RemovesSystem()
        {
            // Arrange
            var processedOrder = new List<int>();
            var system1 = new TestSerialSystem(1, processedOrder);
            var system2 = new TestSerialSystem(2, processedOrder);
            var group = new SerialSystemGroup(system1, system2);

            // Act
            bool removed = group.Remove(system1);
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);
            group.Execute(registry, in context);

            // Assert
            Assert.True(removed);
            Assert.Equal(1, group.Count);
            Assert.Equal(new[] { 2 }, processedOrder);
        }
    }
}
