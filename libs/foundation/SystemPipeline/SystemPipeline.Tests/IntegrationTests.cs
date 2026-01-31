using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Tomato.Time;
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
            public float VelocityX { get; set; } // units per tick
            public float VelocityY { get; set; } // units per tick
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
            private readonly List<AnyHandle> _handles = new List<AnyHandle>();

            public GameEntity AddEntity(int id, float health = 100f)
            {
                var entity = new GameEntity { Id = id, Health = health };
                _arena.AddEntity(entity);
                var handle = new AnyHandle(_arena, _handles.Count, 0);
                _handles.Add(handle);
                return entity;
            }

            public GameEntity GetEntity(AnyHandle handle) => _arena.GetEntity(handle.Index);

            public IReadOnlyList<AnyHandle> GetAllEntities() => _handles;

            public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class => _handles;
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

            public void ProcessEntity(AnyHandle handle, in SystemContext context)
            {
                var entity = _registry.GetEntity(handle);
                if (entity == null || !entity.IsAlive) return;

                // Velocity is now units per tick
                entity.X += entity.VelocityX * context.DeltaTicks;
                entity.Y += entity.VelocityY * context.DeltaTicks;
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

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
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

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
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
            player.VelocityX = 10f; // 10 units per tick
            player.VelocityY = 5f;  // 5 units per tick

            var enemy = registry.AddEntity(2, 50f);
            enemy.VelocityX = -5f;

            var movementSystem = new MovementSystem(registry);
            var damageSystem = new DamageSystem(registry);
            var cleanupSystem = new CleanupSystem(registry);

            var updateGroup = new SerialSystemGroup(movementSystem, damageSystem);
            var lateUpdateGroup = new SerialSystemGroup(cleanupSystem);

            var pipeline = new Pipeline(registry);

            // Act - Simulate frame 1 (1 tick)
            pipeline.Execute(updateGroup, 1);
            pipeline.Execute(lateUpdateGroup, 1);

            // Assert - Movement applied (1 tick)
            Assert.Equal(10f, player.X, 2);
            Assert.Equal(5f, player.Y, 2);
            Assert.Equal(-5f, enemy.X, 2);
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

            var updateGroup = new SerialSystemGroup(damageSystem);
            var lateUpdateGroup = new SerialSystemGroup(cleanupSystem);

            var pipeline = new Pipeline(registry);

            // Act - Deal damage to enemy
            damageSystem.DamageQueue.Enqueue((2, 35f));
            pipeline.Execute(updateGroup, 1);
            pipeline.Execute(lateUpdateGroup, 1);

            // Assert
            Assert.True(player.IsAlive);
            Assert.False(enemy.IsAlive);
            Assert.Equal(0f, enemy.Health);
            Assert.Contains(2, cleanupSystem.CleanedUpIds);
        }

        [Fact]
        public void GameLoop_MultipleFrames_AccumulatesTicks()
        {
            // Arrange
            var registry = new GameEntityRegistry();
            var entity = registry.AddEntity(1, 100f);
            entity.VelocityX = 10f; // 10 units per tick

            var movementSystem = new MovementSystem(registry);
            var group = new SerialSystemGroup(movementSystem);
            var pipeline = new Pipeline(registry);

            // Act - Simulate 10 frames at 1 tick each
            for (int i = 0; i < 10; i++)
            {
                pipeline.Execute(group, 1);
            }

            // Assert
            Assert.Equal(10L, pipeline.CurrentTick.Value);
            Assert.Equal(100f, entity.X, 1); // 10 * 10 = 100
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

            var updateGroup = new SerialSystemGroup(system1, system2);
            var lateUpdateGroup = new SerialSystemGroup(system3, system4);

            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(updateGroup, 1);
            pipeline.Execute(lateUpdateGroup, 1);

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

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
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
            entity.VelocityX = 10f; // 10 units per tick

            var movementSystem = new MovementSystem(registry);
            var group = new SerialSystemGroup(movementSystem);
            var pipeline = new Pipeline(registry);

            // Act - Frame 1: enabled
            pipeline.Execute(group, 1);
            var posAfterFrame1 = entity.X;

            // Disable movement
            movementSystem.IsEnabled = false;

            // Frame 2: disabled
            pipeline.Execute(group, 1);
            var posAfterFrame2 = entity.X;

            // Re-enable
            movementSystem.IsEnabled = true;

            // Frame 3: enabled again
            pipeline.Execute(group, 1);
            var posAfterFrame3 = entity.X;

            // Assert
            Assert.Equal(10f, posAfterFrame1, 1);
            Assert.Equal(10f, posAfterFrame2, 1); // No change
            Assert.Equal(20f, posAfterFrame3, 1);
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

            var group = new SerialSystemGroup(movementSystem, damageSystem, cleanupSystem);
            var pipeline = new Pipeline(registry);

            // Act
            damageSystem.DamageQueue.Enqueue((50, 150f)); // Kill entity 50
            pipeline.Execute(group, 1);

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

            var trackingSystem = new TickTrackingSystem();
            var group = new SerialSystemGroup(trackingSystem);
            var pipeline = new Pipeline(registry);

            // Act
            pipeline.Execute(group, 1);
            pipeline.Execute(group, 1);
            pipeline.Execute(group, 1);

            var ticksBefore = trackingSystem.CurrentTicks.ToList();

            pipeline.Reset();

            pipeline.Execute(group, 1);
            pipeline.Execute(group, 1);

            // Assert
            Assert.Equal(new long[] { 1, 2, 3 }, ticksBefore);
            Assert.Equal(new long[] { 1, 2, 3, 1, 2 }, trackingSystem.CurrentTicks);
        }

        private class TickTrackingSystem : ISerialSystem
        {
            public bool IsEnabled { get; set; } = true;
            public IEntityQuery Query => null;
            public List<long> CurrentTicks { get; } = new List<long>();

            public void ProcessSerial(IEntityRegistry registry, IReadOnlyList<AnyHandle> entities, in SystemContext context)
            {
                CurrentTicks.Add(context.CurrentTick.Value);
            }
        }
    }
}
