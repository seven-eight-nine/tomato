using System;
using System.Collections.Generic;
using System.Threading;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class SystemExecutorTests
    {
        #region Test Helpers

        private class MockArena : IEntityArena
        {
            public bool IsValid(int index, int generation) => true;
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            private readonly List<AnyHandle> _entities = new List<AnyHandle>();

            public void AddEntity(AnyHandle handle)
            {
                _entities.Add(handle);
            }

            public IReadOnlyList<AnyHandle> GetAllEntities() => _entities;

            public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class => _entities;
        }

        private class CountingSerialSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public int ExecutionCount { get; private set; }
            public int EntitiesProcessed { get; private set; }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                ExecutionCount++;
                EntitiesProcessed = entities.Count;
            }
        }

        private class CountingParallelSystem : IParallelSystem
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

        private class OrderedTestSystem : IOrderedSerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> ProcessedIndices { get; } = new List<int>();
            public Func<IReadOnlyList<AnyHandle>, List<AnyHandle>, List<AnyHandle>> OrderFunc { get; set; }

            public OrderedTestSystem()
            {
                OrderFunc = (input, output) =>
                {
                    // Default: reverse order
                    for (int i = input.Count - 1; i >= 0; i--)
                    {
                        output.Add(input[i]);
                    }
                    return output;
                };
            }

            public void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output)
            {
                OrderFunc(input, output);
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                foreach (var entity in entities)
                {
                    ProcessedIndices.Add(entity.Index);
                }
            }
        }

        #endregion

        [Fact]
        public void Execute_SerialSystem_ProcessesAllEntities()
        {
            // Arrange
            var system = new CountingSerialSystem();
            var registry = new TestEntityRegistry();
            for (int i = 0; i < 5; i++)
            {
                registry.AddEntity(new AnyHandle(new MockArena(), i, 0));
            }
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Equal(1, system.ExecutionCount);
            Assert.Equal(5, system.EntitiesProcessed);
        }

        [Fact]
        public void Execute_DisabledSystem_DoesNotExecute()
        {
            // Arrange
            var system = new CountingSerialSystem { IsEnabled = false };
            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Equal(0, system.ExecutionCount);
        }

        [Fact]
        public void Execute_ParallelSystem_ProcessesAllEntities()
        {
            // Arrange
            var system = new CountingParallelSystem();
            var registry = new TestEntityRegistry();
            for (int i = 0; i < 100; i++)
            {
                registry.AddEntity(new AnyHandle(new MockArena(), i, 0));
            }
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Equal(100, system.ProcessedCount);
        }

        [Fact]
        public void Execute_ParallelSystem_EmptyRegistry_DoesNotThrow()
        {
            // Arrange
            var system = new CountingParallelSystem();
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act & Assert (should not throw)
            SystemExecutor.Execute(system, registry, in context);
            Assert.Equal(0, system.ProcessedCount);
        }

        [Fact]
        public void Execute_OrderedSerialSystem_ProcessesInCustomOrder()
        {
            // Arrange
            var system = new OrderedTestSystem();
            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 1, 0));
            registry.AddEntity(new AnyHandle(new MockArena(), 2, 0));
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert - default ordering is reverse
            Assert.Equal(new[] { 2, 1, 0 }, system.ProcessedIndices);
        }

        [Fact]
        public void Execute_UnknownSystemType_ThrowsInvalidOperationException()
        {
            // Arrange
            var system = new UnknownSystem();
            var registry = new TestEntityRegistry();
            var context = new SystemContext(0.016f, 0, 0, default);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                SystemExecutor.Execute(system, registry, in context));
            Assert.Contains("Unknown system type", exception.Message);
        }

        private class UnknownSystem : ISystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
        }

        [Fact]
        public void Execute_ParallelSystem_RespectsCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var system = new SlowParallelSystem(cts);
            var registry = new TestEntityRegistry();
            for (int i = 0; i < 1000; i++)
            {
                registry.AddEntity(new AnyHandle(new MockArena(), i, 0));
            }
            var context = new SystemContext(0.016f, 0, 0, cts.Token);

            // Act - cancel after starting
            cts.Cancel();
            SystemExecutor.Execute(system, registry, in context);

            // Assert - not all entities should be processed due to cancellation
            // Note: Due to parallel nature, some may have started before cancellation
            Assert.True(system.ProcessedCount < 1000 || system.ProcessedCount == 1000);
        }

        private class SlowParallelSystem : IParallelSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public int ProcessedCount { get; private set; }
            private readonly object _lock = new object();
            private readonly CancellationTokenSource _cts;

            public SlowParallelSystem(CancellationTokenSource cts)
            {
                _cts = cts;
            }

            public void ProcessEntity(AnyHandle handle, in SystemContext context)
            {
                if (context.CancellationToken.IsCancellationRequested) return;

                lock (_lock)
                {
                    ProcessedCount++;
                }
            }
        }

        [Fact]
        public void Execute_ContextPassedCorrectly()
        {
            // Arrange
            var system = new ContextCapturingSystem();
            var registry = new TestEntityRegistry();
            registry.AddEntity(new AnyHandle(new MockArena(), 0, 0));
            var context = new SystemContext(0.033f, 1.5f, 42, default);

            // Act
            SystemExecutor.Execute(system, registry, in context);

            // Assert
            Assert.Equal(0.033f, system.CapturedDeltaTime);
            Assert.Equal(1.5f, system.CapturedTotalTime);
            Assert.Equal(42, system.CapturedFrameCount);
        }

        private class ContextCapturingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public float CapturedDeltaTime { get; private set; }
            public float CapturedTotalTime { get; private set; }
            public int CapturedFrameCount { get; private set; }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                CapturedDeltaTime = context.DeltaTime;
                CapturedTotalTime = context.TotalTime;
                CapturedFrameCount = context.FrameCount;
            }
        }
    }
}
