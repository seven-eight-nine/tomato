using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.EntitySystem.Context;
using Tomato.EntitySystem.Phases;
using Tomato.EntitySystem.Providers;
using Tomato.EntitySystem.Spawn;
using Tomato.SystemPipeline;
using Tomato.CollisionSystem;
using EHSIEntityArena = Tomato.EntityHandleSystem.IEntityArena;

namespace Tomato.EntitySystem.Tests.Phases;

// VolumeType定数（テスト用）
public static class TestVolumeType
{
    public const int Hitbox = 0;
    public const int Hurtbox = 1;
}

#region Test Mocks for Collision

public class MockCollisionArena : EHSIEntityArena, IEntitySpawner
{
    private readonly HashSet<(int index, int generation)> _validHandles = new();
    private int _nextIndex = 0;

    public VoidHandle Spawn()
    {
        var index = _nextIndex++;
        var generation = 1;
        _validHandles.Add((index, generation));
        return new VoidHandle(this, index, generation);
    }

    public bool Despawn(VoidHandle handle)
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

public class MockCollisionMessageEmitter : ICollisionMessageEmitter
{
    public List<List<CollisionResult>> EmitCalls { get; } = new();

    public void EmitMessages(IReadOnlyList<CollisionResult> results)
    {
        EmitCalls.Add(new List<CollisionResult>(results));
    }
}

public class MockEntityPositionProvider : IEntityPositionProvider
{
    private readonly Dictionary<VoidHandle, Vector3> _positions = new();

    public void SetPosition(VoidHandle handle, Vector3 position)
    {
        _positions[handle] = position;
    }

    public Vector3 GetPosition(VoidHandle handle)
    {
        return _positions.TryGetValue(handle, out var pos) ? pos : Vector3.Zero;
    }
}

#endregion

public class CollisionSystemTests
{
    private EntityContextRegistry<TestCategory> CreateRegistry() => new EntityContextRegistry<TestCategory>();
    private MockCollisionArena CreateArena() => new MockCollisionArena();
    private SystemContext CreateContext() => new SystemContext(0.016f, 0.016f, 1, CancellationToken.None);
    private MockCollisionMessageEmitter CreateEmitter() => new MockCollisionMessageEmitter();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();

        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);

        Assert.NotNull(system);
        Assert.True(system.IsEnabled);
    }

    [Fact]
    public void Constructor_WithNullRegistry_ShouldThrow()
    {
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem<TestCategory>(null!, detector, positionProvider, emitter));
    }

    [Fact]
    public void Constructor_WithNullDetector_ShouldThrow()
    {
        var registry = CreateRegistry();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem<TestCategory>(registry, null!, positionProvider, emitter));
    }

    [Fact]
    public void Constructor_WithNullPositionProvider_ShouldThrow()
    {
        var registry = CreateRegistry();
        var detector = new CollisionDetector();
        var emitter = CreateEmitter();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem<TestCategory>(registry, detector, null!, emitter));
    }

    [Fact]
    public void Constructor_WithNullEmitter_ShouldThrow()
    {
        var registry = CreateRegistry();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new CollisionSystem<TestCategory>(registry, detector, positionProvider, null!));
    }

    #endregion

    #region ProcessSerial Tests

    [Fact]
    public void ProcessSerial_WithNoEntities_ShouldNotThrow()
    {
        var registry = CreateRegistry();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        // Should not throw
        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);
    }

    [Fact]
    public void ProcessSerial_WithInactiveEntity_ShouldSkipIt()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        var handle = arena.Spawn();
        var entityContext = registry.Register(handle);
        entityContext.IsActive = false;

        // Add a collision volume
        var shape = new SphereShape(1.0f);
        var filter = new CollisionFilter(1, 1);
        entityContext.CollisionVolumes.Add(new CollisionVolume(handle, shape, filter, TestVolumeType.Hurtbox));

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        // EmitMessages is called but with empty results (inactive entity not processed)
        Assert.Single(emitter.EmitCalls);
        Assert.Empty(emitter.EmitCalls[0]);
    }

    [Fact]
    public void ProcessSerial_WithActiveEntityAndVolume_ShouldProcess()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        var handle = arena.Spawn();
        var entityContext = registry.Register(handle);
        entityContext.IsActive = true;
        positionProvider.SetPosition(handle, new Vector3(0, 0, 0));

        // Add a collision volume
        var shape = new SphereShape(1.0f);
        var filter = new CollisionFilter(1, 1);
        entityContext.CollisionVolumes.Add(new CollisionVolume(handle, shape, filter, TestVolumeType.Hurtbox));

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        // Should complete without error
        Assert.Single(entityContext.CollisionVolumes);
    }

    [Fact]
    public void ProcessSerial_ShouldTickVolumeLifetimes()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        var handle = arena.Spawn();
        var entityContext = registry.Register(handle);
        entityContext.IsActive = true;
        positionProvider.SetPosition(handle, new Vector3(0, 0, 0));

        // Add a collision volume with 3 frame lifetime
        var shape = new SphereShape(1.0f);
        var filter = new CollisionFilter(1, 1);
        var volume = new CollisionVolume(handle, shape, filter, TestVolumeType.Hitbox, lifetime: 3);
        entityContext.CollisionVolumes.Add(volume);

        Assert.Equal(3, volume.RemainingLifetime);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Equal(2, volume.RemainingLifetime);
    }

    [Fact]
    public void ProcessSerial_ShouldRemoveExpiredVolumes()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        var handle = arena.Spawn();
        var entityContext = registry.Register(handle);
        entityContext.IsActive = true;
        positionProvider.SetPosition(handle, new Vector3(0, 0, 0));

        // Add a collision volume with 1 frame lifetime
        var shape = new SphereShape(1.0f);
        var filter = new CollisionFilter(1, 1);
        var volume = new CollisionVolume(handle, shape, filter, TestVolumeType.Hitbox, lifetime: 1);
        entityContext.CollisionVolumes.Add(volume);

        var entities = registry.GetAllEntities();

        // First process - volume is not yet expired
        system.ProcessSerial(registry, entities, in context);
        Assert.Single(entityContext.CollisionVolumes);

        // Second process - volume should be removed after tick
        system.ProcessSerial(registry, entities, in context);
        Assert.Empty(entityContext.CollisionVolumes);
    }

    [Fact]
    public void ProcessSerial_WithCollidingVolumes_ShouldCallEmitter()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        var context = CreateContext();

        // Entity 1 with hitbox
        var handle1 = arena.Spawn();
        var context1 = registry.Register(handle1);
        context1.IsActive = true;
        positionProvider.SetPosition(handle1, new Vector3(0, 0, 0));

        var shape1 = new SphereShape(1.0f);
        var filter1 = new CollisionFilter(1, 2); // Layer 1, can hit layer 2
        context1.CollisionVolumes.Add(new CollisionVolume(handle1, shape1, filter1, TestVolumeType.Hitbox));

        // Entity 2 with hurtbox at same position (should collide)
        var handle2 = arena.Spawn();
        var context2 = registry.Register(handle2);
        context2.IsActive = true;
        positionProvider.SetPosition(handle2, new Vector3(0, 0, 0));

        var shape2 = new SphereShape(1.0f);
        var filter2 = new CollisionFilter(2, 1); // Layer 2, can hit layer 1
        context2.CollisionVolumes.Add(new CollisionVolume(handle2, shape2, filter2, TestVolumeType.Hurtbox));

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        // Emitter should have been called with collision results
        Assert.Single(emitter.EmitCalls);
        Assert.Single(emitter.EmitCalls[0]); // One collision
    }

    [Fact]
    public void ProcessSerial_WhenDisabled_ShouldNotProcess()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var detector = new CollisionDetector();
        var positionProvider = new MockEntityPositionProvider();
        var emitter = CreateEmitter();
        var system = new CollisionSystem<TestCategory>(registry, detector, positionProvider, emitter);
        system.IsEnabled = false;
        var context = CreateContext();

        var handle = arena.Spawn();
        var entityContext = registry.Register(handle);
        entityContext.IsActive = true;

        // Use SystemExecutor to respect IsEnabled
        SystemExecutor.Execute(system, registry, in context);

        // Emitter should not have been called
        Assert.Empty(emitter.EmitCalls);
    }

    #endregion
}
