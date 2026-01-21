using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.EntitySystem.Context;
using Tomato.EntitySystem.Phases;
using Tomato.EntitySystem.Spawn;
using Tomato.SystemPipeline;
using EHSIEntityArena = Tomato.EntityHandleSystem.IEntityArena;

namespace Tomato.EntitySystem.Tests.Phases;

public enum TestCategory
{
    FullBody,
    Upper
}

#region Test Mocks

public class MockArena : EHSIEntityArena, IEntitySpawner
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

public class MockEntityDespawner : IEntityDespawner
{
    public List<AnyHandle> DespawnedEntities { get; } = new();

    public void Despawn(AnyHandle handle)
    {
        DespawnedEntities.Add(handle);
    }
}

#endregion

public class CleanupSystemTests
{
    private EntityContextRegistry<TestCategory> CreateRegistry() => new EntityContextRegistry<TestCategory>();
    private MockArena CreateArena() => new MockArena();
    private MockEntityDespawner CreateDespawner() => new MockEntityDespawner();
    private SystemContext CreateContext() => new SystemContext(0.016f, 0.016f, 1, CancellationToken.None);

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var despawner = CreateDespawner();

        var system = new CleanupSystem<TestCategory>(registry, despawner);

        Assert.NotNull(system);
        Assert.True(system.IsEnabled);
    }

    [Fact]
    public void Constructor_WithNullRegistry_ShouldThrow()
    {
        var despawner = CreateDespawner();

        Assert.Throws<ArgumentNullException>(() =>
            new CleanupSystem<TestCategory>(null!, despawner));
    }

    [Fact]
    public void Constructor_WithNullDespawner_ShouldThrow()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentNullException>(() =>
            new CleanupSystem<TestCategory>(registry, null!));
    }

    #endregion

    #region ProcessSerial Tests

    [Fact]
    public void ProcessSerial_WithNoMarkedEntities_ShouldNotDespawnAnything()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        var context = CreateContext();

        // Add entities but don't mark them for deletion
        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Empty(despawner.DespawnedEntities);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void ProcessSerial_WithMarkedEntities_ShouldDespawnThem()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        var context = CreateContext();

        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);
        registry.MarkForDeletion(handle1);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Single(despawner.DespawnedEntities);
        Assert.Equal(handle1, despawner.DespawnedEntities[0]);
    }

    [Fact]
    public void ProcessSerial_ShouldRemoveMarkedEntitiesFromRegistry()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        var context = CreateContext();

        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);
        registry.MarkForDeletion(handle1);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.False(registry.Exists(handle1));
        Assert.True(registry.Exists(handle2));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void ProcessSerial_WithMultipleMarkedEntities_ShouldDespawnAll()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        var context = CreateContext();

        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        var handle3 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);
        registry.Register(handle3);
        registry.MarkForDeletion(handle1);
        registry.MarkForDeletion(handle3);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);

        Assert.Equal(2, despawner.DespawnedEntities.Count);
        Assert.Contains(handle1, despawner.DespawnedEntities);
        Assert.Contains(handle3, despawner.DespawnedEntities);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.Exists(handle2));
    }

    [Fact]
    public void ProcessSerial_CalledMultipleTimes_ShouldOnlyProcessOnce()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        var context = CreateContext();

        var handle = arena.Spawn();
        registry.Register(handle);
        registry.MarkForDeletion(handle);

        var entities = registry.GetAllEntities();
        system.ProcessSerial(registry, entities, in context);
        system.ProcessSerial(registry, entities, in context); // Second call

        Assert.Single(despawner.DespawnedEntities);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void ProcessSerial_WhenDisabled_ShouldNotProcess()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var despawner = CreateDespawner();
        var system = new CleanupSystem<TestCategory>(registry, despawner);
        system.IsEnabled = false;
        var context = CreateContext();

        var handle = arena.Spawn();
        registry.Register(handle);
        registry.MarkForDeletion(handle);

        // Use SystemExecutor to respect IsEnabled
        SystemExecutor.Execute(system, registry, in context);

        Assert.Empty(despawner.DespawnedEntities);
        Assert.Equal(1, registry.Count);
    }

    #endregion
}
