using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    /// <summary>
    /// Integration tests that verify complete pipeline workflows
    /// simulating real-world game loop scenarios.
    /// </summary>
    public class IntegrationTests
    {
        #region Test Infrastructure

        private class GameEntity
        {
            public int Id { get; set; }
            public float Health { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float VelocityX { get; set; }
            public float VelocityY { get; set; }
            public bool IsAlive { get; set; } = true;
        }

        private class GameEntityArena : IEntityArena
        {
            private readonly List<GameEntity> _entities = new List<GameEntity>();

            public GameEntity AddEntity(GameEntity entity)
            {
                _entities.Add(entity);
                return entity;
            }

            public GameEntity GetEntity(int index)
            {
                if (index >= 0 && index < _entities.Count)
                    return _entities[index];
                return null;
            }

            public bool IsValid(int index, int generation) => index >= 0 && index < _entities.Count;
        }

        private class GameEntityRegistry : IEntityRegistry
        {
            private readonly GameEntityArena _arena = new GameEntityArena();
            private readonly List<VoidHandle> _handles = new List<VoidHandle>();

            public GameEntity AddEntity(int id, float health = 100f)
            {
                var entity = new GameEntity { Id = id, Health = health };
                _arena.AddEntity(entity);
                var handle = new VoidHandle(_arena, _handles.Count, 0);
                _handles.Add(handle);
                return entity;
            }

            public GameEntity GetEntity(VoidHandle handle) => _arena.GetEntity(handle.Index);

            public IReadOnlyList<VoidHandle> GetAllEntities() => _handles;

            public IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class => _handles;
        }

        private class MovementSystem : IParallelSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            private readonly GameEntityRegistry _registry;

            public MovementSystem(GameEntityRegistry registry)
            {
                _registry = registry;
            }

            public void ProcessEntity(VoidHandle handle, in SystemContext context)
            {
                var entity = _registry.GetEntity(handle);
                if (entity == null || !entity.IsAlive) return;

                entity.X += entity.VelocityX * context.DeltaTime;
                entity.Y += entity.VelocityY * context.DeltaTime;
            }
        }

        private class DamageSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            private readonly GameEntityRegistry _registry;
            public Queue<(int TargetId, float Damage)> DamageQueue { get; } = new Queue<(int, float)>();

            public DamageSystem(GameEntityRegistry registry)
            {
                _registry = registry;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                while (DamageQueue.Count > 0)
                {
                    var (targetId, damage) = DamageQueue.Dequeue();
                    foreach (var handle in entities)
                    {
                        var entity = _registry.GetEntity(handle);
                        if (entity != null && entity.Id == targetId)
                        {
                            entity.Health -= damage;
                            if (entity.Health <= 0)
                            {
                                entity.Health = 0;
                                entity.IsAlive = false;
                            }
                            break;
                        }
                    }
                }
            }
        }

        private class CleanupSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            private readonly GameEntityRegistry _registry;
            public List<int> CleanedUpIds { get; } = new List<int>();

            public CleanupSystem(GameEntityRegistry registry)
            {
                _registry = registry;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                foreach (var handle in entities)
                {
                    var entity = _registry.GetEntity(handle);
                    if (entity != null && !entity.IsAlive)
                    {
                        CleanedUpIds.Add(entity.Id);
                    }
                }
            }
        }

        #endregion

        [Fact]
        public void GameLoop_MovementAndDamage_ProcessesCorrectly()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            var player = registry.AddEntity(1, 100f);
            player.VelocityX = 10f;
            player.VelocityY = 5f;

            var enemy = registry.AddEntity(2, 50f);
            enemy.VelocityX = -5f;

            var movementSystem = new MovementSystem(registry);
            var damageSystem = new DamageSystem(registry);
            var cleanupSystem = new CleanupSystem(registry);

            var updateGroup = new SystemGroup(movementSystem, damageSystem);
            var lateUpdateGroup = new SystemGroup(cleanupSystem);

            var pipeline = new Pipeline(registry);

            // Act - Simulate frame 1
            pipeline.Execute(updateGroup, 0.016f);
            pipeline.Execute(lateUpdateGroup, 0.016f);

            // Assert - Movement applied
            Assert.Equal(0.16f, player.X, 2);
            Assert.Equal(0.08f, player.Y, 2);
            Assert.Equal(-0.08f, enemy.X, 2);
        }

        [Fact]
        public void GameLoop_DamageAndDeath_ProcessesCorrectly()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            var player = registry.AddEntity(1, 100f);
            var enemy = registry.AddEntity(2, 30f);

            var damageSystem = new DamageSystem(registry);
            var cleanupSystem = new CleanupSystem(registry);

            var updateGroup = new SystemGroup(damageSystem);
            var lateUpdateGroup = new SystemGroup(cleanupSystem);

            var pipeline = new Pipeline(registry);

            // Act - Deal damage to enemy
            damageSystem.DamageQueue.Enqueue((2, 35f));
            pipeline.Execute(updateGroup, 0.016f);
            pipeline.Execute(lateUpdateGroup, 0.016f);

            // Assert
            Assert.True(player.IsAlive);
            Assert.False(enemy.IsAlive);
            Assert.Equal(0f, enemy.Health);
            Assert.Contains(2, cleanupSystem.CleanedUpIds);
        }

        [Fact]
        public void GameLoop_MultipleFrames_AccumulatesTime()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            var entity = registry.AddEntity(1, 100f);
            entity.VelocityX = 100f;

            var movementSystem = new MovementSystem(registry);
            var group = new SystemGroup(movementSystem);
            var pipeline = new Pipeline(registry);

            // Act - Simulate 10 frames at 60fps
            for (int i = 0; i < 10; i++)
            {
                pipeline.Execute(group, 0.016f);
            }

            // Assert
            Assert.Equal(10, pipeline.FrameCount);
            Assert.Equal(0.16f, pipeline.TotalTime, 2);
            Assert.Equal(16f, entity.X, 1); // 100 * 0.16
        }

        [Fact]
        public void GameLoop_MultipleGroups_ExecuteIndependently()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            registry.AddEntity(1, 100f);

            var executionOrder = new List<string>();

            var system1 = new TrackingSystem("Update1", executionOrder);
            var system2 = new TrackingSystem("Update2", executionOrder);
            var system3 = new TrackingSystem("LateUpdate1", executionOrder);
            var system4 = new TrackingSystem("LateUpdate2", executionOrder);

            var updateGroup = new SystemGroup(system1, system2);
            var lateUpdateGroup = new SystemGroup(system3, system4);

            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(updateGroup, 0.016f);
            pipeline.Execute(lateUpdateGroup, 0.016f);

            // Assert
            Assert.Equal(new[] { "Update1", "Update2", "LateUpdate1", "LateUpdate2" }, executionOrder);
        }

        private class TrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            private readonly string _name;
            private readonly List<string> _executionOrder;

            public TrackingSystem(string name, List<string> executionOrder)
            {
                _name = name;
                _executionOrder = executionOrder;
            }

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                _executionOrder.Add(_name);
            }
        }

        [Fact]
        public void GameLoop_SystemToggling_Works()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            var entity = registry.AddEntity(1, 100f);
            entity.VelocityX = 100f;

            var movementSystem = new MovementSystem(registry);
            var group = new SystemGroup(movementSystem);
            var pipeline = new Pipeline(registry);

            // Act - Frame 1: enabled
            pipeline.Execute(group, 0.016f);
            var posAfterFrame1 = entity.X;

            // Disable movement
            movementSystem.IsEnabled = false;

            // Frame 2: disabled
            pipeline.Execute(group, 0.016f);
            var posAfterFrame2 = entity.X;

            // Re-enable
            movementSystem.IsEnabled = true;

            // Frame 3: enabled again
            pipeline.Execute(group, 0.016f);
            var posAfterFrame3 = entity.X;

            // Assert
            Assert.Equal(1.6f, posAfterFrame1, 1);
            Assert.Equal(1.6f, posAfterFrame2, 1); // No change
            Assert.Equal(3.2f, posAfterFrame3, 1);
        }

        [Fact]
        public void GameLoop_ParallelAndSerialMixed_ExecutesCorrectly()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            for (int i = 0; i < 100; i++)
            {
                var entity = registry.AddEntity(i, 100f);
                entity.VelocityX = i;
            }

            var movementSystem = new MovementSystem(registry); // Parallel
            var damageSystem = new DamageSystem(registry); // Serial
            var cleanupSystem = new CleanupSystem(registry); // Serial

            var group = new SystemGroup(movementSystem, damageSystem, cleanupSystem);
            var pipeline = new Pipeline(registry);

            // Act
            damageSystem.DamageQueue.Enqueue((50, 150f)); // Kill entity 50
            pipeline.Execute(group, 0.016f);

            // Assert
            var entities = registry.GetAllEntities();
            var entity50 = registry.GetEntity(entities[50]);
            Assert.False(entity50.IsAlive);
            Assert.Contains(50, cleanupSystem.CleanedUpIds);
        }

        [Fact]
        public void Pipeline_Reset_StartsNewSession()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            registry.AddEntity(1, 100f);

            var trackingSystem = new FrameTrackingSystem();
            var group = new SystemGroup(trackingSystem);
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.016f);

            var framesBefore = trackingSystem.FrameCounts.ToList();

            pipeline.Reset();

            pipeline.Execute(group, 0.016f);
            pipeline.Execute(group, 0.016f);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, framesBefore);
            Assert.Equal(new[] { 1, 2, 3, 1, 2 }, trackingSystem.FrameCounts);
        }

        private class FrameTrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<int> FrameCounts { get; } = new List<int>();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<VoidHandle> entities, in SystemContext context)
            {
                FrameCounts.Add(context.FrameCount);
            }
        }
    }
}
