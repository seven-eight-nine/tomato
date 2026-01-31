using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Tomato.Time;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class OrderedSerialSystemTests
    {
        private class MockArena : IEntityArena
        {
            public bool IsValid(int index, int generation) => true;
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            private readonly List<AnyHandle> _entities = new List<AnyHandle>();

            public void AddEntity(AnyHandle handle) => _entities.Add(handle);

            public IReadOnlyList<AnyHandle> GetAllEntities() => _entities;

            public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class => _entities;
        }

        private class PriorityOrderedSystem : IOrderedSerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> ProcessedIndices { get; } = new List<int>();
            public Dictionary<int, int> Priorities { get; } = new Dictionary<int, int>();

            public void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output)
            {
                // Sort by priority (higher priority first)
                var sorted = input
                    .OrderByDescending(h => Priorities.TryGetValue(h.Index, out var p) ? p : 0)
                    .ToList();
                output.AddRange(sorted);
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                foreach (var entity in entities)
                {
                    ProcessedIndices.Add(entity.Index);
                }
            }
        }

        private class TopologicalOrderSystem : IOrderedSerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> ProcessedIndices { get; } = new List<int>();
            public Dictionary<int, List<int>> Dependencies { get; } = new Dictionary<int, List<int>>();

            public void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output)
            {
                var remaining = new HashSet<int>(input.Select(h => h.Index));
                var handleMap = input.ToDictionary(h => h.Index);

                while (remaining.Count > 0)
                {
                    // Find nodes with no unprocessed dependencies
                    var ready = remaining
                        .Where(idx => !Dependencies.TryGetValue(idx, out var deps) ||
                                      deps.All(d => !remaining.Contains(d)))
                        .ToList();

                    if (ready.Count == 0 && remaining.Count > 0)
                    {
                        // Circular dependency - just add remaining
                        foreach (var idx in remaining)
                        {
                            output.Add(handleMap[idx]);
                        }
                        break;
                    }

                    foreach (var idx in ready.OrderBy(x => x))
                    {
                        output.Add(handleMap[idx]);
                        remaining.Remove(idx);
                    }
                }
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                foreach (var entity in entities)
                {
                    ProcessedIndices.Add(entity.Index);
                }
            }
        }

        [Fact]
        public void OrderedSystem_PriorityOrdering_HighPriorityFirst()
        {
            // Arrange
            var system = new PriorityOrderedSystem();
            system.Priorities[0] = 1;
            system.Priorities[1] = 3;
            system.Priorities[2] = 2;

            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 1, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 2, 0));

            var context = new SystemContext(1, new GameTick(0), default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert - should be ordered by priority: 1(3), 2(2), 0(1)
            Assert.Equal(new[] { 1, 2, 0 }, system.ProcessedIndices);
        }

        [Fact]
        public void OrderedSystem_TopologicalOrdering_DependenciesFirst()
        {
            // Arrange
            var system = new TopologicalOrderSystem();
            // Entity 2 depends on 0 and 1
            // Entity 1 depends on 0
            system.Dependencies[2] = new List<int> { 0, 1 };
            system.Dependencies[1] = new List<int> { 0 };

            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 1, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 2, 0));

            var context = new SystemContext(1, new GameTick(0), default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert - 0 first, then 1, then 2
            Assert.Equal(new[] { 0, 1, 2 }, system.ProcessedIndices);
        }

        [Fact]
        public void OrderedSystem_EmptyInput_HandlesGracefully()
        {
            // Arrange
            var system = new PriorityOrderedSystem();
            var registry = new TestEntityRegistry();
            var context = new SystemContext(1, new GameTick(0), default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Empty(system.ProcessedIndices);
        }

        [Fact]
        public void OrderedSystem_SingleEntity_ProcessesCorrectly()
        {
            // Arrange
            var system = new PriorityOrderedSystem();
            system.Priorities[5] = 10;

            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 5, 0));

            var context = new SystemContext(1, new GameTick(0), default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Single(system.ProcessedIndices);
            Assert.Equal(5, system.ProcessedIndices[0]);
        }

        [Fact]
        public void OrderedSystem_EqualPriorities_MaintainsStableOrder()
        {
            // Arrange
            var system = new PriorityOrderedSystem();
            // All same priority
            system.Priorities[0] = 1;
            system.Priorities[1] = 1;
            system.Priorities[2] = 1;

            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 1, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 2, 0));

            var context = new SystemContext(1, new GameTick(0), default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert - all processed
            Assert.Equal(3, system.ProcessedIndices.Count);
        }

        [Fact]
        public void OrderedSystem_CircularDependency_HandlesGracefully()
        {
            // Arrange
            var system = new TopologicalOrderSystem();
            // Circular: 0 -> 1 -> 2 -> 0
            system.Dependencies[0] = new List<int> { 2 };
            system.Dependencies[1] = new List<int> { 0 };
            system.Dependencies[2] = new List<int> { 1 };

            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 1, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 2, 0));

            var context = new SystemContext(1, new GameTick(0), default);

            // Act - should not throw
            SystemExecutor.Execute(system, registry, in context);

            // Assert - all entities eventually processed
            Assert.Equal(3, system.ProcessedIndices.Count);
        }
    }
}
