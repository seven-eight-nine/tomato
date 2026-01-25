using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Collision;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Phases;
using Tomato.GameLoop.Providers;
using Tomato.GameLoop.Spawn;
using Tomato.SystemPipeline;
using Tomato.Math;
using EHSIEntityArena = Tomato.EntityHandleSystem.IEntityArena;

namespace Tomato.GameLoop.Tests.Phases;

#region Test Mocks for Collision

public class MockCollisionArena : EHSIEntityArena, IEntitySpawner
{
    private readonly HashSet<(int index, int generation)> _validHandles = new();
    private int _nextIndex = 0;

    public AnyHandle Spawn()
    {
        var index = _nextIndex++;
        var generation = 1;
        _validHandles.Add((index, generation));
        return new AnyHandle(this, index, generation);
    }

    public bool Despawn(AnyHandle handle)
    {
        var key = (handle.Index, handle.Generation);
        if (_validHandles.Contains(key))
        {
            _validHandles.Remove(key);
            return true;
        }
        return false;
    }

    public bool IsValid(int index, int generation)
    {
        return _validHandles.Contains((index, generation));
    }
}

public class MockCollisionSource : ICollisionSource
{
    private readonly List<CollisionPair> _collisions = new();

    public void AddCollision(int entityIdA, int entityIdB, Vector3 point, Vector3 normal)
    {
        _collisions.Add(new CollisionPair(entityIdA, entityIdB, point, normal));
    }

    public IReadOnlyList<CollisionPair> GetCollisions() => _collisions;

    public void Clear() => _collisions.Clear();
}

public class MockCollisionMessageEmitter : ICollisionMessageEmitter
{
    public List<List<CollisionPair>> EmitCalls { get; } = new();

    public void EmitMessages(IReadOnlyList<CollisionPair> collisions)
    {
        EmitCalls.Add(new List<CollisionPair>(collisions));
    }
}

#endregion

public class CollisionSystemTests
{
    private EntityContextRegistry<TestCategory> CreateRegistry() => new EntityContextRegistry<TestCategory>();
    private SystemContext CreateContext() => new SystemContext(0.016f, 0.016f, 1, CancellationToken.None);
    private MockCollisionSource CreateSource() => new MockCollisionSource();
    private MockCollisionMessageEmitter CreateEmitter() => new MockCollisionMessageEmitter();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        var source = CreateSource();
        var emitter = CreateEmitter();

        var system = new CollisionSystem(source, emitter);

        Assert.NotNull(system);
        Assert.True(system.IsEnabled);
    }

    [Fact]
    public void Constructor_WithNullSource_ShouldThrow()
    {
        var emitter = CreateEmitter();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem(null!, emitter));
    }

    [Fact]
    public void Constructor_WithNullEmitter_ShouldThrow()
    {
        var source = CreateSource();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem(source, null!));
    }

    #endregion

    #region ProcessSerial Tests

    [Fact]
    public void ProcessSerial_WithNoCollisions_ShouldCallEmitterWithEmptyList()
    {
        var registry = CreateRegistry();
        var source = CreateSource();
        var emitter = CreateEmitter();
        var system = new CollisionSystem(source, emitter);
        var context = CreateContext();

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Single(emitter.EmitCalls);
        Assert.Empty(emitter.EmitCalls[0]);
    }

    [Fact]
    public void ProcessSerial_WithCollisions_ShouldPassToEmitter()
    {
        var registry = CreateRegistry();
        var source = CreateSource();
        var emitter = CreateEmitter();
        var system = new CollisionSystem(source, emitter);
        var context = CreateContext();

        // Add some collisions
        source.AddCollision(1, 2, Vector3.Zero, Vector3.UnitY);
        source.AddCollision(3, 4, new Vector3(1, 0, 0), Vector3.UnitX);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Single(emitter.EmitCalls);
        Assert.Equal(2, emitter.EmitCalls[0].Count);

        var collision1 = emitter.EmitCalls[0][0];
        Assert.Equal(1, collision1.EntityIdA);
        Assert.Equal(2, collision1.EntityIdB);

        var collision2 = emitter.EmitCalls[0][1];
        Assert.Equal(3, collision2.EntityIdA);
        Assert.Equal(4, collision2.EntityIdB);
    }

    [Fact]
    public void ProcessSerial_ShouldClearSourceAfterProcessing()
    {
        var registry = CreateRegistry();
        var source = CreateSource();
        var emitter = CreateEmitter();
        var system = new CollisionSystem(source, emitter);
        var context = CreateContext();

        source.AddCollision(1, 2, Vector3.Zero, Vector3.UnitY);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        // Source should be cleared
        Assert.Empty(source.GetCollisions());
    }

    [Fact]
    public void ProcessSerial_CalledTwice_ShouldClearBetweenCalls()
    {
        var registry = CreateRegistry();
        var source = CreateSource();
        var emitter = CreateEmitter();
        var system = new CollisionSystem(source, emitter);
        var context = CreateContext();

        // First call with collision
        source.AddCollision(1, 2, Vector3.Zero, Vector3.UnitY);
        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        // Second call without adding new collision
        system.ProcessSerial(registry, entities, in context);

        Assert.Equal(2, emitter.EmitCalls.Count);
        Assert.Single(emitter.EmitCalls[0]);  // First call had 1 collision
        Assert.Empty(emitter.EmitCalls[1]);    // Second call had 0 (cleared)
    }

    [Fact]
    public void ProcessSerial_WhenDisabled_ShouldNotProcess()
    {
        var registry = CreateRegistry();
        var source = CreateSource();
        var emitter = CreateEmitter();
        var system = new CollisionSystem(source, emitter);
        system.IsEnabled = false;
        var context = CreateContext();

        source.AddCollision(1, 2, Vector3.Zero, Vector3.UnitY);

        // Use SystemExecutor to respect IsEnabled
        SystemExecutor.Execute(system, registry, in context);

        // Emitter should not have been called
        Assert.Empty(emitter.EmitCalls);
    }

    #endregion
}
