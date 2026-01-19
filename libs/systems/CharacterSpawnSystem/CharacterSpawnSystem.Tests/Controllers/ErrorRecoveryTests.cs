using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Controllers;

/// <summary>
/// Error recovery tests - t-wada style thorough coverage
/// Focuses on error handling, retry mechanisms, and recovery scenarios
/// </summary>
public class ErrorRecoveryTests
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

    #region Multiple Retry Attempts from DataLoadFailed Tests

    [Fact]
    public void DataLoadFailed_FirstRetry_ShouldStartNewLoad()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Initial failure
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Retry by resetting and requesting again
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
        Assert.Equal(2, loader.DataLoadRequests.Count);
    }

    [Fact]
    public void DataLoadFailed_SecondRetrySuccess_ShouldTransitionToLoaded()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Initial failure
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Retry
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void DataLoadFailed_MultipleRetries_AllFail_ShouldRemainInFailed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple retry attempts, all fail
        for (int i = 0; i < 5; i++)
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
            loader.CompleteDataLoad(ResourceLoadResult.Failed);
            Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: Should have attempted 5 loads
        Assert.Equal(5, loader.DataLoadRequests.Count);
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void DataLoadFailed_ThirdRetrySuccess_ShouldWork()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Two failures, then success
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.RequestState(CharacterRequestState.None);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.RequestState(CharacterRequestState.None);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.Equal(3, loader.DataLoadRequests.Count);
    }

    [Fact]
    public void DataLoadFailed_DirectRetry_WithoutNone_ShouldRetry()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Fail, then request same state again (triggers retry from DataLoadFailed)
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Update should trigger retry since target is still PlacedOnly
        controller.Update();

        // Assert: Should be back to loading
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
        Assert.Equal(2, loader.DataLoadRequests.Count);
    }

    #endregion

    #region Error Then Retry Then Error Again Sequence Tests

    [Fact]
    public void ErrorRetryError_DataLoad_ShouldHandleSequence()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Error -> Retry -> Error
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        controller.Update(); // Retry
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);

        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Assert: Should be back in failed state
        Assert.Equal(2, loader.DataLoadRequests.Count); // Initial + 1 retry (from Update)
    }

    [Fact]
    public void ErrorRetryError_ThenSuccess_ShouldRecover()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Error -> Retry -> Error -> Retry -> Success
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.Update(); // Retry
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.Update(); // Retry again
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorRetryError_GameObjectLoad_ShouldHandleSequence()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: GO Error -> Retry -> Error
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        controller.Update(); // Retry
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Assert
        Assert.Equal(2, loader.GameObjectLoadRequests.Count); // Initial + 1 retry (from Update)
    }

    [Fact]
    public void ErrorRetryError_AlternatingErrorTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Data fail -> Reset -> Data success -> GO fail -> Reset -> Data success -> GO success
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    #endregion

    #region Error State Transitions to All Possible Target States Tests

    [Fact]
    public void DataLoadFailed_ToNone_ShouldTransitionToNotPlaced()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void DataLoadFailed_ToPlacedOnly_ShouldRetry()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act: Request PlacedOnly again and Update
        controller.Update();

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
    }

    [Fact]
    public void DataLoadFailed_ToReady_ShouldRetryDataThenLoadGO()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act: Request Ready
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should proceed to GO loading
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);
    }

    [Fact]
    public void DataLoadFailed_ToActive_ShouldRetryAndContinueToActive()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act: Request Active
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update();

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
    }

    [Fact]
    public void GameObjectLoadFailed_ToNone_ShouldUnloadAllAndGoToNotPlaced()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.Single(loader.UnloadedDataResources);
    }

    [Fact]
    public void GameObjectLoadFailed_ToPlacedOnly_ShouldKeepDataAndGoToPlacedDataLoaded()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
        Assert.Empty(loader.UnloadedDataResources);
    }

    [Fact]
    public void GameObjectLoadFailed_ToReady_ShouldRetryGOLoad()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        // Act: Update to trigger retry
        controller.Update();

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);
        Assert.Equal(2, loader.GameObjectLoadRequests.Count);
    }

    [Fact]
    public void GameObjectLoadFailed_ToActive_ShouldRetryAndActivate()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update();

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
    }

    #endregion

    #region Resource Cleanup on Error State Transitions Tests

    [Fact]
    public void DataLoadFailed_ToNone_ShouldNotUnloadAnything()
    {
        // Arrange: DataLoadFailed has no resources loaded
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Nothing to unload since data load failed
        Assert.Empty(loader.UnloadedDataResources);
        Assert.Empty(loader.UnloadedGameObjectResources);
    }

    [Fact]
    public void GameObjectLoadFailed_ToNone_ShouldUnloadDataResource()
    {
        // Arrange: GOLoadFailed has data loaded but no GO
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.True(controller.IsDataLoaded);

        // Act
        controller.RequestState(CharacterRequestState.None);

        // Assert: Should unload data resource
        Assert.Single(loader.UnloadedDataResources);
        Assert.Empty(loader.UnloadedGameObjectResources);
    }

    [Fact]
    public void GameObjectLoadFailed_ToPlacedOnly_ShouldNotUnloadDataResource()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Should keep data resource
        Assert.Empty(loader.UnloadedDataResources);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorState_NoLeakedResources_AfterMultipleRetries()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple data load attempts
        for (int i = 0; i < 3; i++)
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
            loader.CompleteDataLoad(ResourceLoadResult.Failed);
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: No leaked resources (failures don't create resources)
        Assert.Empty(loader.UnloadedDataResources);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorRecovery_ThenCleanup_ShouldUnloadCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Fail -> Retry -> Success -> Cleanup
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.Update(); // Retry
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.True(controller.IsDataLoaded);

        controller.RequestState(CharacterRequestState.None);

        // Assert: Should unload the successfully loaded resource
        Assert.Single(loader.UnloadedDataResources);
    }

    [Fact]
    public void GOErrorRecovery_ThenCleanup_ShouldUnloadBothResources()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: GO Fail -> Retry -> Success -> Full Cleanup
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        controller.Update(); // Retry
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        Assert.True(controller.HasGameObject);

        controller.RequestState(CharacterRequestState.None);

        // Assert: Should unload both resources
        Assert.Single(loader.UnloadedDataResources);
        Assert.Single(loader.UnloadedGameObjectResources);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ConsecutiveErrors_DifferentTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Data error, then cancel, then data success + GO error
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Retry and succeed
        controller.Update();
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    [Fact]
    public void ErrorDuringUpgrade_ShouldAllowDowngrade()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Try to upgrade to Ready, GO fails, downgrade to PlacedOnly
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Should be in PlacedDataLoaded with data intact
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void ErrorState_UpdateMultipleTimes_ShouldContinueRetrying()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Fail, then update multiple times with continued failures
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        for (int i = 0; i < 3; i++)
        {
            controller.Update();
            loader.CompleteDataLoad(ResourceLoadResult.Failed);
        }

        // Assert: Should have made 4 attempts total (initial + 3 retries)
        Assert.Equal(4, loader.DataLoadRequests.Count);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);
    }

    #endregion
}
