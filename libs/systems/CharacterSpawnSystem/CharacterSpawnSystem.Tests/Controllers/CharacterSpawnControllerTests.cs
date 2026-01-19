using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Controllers;

/// <summary>
/// CharacterSpawnController comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CharacterSpawnControllerTests
{
    #region Test Helpers

    private class MockResourceLoader : IResourceLoader
    {
        public List<string> DataLoadRequests { get; } = new List<string>();
        public List<string> GameObjectLoadRequests { get; } = new List<string>();
        public List<object> UnloadedDataResources { get; } = new List<object>();
        public List<object> UnloadedGameObjectResources { get; } = new List<object>();

        private ResourceLoadCallback? pendingDataCallback;
        private ResourceLoadCallback? pendingGameObjectCallback;

        public bool AutoCompleteDataLoad { get; set; } = false;
        public bool AutoCompleteGameObjectLoad { get; set; } = false;
        public ResourceLoadResult DataLoadResult { get; set; } = ResourceLoadResult.Success;
        public ResourceLoadResult GameObjectLoadResult { get; set; } = ResourceLoadResult.Success;

        public void LoadDataResourceAsync(string characterId, ResourceLoadCallback callback)
        {
            DataLoadRequests.Add(characterId);
            if (AutoCompleteDataLoad)
            {
                callback(DataLoadResult, DataLoadResult == ResourceLoadResult.Success ? new object() : null!);
            }
            else
            {
                pendingDataCallback = callback;
            }
        }

        public void LoadGameObjectResourceAsync(string characterId, ResourceLoadCallback callback)
        {
            GameObjectLoadRequests.Add(characterId);
            if (AutoCompleteGameObjectLoad)
            {
                callback(GameObjectLoadResult, GameObjectLoadResult == ResourceLoadResult.Success ? new object() : null!);
            }
            else
            {
                pendingGameObjectCallback = callback;
            }
        }

        public void UnloadDataResource(object resource)
        {
            UnloadedDataResources.Add(resource);
        }

        public void UnloadGameObjectResource(object resource)
        {
            UnloadedGameObjectResources.Add(resource);
        }

        public void CompleteDataLoad(ResourceLoadResult result, object? resource = null)
        {
            pendingDataCallback?.Invoke(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            pendingDataCallback = null;
        }

        public void CompleteGameObjectLoad(ResourceLoadResult result, object? resource = null)
        {
            pendingGameObjectCallback?.Invoke(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            pendingGameObjectCallback = null;
        }
    }

    private class MockGameObjectProxy : IGameObjectProxy
    {
        public bool IsActive { get; set; }
        public bool IsDestroyed { get; private set; }

        public void Destroy()
        {
            IsDestroyed = true;
        }
    }

    private class MockGameObjectFactory : IGameObjectFactory
    {
        public List<(object goResource, object dataResource)> CreateCalls { get; } = new List<(object, object)>();
        public MockGameObjectProxy? LastCreatedProxy { get; private set; }

        public IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource)
        {
            CreateCalls.Add((gameObjectResource, dataResource));
            LastCreatedProxy = new MockGameObjectProxy();
            return LastCreatedProxy;
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateController()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        Assert.NotNull(controller);
    }

    [Fact]
    public void Constructor_ShouldSetCharacterId()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        Assert.Equal("char1", controller.CharacterId);
    }

    [Fact]
    public void Constructor_ShouldStartWithNotPlacedState()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void Constructor_ShouldStartWithNoneTargetState()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        Assert.Equal(CharacterRequestState.None, controller.TargetRequestState);
    }

    [Fact]
    public void Constructor_WithNullCharacterId_ShouldThrow()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();

        Assert.Throws<ArgumentException>(() => new CharacterSpawnController(null!, loader, factory));
    }

    [Fact]
    public void Constructor_WithEmptyCharacterId_ShouldThrow()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();

        Assert.Throws<ArgumentException>(() => new CharacterSpawnController("", loader, factory));
    }

    [Fact]
    public void Constructor_WithNullResourceLoader_ShouldThrow()
    {
        var factory = new MockGameObjectFactory();

        Assert.Throws<ArgumentNullException>(() => new CharacterSpawnController("char1", null!, factory));
    }

    [Fact]
    public void Constructor_WithNullGameObjectFactory_ShouldThrow()
    {
        var loader = new MockResourceLoader();

        Assert.Throws<ArgumentNullException>(() => new CharacterSpawnController("char1", loader, null!));
    }

    #endregion

    #region RequestState Tests

    [Fact]
    public void RequestState_ToPlacedOnly_ShouldStartDataLoading()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
        Assert.Single(loader.DataLoadRequests);
    }

    [Fact]
    public void RequestState_ToSameState_ShouldNotRetrigger()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Single(loader.DataLoadRequests);
    }

    [Fact]
    public void RequestState_ShouldUpdateTargetRequestState()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);

        Assert.Equal(CharacterRequestState.Ready, controller.TargetRequestState);
    }

    #endregion

    #region Data Loading Tests

    [Fact]
    public void DataLoadComplete_Success_ShouldTransitionToPlacedDataLoaded()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    [Fact]
    public void DataLoadComplete_Failure_ShouldTransitionToDataLoadFailed()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);
    }

    [Fact]
    public void DataLoadComplete_ShouldSetIsDataLoadedTrue()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        Assert.False(controller.IsDataLoaded);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        Assert.True(controller.IsDataLoaded);
    }

    #endregion

    #region GameObject Loading Tests

    [Fact]
    public void RequestReady_AfterDataLoaded_ShouldStartGameObjectLoading()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);

        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);
        Assert.Single(loader.GameObjectLoadRequests);
    }

    [Fact]
    public void GameObjectLoadComplete_Success_ShouldTransitionToInstantiatedInactive()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    [Fact]
    public void GameObjectLoadComplete_Failure_ShouldTransitionToGameObjectLoadFailed()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.GameObjectLoadResult = ResourceLoadResult.Failed;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);
    }

    [Fact]
    public void GameObjectLoadComplete_ShouldCreateGameObject()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Single(factory.CreateCalls);
        Assert.True(controller.HasGameObject);
    }

    [Fact]
    public void GameObjectLoadComplete_ShouldCreateInactiveGameObject()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.False(factory.LastCreatedProxy!.IsActive);
    }

    #endregion

    #region Activation Tests

    [Fact]
    public void RequestActive_FromInstantiatedInactive_ShouldActivateGameObject()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update(); // Process to InstantiatedInactive

        controller.RequestState(CharacterRequestState.Active);

        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsActive);
    }

    [Fact]
    public void RequestReady_FromInstantiatedActive_ShouldDeactivateGameObject()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update(); // Process to Active state

        controller.RequestState(CharacterRequestState.Ready);

        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
        Assert.False(factory.LastCreatedProxy!.IsActive);
    }

    #endregion

    #region Deallocation Tests

    [Fact]
    public void RequestNone_FromPlacedDataLoaded_ShouldUnloadData()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.Single(loader.UnloadedDataResources);
    }

    [Fact]
    public void RequestNone_FromInstantiatedActive_ShouldDestroyAndUnloadAll()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.Single(loader.UnloadedGameObjectResources);
        Assert.Single(loader.UnloadedDataResources);
    }

    [Fact]
    public void RequestPlacedOnly_FromInstantiatedActive_ShouldDestroyGameObjectButKeepData()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.Single(loader.UnloadedGameObjectResources);
        Assert.Empty(loader.UnloadedDataResources);
    }

    #endregion

    #region Cancel During Loading Tests

    [Fact]
    public void RequestNone_DuringDataLoading_ShouldCancelToNotPlaced()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void RequestNone_DuringGameObjectLoading_ShouldCancelToNotPlaced()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        // Now in InstantiatingGOLoading
        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.Single(loader.UnloadedDataResources);
    }

    [Fact]
    public void RequestPlacedOnly_DuringGameObjectLoading_ShouldCancelToPlacedDataLoaded()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    [Fact]
    public void LateCallback_AfterCancel_ShouldBeIgnored()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);
        // Late callback arrives
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public void RequestRetry_FromDataLoadFailed_ShouldRetryLoad()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
        Assert.Equal(2, loader.DataLoadRequests.Count);
    }

    [Fact]
    public void RequestRetry_FromGameObjectLoadFailed_ShouldRetryLoad()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.GameObjectLoadResult = ResourceLoadResult.Failed;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Reset loader to success for retry
        loader.GameObjectLoadResult = ResourceLoadResult.Success;

        // Request None first, then Ready again to retry
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    [Fact]
    public void FromDataLoadFailed_RequestNone_ShouldGoToNotPlaced()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void FromGameObjectLoadFailed_RequestPlacedOnly_ShouldGoToPlacedDataLoaded()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.GameObjectLoadResult = ResourceLoadResult.Failed;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    #endregion

    #region StateChanged Event Tests

    [Fact]
    public void StateChanged_ShouldFireOnTransition()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var eventsFired = new List<StateChangedEventArgs>();
        controller.StateChanged += (sender, e) => eventsFired.Add(e);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Single(eventsFired);
        Assert.Equal(CharacterInternalState.NotPlaced, eventsFired[0].OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, eventsFired[0].NewState);
    }

    [Fact]
    public void StateChanged_ShouldFireForEachTransition()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var eventsFired = new List<StateChangedEventArgs>();
        controller.StateChanged += (sender, e) => eventsFired.Add(e);

        controller.RequestState(CharacterRequestState.Ready);

        // NotPlaced -> DataLoading, DataLoading -> DataLoaded, DataLoaded -> GOLoading
        Assert.Equal(3, eventsFired.Count);
    }

    [Fact]
    public void StateChanged_SenderShouldBeController()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        object? sender = null;
        controller.StateChanged += (s, e) => sender = s;

        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Same(controller, sender);
    }

    #endregion

    #region Full Lifecycle Tests

    [Fact]
    public void FullLifecycle_FromNoneToActiveAndBack()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Go to Active
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsActive);

        // Go back to None
        controller.RequestState(CharacterRequestState.None);

        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
    }

    [Fact]
    public void FullLifecycle_UpgradeFromPlacedOnlyToActive()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start with PlacedOnly
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);

        // Upgrade to Active
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update();

        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
    }

    [Fact]
    public void FullLifecycle_DowngradeFromActiveToPlacedOnly()
    {
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start Active
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);

        // Downgrade to PlacedOnly
        controller.RequestState(CharacterRequestState.PlacedOnly);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.True(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
    }

    #endregion

    #region Update Method Tests

    [Fact]
    public void Update_ShouldProcessPendingTransitions()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        controller.Update();

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    [Fact]
    public void Update_OnStableState_ShouldNotChange()
    {
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        var stateBeforeUpdate = controller.CurrentState;
        controller.Update();
        controller.Update();
        controller.Update();

        Assert.Equal(stateBeforeUpdate, controller.CurrentState);
    }

    #endregion
}
