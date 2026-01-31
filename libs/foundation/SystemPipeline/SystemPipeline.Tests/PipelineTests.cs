using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Tomato.Time;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class PipelineTests
    {
        private class TestSerialSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> RecordedDeltaTicks { get; } = new List<int>();
            public List<long> RecordedCurrentTicks { get; } = new List<long>();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                RecordedDeltaTicks.Add(context.DeltaTicks);
                RecordedCurrentTicks.Add(context.CurrentTick.Value);
            }
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            public IReadOnlyList<AnyHandle> GetAllEntities()
            {
                return new List<AnyHandle>();
            }

            public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class
            {
                return new List<AnyHandle>();
            }
        }

        [Fact]
        public void Pipeline_TracksTicks_Correctly()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 1);
            pipeline.Execute(group, 2);
            pipeline.Execute(group, 3);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, system.RecordedDeltaTicks);
            Assert.Equal(6, system.RecordedCurrentTicks[2]);
        }

        [Fact]
        public void Pipeline_AccumulatesCurrentTick()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 10);
            pipeline.Execute(group, 20);
            pipeline.Execute(group, 30);

            // Assert
            Assert.Equal(10L, system.RecordedCurrentTicks[0]);
            Assert.Equal(30L, system.RecordedCurrentTicks[1]);
            Assert.Equal(60L, system.RecordedCurrentTicks[2]);
        }

        [Fact]
        public void Pipeline_IncrementsCurrentTick()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 1);
            pipeline.Execute(group, 1);
            pipeline.Execute(group, 1);

            // Assert
            Assert.Equal(new long[] { 1, 2, 3 }, system.RecordedCurrentTicks);
        }

        [Fact]
        public void Pipeline_Reset_ClearsCurrentTick()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Execute a few frames
            pipeline.Execute(group, 10);
            pipeline.Execute(group, 10);

            // Act
            pipeline.Reset();
            pipeline.Execute(group, 5);

            // Assert
            Assert.Equal(5L, pipeline.CurrentTick.Value);
            Assert.Equal(5L, system.RecordedCurrentTicks[2]);
        }

        [Fact]
        public void Pipeline_CanExecuteMultipleGroups()
        {
            // Arrange
            var updateSystem = new TestSerialSystem();
            var lateUpdateSystem = new TestSerialSystem();
            var updateGroup = new SerialSystemGroup(updateSystem);
            var lateUpdateGroup = new SerialSystemGroup(lateUpdateSystem);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act - Simulate Unity's Update/LateUpdate pattern
            pipeline.Execute(updateGroup, 1);
            pipeline.Execute(lateUpdateGroup, 1);

            // Assert
            Assert.Single(updateSystem.RecordedDeltaTicks);
            Assert.Single(lateUpdateSystem.RecordedDeltaTicks);
            Assert.Equal(2L, pipeline.CurrentTick.Value);
        }
    }
}
