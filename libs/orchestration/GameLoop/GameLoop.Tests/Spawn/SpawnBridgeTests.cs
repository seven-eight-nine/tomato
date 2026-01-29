using System;
using System.Collections.Generic;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Spawn;
using Tomato.UnitLODSystem;
using EHSIEntityArena = Tomato.EntityHandleSystem.IEntityArena;

namespace Tomato.GameLoop.Tests.Spawn;

public enum TestActionCategory
{
    FullBody,
    Upper,
    Lower
}

#region Test Mocks

/// <summary>
/// テスト用のモックArena
/// </summary>
public class MockArena : EHSIEntityArena, IEntitySpawner
{
    private readonly HashSet<(int index, int generation)> _validHandles = new();
    private int _nextIndex = 0;
    public List<AnyHandle> SpawnedHandles { get; } = new();
    public List<AnyHandle> DespawnedHandles { get; } = new();

    public AnyHandle Spawn()
    {
        var index = _nextIndex++;
        var generation = 1;
        _validHandles.Add((index, generation));
        var handle = new AnyHandle(this, index, generation);
        SpawnedHandles.Add(handle);
        return handle;
    }

    public bool Despawn(AnyHandle handle)
    {
        var key = (handle.Index, handle.Generation);
        if (_validHandles.Contains(key))
        {
            _validHandles.Remove(key);
            DespawnedHandles.Add(handle);
            return true;
        }
        return false;
    }

    public bool IsValid(int index, int generation)
    {
        return _validHandles.Contains((index, generation));
    }
}

/// <summary>
/// テスト用のモックEntityInitializer
/// </summary>
public class MockEntityInitializer : IEntityInitializer<TestActionCategory>
{
    public List<(EntityContext<TestActionCategory> context, Unit unit, object? dataResource)> InitializeCalls { get; } = new();

    public void Initialize(EntityContext<TestActionCategory> context, Unit unit, object? dataResource)
    {
        InitializeCalls.Add((context, unit, dataResource));
    }
}

/// <summary>
/// テスト用のモックUnitDetail
/// </summary>
public class MockUnitDetail : IUnitDetail<Unit>
{
    public UnitPhase Phase { get; private set; } = UnitPhase.None;
    private int _tickCount;

    public void OnUpdatePhase(Unit owner, UnitPhase phase)
    {
        switch (Phase)
        {
            case UnitPhase.Loading:
                Phase = UnitPhase.Loaded;
                break;
            case UnitPhase.Creating:
                Phase = UnitPhase.Ready;
                break;
            case UnitPhase.Unloading:
                if (++_tickCount >= 1)
                    Phase = UnitPhase.Unloaded;
                break;
        }
    }

    public void OnChangePhase(Unit owner, UnitPhase prev, UnitPhase next)
    {
        Phase = next;
        _tickCount = 0;
    }

    public void Dispose() { }
}

#endregion

public class SpawnBridgeTests
{
    private MockArena CreateArena() => new MockArena();
    private EntityContextRegistry<TestActionCategory> CreateRegistry() => new EntityContextRegistry<TestActionCategory>();
    private MockEntityInitializer CreateInitializer() => new MockEntityInitializer();

    private Unit CreateUnit()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetail>(1);
        return unit;
    }

    private Unit CreateReadyUnit()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetail>(1);
        unit.RequestState(1);
        // Tick until stable
        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
            if (unit.IsStable) break;
        }
        return unit;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();

        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        Assert.NotNull(bridge);
    }

    [Fact]
    public void Constructor_WithNullRegistry_ShouldThrow()
    {
        var arena = CreateArena();
        var initializer = CreateInitializer();

        Assert.Throws<ArgumentNullException>(() =>
            new SpawnBridge<TestActionCategory>(null!, arena, initializer));
    }

    [Fact]
    public void Constructor_WithNullArena_ShouldThrow()
    {
        var registry = CreateRegistry();
        var initializer = CreateInitializer();

        Assert.Throws<ArgumentNullException>(() =>
            new SpawnBridge<TestActionCategory>(registry, null!, initializer));
    }

    [Fact]
    public void Constructor_WithNullInitializer_ShouldThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();

        Assert.Throws<ArgumentNullException>(() =>
            new SpawnBridge<TestActionCategory>(registry, arena, null!));
    }

    #endregion

    #region Connect/Disconnect Tests

    [Fact]
    public void Connect_WithValidUnit_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        // Should not throw
        bridge.Connect(unit);
    }

    [Fact]
    public void Connect_WithNullUnit_ShouldThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        Assert.Throws<ArgumentNullException>(() => bridge.Connect(null!));
    }

    [Fact]
    public void Disconnect_WithValidUnit_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();
        bridge.Connect(unit);

        // Should not throw
        bridge.Disconnect(unit);
    }

    [Fact]
    public void Disconnect_WithNullUnit_ShouldThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        Assert.Throws<ArgumentNullException>(() => bridge.Disconnect(null!));
    }

    #endregion

    #region OnUnitReady Tests

    [Fact]
    public void OnUnitReady_ShouldSpawnEntity()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);

        Assert.Equal(1, arena.SpawnedHandles.Count);
    }

    [Fact]
    public void OnUnitReady_ShouldRegisterContext()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);

        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void OnUnitReady_ShouldCallInitializer()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);

        Assert.Single(initializer.InitializeCalls);
        Assert.Same(unit, initializer.InitializeCalls[0].unit);
    }

    [Fact]
    public void OnUnitReady_ShouldSetContextUnit()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);

        var context = bridge.GetContext(unit);
        Assert.NotNull(context);
        Assert.Same(unit, context!.Unit);
    }

    [Fact]
    public void OnUnitReady_ShouldSetContextActive()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);

        var context = bridge.GetContext(unit);
        Assert.NotNull(context);
        Assert.True(context!.IsActive);
    }

    [Fact]
    public void OnUnitReady_CalledTwice_ShouldReactivate()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        var context = bridge.GetContext(unit);
        context!.IsActive = false; // Deactivate

        bridge.OnUnitReady(unit);

        Assert.True(context.IsActive);
        Assert.Equal(1, arena.SpawnedHandles.Count); // Should not spawn again
    }

    #endregion

    #region OnUnitUnloading Tests

    [Fact]
    public void OnUnitUnloading_ShouldSetContextInactive()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        bridge.OnUnitUnloading(unit);

        var context = bridge.GetContext(unit);
        Assert.NotNull(context);
        Assert.False(context!.IsActive);
    }

    [Fact]
    public void OnUnitUnloading_WithUnknownUnit_ShouldNotThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        // Should not throw
        bridge.OnUnitUnloading(unit);
    }

    #endregion

    #region OnUnitRemoved Tests

    [Fact]
    public void OnUnitRemoved_ShouldMarkForDeletion()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        bridge.OnUnitRemoved(unit);

        var markedForDeletion = registry.GetMarkedForDeletion();
        Assert.Single(markedForDeletion);
    }

    [Fact]
    public void OnUnitRemoved_ShouldRemoveMapping()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        bridge.OnUnitRemoved(unit);

        Assert.Null(bridge.GetHandle(unit));
    }

    [Fact]
    public void OnUnitRemoved_WithUnknownUnit_ShouldNotThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        // Should not throw
        bridge.OnUnitRemoved(unit);
    }

    #endregion

    #region GetHandle/GetContext Tests

    [Fact]
    public void GetHandle_WithKnownUnit_ShouldReturnHandle()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        var handle = bridge.GetHandle(unit);

        Assert.NotNull(handle);
    }

    [Fact]
    public void GetHandle_WithUnknownUnit_ShouldReturnNull()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        var handle = bridge.GetHandle(unit);

        Assert.Null(handle);
    }

    [Fact]
    public void GetContext_WithKnownUnit_ShouldReturnContext()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateReadyUnit();

        bridge.OnUnitReady(unit);
        var context = bridge.GetContext(unit);

        Assert.NotNull(context);
    }

    [Fact]
    public void GetContext_WithUnknownUnit_ShouldReturnNull()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        var context = bridge.GetContext(unit);

        Assert.Null(context);
    }

    #endregion

    #region UpdateUnit Integration Tests

    [Fact]
    public void UpdateUnit_WhenUnitBecomesStable_ShouldAutoRegister()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        bridge.Connect(unit);
        unit.RequestState(1);

        // Tick and update until stable
        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
            bridge.UpdateUnit(unit);
            if (unit.IsStable) break;
        }

        Assert.Equal(1, registry.Count);
        Assert.NotNull(bridge.GetHandle(unit));
    }

    [Fact]
    public void UpdateUnit_WhenUnitUnloadsToZero_ShouldMarkForDeletion()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var unit = CreateUnit();

        bridge.Connect(unit);
        unit.RequestState(1);

        // Tick until stable
        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
            bridge.UpdateUnit(unit);
            if (unit.IsStable) break;
        }

        Assert.Equal(1, registry.Count);

        // Now unload
        unit.RequestState(0);
        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
            bridge.UpdateUnit(unit);
            if (unit.IsStable) break;
        }

        Assert.Single(registry.GetMarkedForDeletion());
    }

    #endregion
}
