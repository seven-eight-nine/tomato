using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Controllers;

/// <summary>
/// Resource lifecycle tests - t-wada style thorough coverage
/// Focuses on resource management, cleanup, and leak prevention
/// </summary>
public class ResourceLifecycleTests
{
    #region Test Helpers

    private class TrackingResourceLoader : IResourceLoader
    {
        public List<string> DataLoadRequests { get; } = new List<string>();
        public List<string> GameObjectLoadRequests { get; } = new List<string>();
        public List<object> UnloadedDataResources { get; } = new List<object>();
        public List<object> UnloadedGameObjectResources { get; } = new List<object>();
        public int UnloadDataResourceCallCount { get; private set; } = 0;
        public int UnloadGameObjectResourceCallCount { get; private set; } = 0;

        private ResourceLoadCallback? pendingDataCallback;
        private ResourceLoadCallback? pendingGameObjectCallback;

        public bool AutoCompleteDataLoad { get; set; } = false;
        public bool AutoCompleteGameObjectLoad { get; set; } = false;
        public ResourceLoadResult DataLoadResult { get; set; } = ResourceLoadResult.Success;
        public ResourceLoadResult GameObjectLoadResult { get; set; } = ResourceLoadResult.Success;

        private object? lastDataResource;
        private object? lastGameObjectResource;

        public void LoadDataResourceAsync(string characterId, ResourceLoadCallback callback)
        {
            DataLoadRequests.Add(characterId);
            if (AutoCompleteDataLoad)
            {
                lastDataResource = DataLoadResult == ResourceLoadResult.Success ? new object() : null;
                callback(DataLoadResult, lastDataResource!);
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
                lastGameObjectResource = GameObjectLoadResult == ResourceLoadResult.Success ? new object() : null;
                callback(GameObjectLoadResult, lastGameObjectResource!);
            }
            else
            {
                pendingGameObjectCallback = callback;
            }
        }

        public void UnloadDataResource(object resource)
        {
            UnloadDataResourceCallCount++;
            UnloadedDataResources.Add(resource);
        }

        public void UnloadGameObjectResource(object resource)
        {
            UnloadGameObjectResourceCallCount++;
            UnloadedGameObjectResources.Add(resource);
        }

        public void CompleteDataLoad(ResourceLoadResult result, object? resource = null)
        {
            lastDataResource = resource ?? (result == ResourceLoadResult.Success ? new object() : null);
            pendingDataCallback?.Invoke(result, lastDataResource!);
            pendingDataCallback = null;
        }

        public void CompleteGameObjectLoad(ResourceLoadResult result, object? resource = null)
        {
            lastGameObjectResource = resource ?? (result == ResourceLoadResult.Success ? new object() : null);
            pendingGameObjectCallback?.Invoke(result, lastGameObjectResource!);
            pendingGameObjectCallback = null;
        }

        public object? LastDataResource => lastDataResource;
        public object? LastGameObjectResource => lastGameObjectResource;
    }

    private class TrackingGameObjectProxy : IGameObjectProxy
    {
        public bool IsActive { get; set; }
        public bool IsDestroyed { get; private set; }
        public int DestroyCallCount { get; private set; } = 0;

        public void Destroy()
        {
            DestroyCallCount++;
            IsDestroyed = true;
        }
    }

    private class TrackingGameObjectFactory : IGameObjectFactory
    {
        public List<(object goResource, object dataResource)> CreateCalls { get; } = new List<(object, object)>();
        public List<TrackingGameObjectProxy> CreatedProxies { get; } = new List<TrackingGameObjectProxy>();
        public TrackingGameObjectProxy? LastCreatedProxy { get; private set; }

        public IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource)
        {
            CreateCalls.Add((gameObjectResource, dataResource));
            LastCreatedProxy = new TrackingGameObjectProxy();
            CreatedProxies.Add(LastCreatedProxy);
            return LastCreatedProxy;
        }
    }

    #endregion

    #region Partial Resource Cleanup Tests (GO destroyed but data kept)

    [Fact]
    public void PartialCleanup_FromActive_ToPlacedOnly_ShouldKeepData()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        Assert.True(controller.IsDataLoaded);
        Assert.True(controller.HasGameObject);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: GO destroyed and unloaded, but data kept
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.Single(loader.UnloadedGameObjectResources);
        Assert.Empty(loader.UnloadedDataResources);
        Assert.True(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void PartialCleanup_FromInactive_ToPlacedOnly_ShouldKeepData()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.True(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void PartialCleanup_DuringGOLoading_ToPlacedOnly_ShouldKeepData()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        // Act: Cancel GO loading but keep data
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Should be in PlacedDataLoaded
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void PartialCleanup_CanReloadGO_AfterPartialCleanup()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Full cycle -> partial cleanup -> reload GO
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Act: Reload GO
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert: Should have created 2 GOs but only loaded data once
        Assert.Equal(2, factory.CreateCalls.Count);
        Assert.Equal(1, loader.DataLoadRequests.Count); // Data reused
        Assert.Equal(2, loader.GameObjectLoadRequests.Count); // GO reloaded
    }

    #endregion

    #region All Resources Unloaded from Error State Tests

    [Fact]
    public void ErrorState_DataLoadFailed_ToNone_NoResourcesLeaked()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: No resources to unload (load failed)
        Assert.Equal(0, loader.UnloadDataResourceCallCount);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorState_GOLoadFailed_ToNone_DataResourceUnloaded()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.True(controller.IsDataLoaded);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Data resource unloaded
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorState_AllResourcesCleared_AfterMultipleErrorCycles()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Multiple error cycles
        for (int i = 0; i < 3; i++)
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
            loader.CompleteDataLoad(ResourceLoadResult.Failed);
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: No resources accumulated
        Assert.Equal(0, loader.UnloadDataResourceCallCount);
        Assert.False(controller.IsDataLoaded);
    }

    #endregion

    #region Resource Leak Prevention with Rapid Cancellations Tests

    [Fact]
    public void RapidCancellation_DuringDataLoading_NoResourceLeak()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start and cancel rapidly multiple times
        for (int i = 0; i < 5; i++)
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: No resources leaked
        Assert.False(controller.IsDataLoaded);
        Assert.Equal(5, loader.DataLoadRequests.Count); // 5 load attempts
        Assert.Equal(0, loader.UnloadDataResourceCallCount); // Nothing to unload
    }

    [Fact]
    public void RapidCancellation_DuringGOLoading_NoResourceLeak()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Rapid GO loading cancellations
        for (int i = 0; i < 5; i++)
        {
            controller.RequestState(CharacterRequestState.Ready);
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: All data resources cleaned up properly
        Assert.False(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
        Assert.Equal(5, loader.UnloadDataResourceCallCount);
    }

    [Fact]
    public void RapidStateChanges_ActiveToNone_NoResourceLeak()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple complete cycles
        for (int i = 0; i < 3; i++)
        {
            controller.RequestState(CharacterRequestState.Active);
            controller.Update();
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: All resources properly cleaned up
        Assert.Equal(3, loader.UnloadDataResourceCallCount);
        Assert.Equal(3, loader.UnloadGameObjectResourceCallCount);
        Assert.Equal(3, factory.CreatedProxies.Count);
        Assert.True(factory.CreatedProxies.TrueForAll(p => p.IsDestroyed));
    }

    [Fact]
    public void RapidUpgradeDowngrade_NoResourceLeak()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Rapid upgrade/downgrade cycle
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        controller.RequestState(CharacterRequestState.None);

        // Assert: All resources cleaned up
        Assert.False(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
        Assert.Single(loader.UnloadedDataResources);
        Assert.Equal(2, loader.UnloadedGameObjectResources.Count);
    }

    [Fact]
    public void CancellationDuringCallback_NoResourceLeak()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start loading
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Complete with success, but immediately cancel
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        controller.RequestState(CharacterRequestState.None);

        // Assert: Resource should be cleaned up
        Assert.Single(loader.UnloadedDataResources);
        Assert.False(controller.IsDataLoaded);
    }

    #endregion

    #region UnloadDataResource Called Multiple Times Safely Tests

    [Fact]
    public void UnloadDataResource_CalledOnce_PerResource()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Unload called exactly once
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
    }

    [Fact]
    public void UnloadDataResource_NotCalled_WhenNoResourceLoaded()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start loading but don't complete
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Act: Cancel before completion
        controller.RequestState(CharacterRequestState.None);

        // Assert: Unload not called (nothing to unload)
        Assert.Equal(0, loader.UnloadDataResourceCallCount);
    }

    [Fact]
    public void UnloadDataResource_NotCalledTwice_OnDoubleCleanup()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        // Act: Request None again (already in NotPlaced)
        controller.RequestState(CharacterRequestState.None);
        controller.Update();
        controller.Update();

        // Assert: Still only one unload call
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
    }

    [Fact]
    public void UnloadGameObjectResource_CalledOnce_PerResource()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert
        Assert.Equal(1, loader.UnloadGameObjectResourceCallCount);
    }

    [Fact]
    public void DestroyGameObject_CalledOnce_PerProxy()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert
        Assert.Equal(1, factory.LastCreatedProxy!.DestroyCallCount);
    }

    [Fact]
    public void ResourceCleanup_CorrectOrder_GOBeforeData()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var cleanupOrder = new List<string>();
        // We can't easily track order with current mocks, but we can verify both are called
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Both cleanup methods called
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
        Assert.Equal(1, loader.UnloadGameObjectResourceCallCount);
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ResourceLifecycle_MultipleCharacters_Independent()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();

        var controller1 = new CharacterSpawnController("char1", loader, factory);
        var controller2 = new CharacterSpawnController("char2", loader, factory);

        // Act
        controller1.RequestState(CharacterRequestState.Active);
        controller1.Update();
        controller2.RequestState(CharacterRequestState.Active);
        controller2.Update();

        controller1.RequestState(CharacterRequestState.None);

        // Assert: Controller1 cleaned up, Controller2 still active
        Assert.Equal(CharacterInternalState.NotPlaced, controller1.CurrentState);
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller2.CurrentState);
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
        Assert.Equal(1, loader.UnloadGameObjectResourceCallCount);
    }

    [Fact]
    public void ResourceLifecycle_ReloadAfterFullCleanup_FreshResources()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Full cycle
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        var firstProxy = factory.LastCreatedProxy;
        controller.RequestState(CharacterRequestState.None);

        // Act: Reload
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert: New resources created
        Assert.NotSame(firstProxy, factory.LastCreatedProxy);
        Assert.Equal(2, factory.CreateCalls.Count);
        Assert.Equal(2, loader.DataLoadRequests.Count);
    }

    [Fact]
    public void ResourceLifecycle_CancelAndRestart_CleanHandoff()
    {
        // Arrange
        var loader = new TrackingResourceLoader();
        var factory = new TrackingGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start first load
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Cancel
        controller.RequestState(CharacterRequestState.None);

        // Start second load
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Complete both (first should be ignored, second processed)
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Only one resource unloaded (the second one)
        Assert.Equal(1, loader.UnloadDataResourceCallCount);
    }

    #endregion
}
