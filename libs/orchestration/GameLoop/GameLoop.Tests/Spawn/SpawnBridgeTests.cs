using System;
using System.Collections.Generic;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Spawn;
using Tomato.CharacterSpawnSystem;
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
    public List<(EntityContext<TestActionCategory> context, string characterId, object? dataResource)> InitializeCalls { get; } = new();

    public void Initialize(EntityContext<TestActionCategory> context, string characterId, object? dataResource)
    {
        InitializeCalls.Add((context, characterId, dataResource));
    }
}

/// <summary>
/// テスト用のモックResourceLoader
/// </summary>
public class MockResourceLoader : IResourceLoader
{
    public bool AutoCompleteDataLoad { get; set; } = true;
    public bool AutoCompleteGameObjectLoad { get; set; } = true;
    public ResourceLoadResult DataLoadResult { get; set; } = ResourceLoadResult.Success;
    public ResourceLoadResult GameObjectLoadResult { get; set; } = ResourceLoadResult.Success;
    public object DataResource { get; set; } = new object();
    public object GameObjectResource { get; set; } = new object();

    private ResourceLoadCallback? _pendingDataCallback;
    private ResourceLoadCallback? _pendingGOCallback;

    public void LoadDataResourceAsync(string characterId, ResourceLoadCallback callback)
    {
        if (AutoCompleteDataLoad)
        {
            callback(DataLoadResult, DataLoadResult == ResourceLoadResult.Success ? DataResource : null!);
        }
        else
        {
            _pendingDataCallback = callback;
        }
    }

    public void LoadGameObjectResourceAsync(string characterId, ResourceLoadCallback callback)
    {
        if (AutoCompleteGameObjectLoad)
        {
            callback(GameObjectLoadResult, GameObjectLoadResult == ResourceLoadResult.Success ? GameObjectResource : null!);
        }
        else
        {
            _pendingGOCallback = callback;
        }
    }

    public void CompleteDataLoad() => _pendingDataCallback?.Invoke(DataLoadResult, DataResource);
    public void CompleteGOLoad() => _pendingGOCallback?.Invoke(GameObjectLoadResult, GameObjectResource);

    public void UnloadDataResource(object resource) { }
    public void UnloadGameObjectResource(object resource) { }
}

/// <summary>
/// テスト用のモックGameObjectProxy
/// </summary>
public class MockGameObjectProxy : IGameObjectProxy
{
    public bool IsActive { get; set; }
    public bool IsDestroyed { get; private set; }
    public void Destroy() => IsDestroyed = true;
}

/// <summary>
/// テスト用のモックGameObjectFactory
/// </summary>
public class MockGameObjectFactory : IGameObjectFactory
{
    public IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource)
    {
        return new MockGameObjectProxy();
    }
}

#endregion

public class SpawnBridgeTests
{
    private MockArena CreateArena() => new MockArena();
    private EntityContextRegistry<TestActionCategory> CreateRegistry() => new EntityContextRegistry<TestActionCategory>();
    private MockEntityInitializer CreateInitializer() => new MockEntityInitializer();

    private CharacterSpawnController CreateController(string id = "char1")
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        return new CharacterSpawnController(id, loader, factory);
    }

    private CharacterSpawnController CreateActiveController(string id = "char1")
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController(id, loader, factory);
        controller.RequestState(CharacterRequestState.Active);
        return controller;
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
    public void Connect_WithValidController_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateController();

        // Should not throw
        bridge.Connect(controller);
    }

    [Fact]
    public void Connect_WithNullController_ShouldThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        Assert.Throws<ArgumentNullException>(() => bridge.Connect(null!));
    }

    [Fact]
    public void Disconnect_WithValidController_ShouldSucceed()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateController();
        bridge.Connect(controller);

        // Should not throw
        bridge.Disconnect(controller);
    }

    [Fact]
    public void Disconnect_WithNullController_ShouldThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        Assert.Throws<ArgumentNullException>(() => bridge.Disconnect(null!));
    }

    #endregion

    #region OnCharacterActivated Tests

    [Fact]
    public void OnCharacterActivated_ShouldSpawnEntity()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);

        Assert.Equal(1, arena.SpawnedHandles.Count);
    }

    [Fact]
    public void OnCharacterActivated_ShouldRegisterContext()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);

        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void OnCharacterActivated_ShouldCallInitializer()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);

        Assert.Single(initializer.InitializeCalls);
        Assert.Equal("char1", initializer.InitializeCalls[0].characterId);
    }

    [Fact]
    public void OnCharacterActivated_ShouldSetContextSpawnController()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);

        var context = bridge.GetContext("char1");
        Assert.NotNull(context);
        Assert.Same(controller, context!.SpawnController);
    }

    [Fact]
    public void OnCharacterActivated_ShouldSetContextActive()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);

        var context = bridge.GetContext("char1");
        Assert.NotNull(context);
        Assert.True(context!.IsActive);
    }

    [Fact]
    public void OnCharacterActivated_CalledTwice_ShouldReactivate()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        var context = bridge.GetContext("char1");
        context!.IsActive = false; // Deactivate

        bridge.OnCharacterActivated(controller);

        Assert.True(context.IsActive);
        Assert.Equal(1, arena.SpawnedHandles.Count); // Should not spawn again
    }

    #endregion

    #region OnCharacterDeactivated Tests

    [Fact]
    public void OnCharacterDeactivated_ShouldSetContextInactive()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        bridge.OnCharacterDeactivated(controller);

        var context = bridge.GetContext("char1");
        Assert.NotNull(context);
        Assert.False(context!.IsActive);
    }

    [Fact]
    public void OnCharacterDeactivated_WithUnknownCharacter_ShouldNotThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController("unknown");

        // Should not throw
        bridge.OnCharacterDeactivated(controller);
    }

    #endregion

    #region OnCharacterRemoved Tests

    [Fact]
    public void OnCharacterRemoved_ShouldMarkForDeletion()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        bridge.OnCharacterRemoved("char1");

        var markedForDeletion = registry.GetMarkedForDeletion();
        Assert.Single(markedForDeletion);
    }

    [Fact]
    public void OnCharacterRemoved_ShouldRemoveMapping()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        bridge.OnCharacterRemoved("char1");

        Assert.Null(bridge.GetHandle("char1"));
    }

    [Fact]
    public void OnCharacterRemoved_WithUnknownCharacter_ShouldNotThrow()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        // Should not throw
        bridge.OnCharacterRemoved("unknown");
    }

    #endregion

    #region GetHandle/GetContext Tests

    [Fact]
    public void GetHandle_WithKnownCharacter_ShouldReturnHandle()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        var handle = bridge.GetHandle("char1");

        Assert.NotNull(handle);
    }

    [Fact]
    public void GetHandle_WithUnknownCharacter_ShouldReturnNull()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        var handle = bridge.GetHandle("unknown");

        Assert.Null(handle);
    }

    [Fact]
    public void GetContext_WithKnownCharacter_ShouldReturnContext()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);
        var controller = CreateActiveController();

        bridge.OnCharacterActivated(controller);
        var context = bridge.GetContext("char1");

        Assert.NotNull(context);
    }

    [Fact]
    public void GetContext_WithUnknownCharacter_ShouldReturnNull()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        var context = bridge.GetContext("unknown");

        Assert.Null(context);
    }

    #endregion

    #region StateChanged Event Integration Tests

    [Fact]
    public void Connect_WhenControllerBecomesActive_ShouldAutoRegister()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        bridge.Connect(controller);

        // Controller transitions to Active
        controller.RequestState(CharacterRequestState.Active);

        Assert.Equal(1, registry.Count);
        Assert.NotNull(bridge.GetHandle("char1"));
    }

    [Fact]
    public void Connect_WhenControllerBecomesInactive_ShouldDeactivate()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        bridge.Connect(controller);
        controller.RequestState(CharacterRequestState.Active);

        // Transition to Ready (inactive)
        controller.RequestState(CharacterRequestState.Ready);

        var context = bridge.GetContext("char1");
        Assert.NotNull(context);
        Assert.False(context!.IsActive);
    }

    [Fact]
    public void Connect_WhenControllerRemoved_ShouldMarkForDeletion()
    {
        var registry = CreateRegistry();
        var arena = CreateArena();
        var initializer = CreateInitializer();
        var bridge = new SpawnBridge<TestActionCategory>(registry, arena, initializer);

        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        bridge.Connect(controller);
        controller.RequestState(CharacterRequestState.Active);

        // Transition to None (removed)
        controller.RequestState(CharacterRequestState.None);

        Assert.Single(registry.GetMarkedForDeletion());
    }

    #endregion
}
