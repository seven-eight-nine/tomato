using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class PipelineTests
    {
        private class TestSerialSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<float> RecordedDeltaTimes { get; } = new List<float>();
            public List<float> RecordedTotalTimes { get; } = new List<float>();
            public List<int> RecordedFrameCounts { get; } = new List<int>();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                RecordedDeltaTimes.Add(context.DeltaTime);
                RecordedTotalTimes.Add(context.TotalTime);
                RecordedFrameCounts.Add(context.FrameCount);
            }
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            public IReadOnlyList<VoidHandle> GetAllEntities()
            {
                return new List<VoidHandle>();
            }

            public IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class
            {
                return new List<VoidHandle>();
            }
        }

        [Fact]
        public void Pipeline_TracksTime_Correctly()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.017f);
            pipeline.Execute(group, 0.015f);

            // Assert
            Assert.Equal(new[] { 0.016f, 0.017f, 0.015f }, system.RecordedDeltaTimes);
            Assert.Equal(3, system.RecordedFrameCounts[2]);
        }

        [Fact]
        public void Pipeline_AccumulatesTotalTime()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 1.0f);
            pipeline.Execute(group, 2.0f);
            pipeline.Execute(group, 3.0f);

            // Assert
            Assert.Equal(1.0f, system.RecordedTotalTimes[0], 5);
            Assert.Equal(3.0f, system.RecordedTotalTimes[1], 5);
            Assert.Equal(6.0f, system.RecordedTotalTimes[2], 5);
        }

        [Fact]
        public void Pipeline_IncrementsFrameCount()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.016f);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, system.RecordedFrameCounts);
        }

        [Fact]
        public void Pipeline_Reset_ClearsTimeAndFrameCount()
        {
            // Arrange
            var system = new TestSerialSystem();
            var group = new SerialSystemGroup(system);
            var registry = new TestEntityRegistry();
            var pipeline = new Pipeline(registry);

            // Execute a few frames
            pipeline.Execute(group, 1.0f);
            pipeline.Execute(group, 1.0f);

            // Act
            pipeline.Reset();
            pipeline.Execute(group, 0.5f);

            // Assert
            Assert.Equal(0.5f, pipeline.TotalTime, 5);
            Assert.Equal(1, pipeline.FrameCount);
            Assert.Equal(0.5f, system.RecordedTotalTimes[2], 5);
            Assert.Equal(1, system.RecordedFrameCounts[2]);
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
            pipeline.Execute(updateGroup, 0.016f);
            pipeline.Execute(lateUpdateGroup, 0.016f);

            // Assert
            Assert.Single(updateSystem.RecordedDeltaTimes);
            Assert.Single(lateUpdateSystem.RecordedDeltaTimes);
            Assert.Equal(2, pipeline.FrameCount);
        }
    }
}
