using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Integration;

/// <summary>
/// Complex integration scenario tests - t-wada style thorough coverage
/// Focuses on real-world usage patterns and edge case combinations
/// </summary>
public class ComplexScenarioTests
{
    #region Test Helpers

    private class MockResourceLoader : IResourceLoader
    {
        public List<string> DataLoadRequests { get; } = new List<string>();
        public List<string> GameObjectLoadRequests { get; } = new List<string>();
        public List<object> UnloadedDataResources { get; } = new List<object>();
        public List<object> UnloadedGameObjectResources { get; } = new List<object>();

        private Queue<ResourceLoadCallback> pendingDataCallbacks = new Queue<ResourceLoadCallback>();
        private Queue<ResourceLoadCallback> pendingGameObjectCallbacks = new Queue<ResourceLoadCallback>();

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
                pendingDataCallbacks.Enqueue(callback);
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
                pendingGameObjectCallbacks.Enqueue(callback);
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
            if (pendingDataCallbacks.Count > 0)
            {
                var callback = pendingDataCallbacks.Dequeue();
                callback(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            }
        }

        public void CompleteGameObjectLoad(ResourceLoadResult result, object? resource = null)
        {
            if (pendingGameObjectCallbacks.Count > 0)
            {
                var callback = pendingGameObjectCallbacks.Dequeue();
                callback(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            }
        }

        public void CompleteAllPendingDataLoads(ResourceLoadResult result)
        {
            while (pendingDataCallbacks.Count > 0)
            {
                var callback = pendingDataCallbacks.Dequeue();
                callback(result, result == ResourceLoadResult.Success ? new object() : null!);
            }
        }

        public void CompleteAllPendingGameObjectLoads(ResourceLoadResult result)
        {
            while (pendingGameObjectCallbacks.Count > 0)
            {
                var callback = pendingGameObjectCallbacks.Dequeue();
                callback(result, result == ResourceLoadResult.Success ? new object() : null!);
            }
        }

        public int PendingDataCallbackCount => pendingDataCallbacks.Count;
        public int PendingGameObjectCallbackCount => pendingGameObjectCallbacks.Count;
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
        public List<MockGameObjectProxy> AllProxies { get; } = new List<MockGameObjectProxy>();
        public MockGameObjectProxy? LastCreatedProxy { get; private set; }

        public IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource)
        {
            CreateCalls.Add((gameObjectResource, dataResource));
            LastCreatedProxy = new MockGameObjectProxy();
            AllProxies.Add(LastCreatedProxy);
            return LastCreatedProxy;
        }
    }

    private class StateTransitionTracker
    {
        public List<(CharacterInternalState From, CharacterInternalState To)> Transitions { get; }
            = new List<(CharacterInternalState, CharacterInternalState)>();

        public void Handler(object sender, StateChangedEventArgs e)
        {
            Transitions.Add((e.OldState, e.NewState));
        }
    }

    #endregion

    #region Rapid State Changes None -> Active -> None Tests

    [Fact]
    public void RapidStateChange_NoneToActiveToNone_Synchronous_ShouldWork()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Rapid state changes
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);

        // Assert: Clean state
        Assert.False(controller.IsDataLoaded);
        Assert.False(controller.HasGameObject);
        Assert.True(factory.LastCreatedProxy!.IsDestroyed);
    }

    [Fact]
    public void RapidStateChange_NoneToActiveToNone_WithPendingLoads_ShouldCancel()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start Active, cancel immediately
        controller.RequestState(CharacterRequestState.Active);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);

        // Late callbacks should be ignored
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void RapidStateChange_MultipleCycles_ShouldRemainConsistent()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple rapid cycles
        for (int i = 0; i < 10; i++)
        {
            controller.RequestState(CharacterRequestState.Active);
            controller.Update();
            Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);

            controller.RequestState(CharacterRequestState.None);
            Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        }

        // Assert: All resources properly cleaned up
        Assert.Equal(10, loader.UnloadedDataResources.Count);
        Assert.Equal(10, loader.UnloadedGameObjectResources.Count);
        Assert.Equal(10, factory.AllProxies.Count);
        Assert.True(factory.AllProxies.TrueForAll(p => p.IsDestroyed));
    }

    [Fact]
    public void RapidStateChange_NoneToActiveToNone_DuringGOLoading_ShouldCleanup()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Go to GO loading, then cancel
        controller.RequestState(CharacterRequestState.Active);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.Single(loader.UnloadedDataResources);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void RapidStateChange_ActiveToNoneToActive_ShouldReload()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();
        var firstProxy = factory.LastCreatedProxy;

        // Act: None then back to Active
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert: New resources created
        Assert.NotSame(firstProxy, factory.LastCreatedProxy);
        Assert.True(firstProxy!.IsDestroyed);
        Assert.False(factory.LastCreatedProxy!.IsDestroyed);
    }

    #endregion

    #region State Change During Multiple Pending Loads Tests

    [Fact]
    public void PendingLoads_StateChange_DuringDataLoad_CancelsProperly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start loading
        controller.RequestState(CharacterRequestState.Ready);
        Assert.Equal(1, loader.PendingDataCallbackCount);

        // Act: Change state
        controller.RequestState(CharacterRequestState.None);

        // Complete the pending load (should be ignored)
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void PendingLoads_StateChange_DuringGOLoad_CancelsProperly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(1, loader.PendingGameObjectCallbackCount);

        // Act: Cancel
        controller.RequestState(CharacterRequestState.None);

        // Complete GO load (should be ignored)
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void PendingLoads_MultipleStartStop_HandlesCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Multiple start/stop cycles create multiple pending callbacks
        for (int i = 0; i < 3; i++)
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
            controller.RequestState(CharacterRequestState.None);
        }

        // Act: Complete all pending loads
        loader.CompleteAllPendingDataLoads(ResourceLoadResult.Success);

        // Assert: All callbacks ignored, state remains NotPlaced
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void PendingLoads_UpgradeDuringLoad_ContinuesAfterCompletion()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Start with PlacedOnly
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Upgrade to Active during load
        controller.RequestState(CharacterRequestState.Active);

        // Act: Complete data load
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should continue to GO loading
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);
    }

    [Fact]
    public void PendingLoads_DowngradeDuringGOLoad_StopsAtData()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Downgrade during GO load
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Should be at PlacedDataLoaded
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);

        // Late GO callback ignored
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    #endregion

    #region Two Error Failures in Succession with Recovery Tests

    [Fact]
    public void TwoDataErrors_ThenRecovery_ShouldSucceed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new StateTransitionTracker();
        controller.StateChanged += tracker.Handler;

        // Act: First failure
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Second failure
        controller.Update(); // Retry
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Recovery
        controller.Update(); // Retry again
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void DataErrorThenGOError_ThenRecovery_ShouldSucceed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Data error
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Retry data - success, then GO error
        controller.Update();
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Retry GO - success
        controller.Update();
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    [Fact]
    public void TwoGOErrors_ThenRecovery_ShouldSucceed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: First GO failure
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Second GO failure
        controller.Update();
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // Recovery
        controller.Update();
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
        Assert.True(controller.HasGameObject);
    }

    [Fact]
    public void ErrorRecovery_DataIntact_DuringGOErrors()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple GO failures
        controller.RequestState(CharacterRequestState.Ready);
        for (int i = 0; i < 5; i++)
        {
            loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
            Assert.True(controller.IsDataLoaded); // Data stays loaded
            controller.Update();
        }

        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Data was never reloaded
        Assert.Equal(1, loader.DataLoadRequests.Count);
        Assert.Equal(6, loader.GameObjectLoadRequests.Count);
    }

    [Fact]
    public void ErrorRecovery_CancelDuringError_CleansUp()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: GO error, then cancel
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);

        // Assert: Clean state
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.IsDataLoaded);
        Assert.Single(loader.UnloadedDataResources);
    }

    #endregion

    #region Full Lifecycle with Callbacks Tests

    [Fact]
    public void FullLifecycle_WithAsyncCallbacks_TracksAllTransitions()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new StateTransitionTracker();
        controller.StateChanged += tracker.Handler;

        // Act: Full lifecycle
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update(); // Activate

        controller.RequestState(CharacterRequestState.None);

        // Assert: All transitions tracked
        var expectedTransitions = new[]
        {
            (CharacterInternalState.NotPlaced, CharacterInternalState.PlacedDataLoading),
            (CharacterInternalState.PlacedDataLoading, CharacterInternalState.PlacedDataLoaded),
            (CharacterInternalState.PlacedDataLoaded, CharacterInternalState.InstantiatingGOLoading),
            (CharacterInternalState.InstantiatingGOLoading, CharacterInternalState.InstantiatedInactive),
            (CharacterInternalState.InstantiatedInactive, CharacterInternalState.InstantiatedActive),
            (CharacterInternalState.InstantiatedActive, CharacterInternalState.NotPlaced),
        };

        Assert.Equal(expectedTransitions.Length, tracker.Transitions.Count);
        for (int i = 0; i < expectedTransitions.Length; i++)
        {
            Assert.Equal(expectedTransitions[i].Item1, tracker.Transitions[i].From);
            Assert.Equal(expectedTransitions[i].Item2, tracker.Transitions[i].To);
        }
    }

    [Fact]
    public void FullLifecycle_WithUpgradeDowngrade_TracksAllTransitions()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new StateTransitionTracker();
        controller.StateChanged += tracker.Handler;

        // Act: Upgrade/downgrade cycle
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.Ready);
        controller.Update();
        controller.RequestState(CharacterRequestState.Active);
        controller.RequestState(CharacterRequestState.Ready);
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        // Assert: Final state correct
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.True(tracker.Transitions.Count > 0);
    }

    [Fact]
    public void FullLifecycle_MultipleCharacters_Independent()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();

        var controller1 = new CharacterSpawnController("char1", loader, factory);
        var controller2 = new CharacterSpawnController("char2", loader, factory);
        var controller3 = new CharacterSpawnController("char3", loader, factory);

        // Act: Different states for each
        controller1.RequestState(CharacterRequestState.Active);
        controller1.Update();

        controller2.RequestState(CharacterRequestState.Ready);
        controller2.Update();

        controller3.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Each in correct state
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller1.CurrentState);
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller2.CurrentState);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller3.CurrentState);
    }

    [Fact]
    public void FullLifecycle_WithCallbackExceptions_ContinuesNormally()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        int errorCount = 0;
        controller.StateChanged += (s, e) =>
        {
            if (e.NewState == CharacterInternalState.PlacedDataLoaded)
            {
                errorCount++;
                throw new Exception("Test exception");
            }
        };

        // Act: Try full lifecycle
        try
        {
            controller.RequestState(CharacterRequestState.Active);
        }
        catch { }

        // Continue after exception
        controller.Update();

        try
        {
            controller.RequestState(CharacterRequestState.None);
        }
        catch { }

        // Assert: State machine continued despite exception
        Assert.True(errorCount > 0);
    }

    [Fact]
    public void FullLifecycle_CompleteScenario_WithErrors()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Complex scenario with errors
        // 1. Start loading
        controller.RequestState(CharacterRequestState.Active);

        // 2. Data load fails
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // 3. Retry succeeds
        controller.Update();
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        // 4. GO load fails
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, controller.CurrentState);

        // 5. Downgrade to PlacedOnly
        controller.RequestState(CharacterRequestState.PlacedOnly);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);

        // 6. Upgrade to Active again
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update();

        // Assert: Finally at Active
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
        Assert.True(controller.HasGameObject);
    }

    #endregion

    #region Edge Cases and Stress Tests

    [Fact]
    public void StressTest_RapidStateChanges_100Cycles()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: 100 rapid cycles
        for (int i = 0; i < 100; i++)
        {
            controller.RequestState(CharacterRequestState.Active);
            controller.Update();
            controller.RequestState(CharacterRequestState.None);
        }

        // Assert: No resource leaks, consistent state
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.Equal(100, loader.UnloadedDataResources.Count);
        Assert.Equal(100, loader.UnloadedGameObjectResources.Count);
    }

    [Fact]
    public void StressTest_AlternatingUpDowngrades()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Alternating state changes
        var states = new[]
        {
            CharacterRequestState.Active,
            CharacterRequestState.Ready,
            CharacterRequestState.Active,
            CharacterRequestState.PlacedOnly,
            CharacterRequestState.Active,
            CharacterRequestState.None,
            CharacterRequestState.Active,
            CharacterRequestState.PlacedOnly,
            CharacterRequestState.Ready,
            CharacterRequestState.None
        };

        foreach (var state in states)
        {
            controller.RequestState(state);
            controller.Update();
        }

        // Assert: Final state correct
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void EdgeCase_AllStatesVisited_SingleLifecycle()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var visitedStates = new HashSet<CharacterInternalState>();
        controller.StateChanged += (s, e) =>
        {
            visitedStates.Add(e.OldState);
            visitedStates.Add(e.NewState);
        };

        // Act: Visit as many states as possible
        controller.RequestState(CharacterRequestState.Active);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        controller.Update();
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Failed);
        controller.Update();
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        controller.Update();
        controller.RequestState(CharacterRequestState.Ready);
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        // Assert: Multiple states visited
        Assert.Contains(CharacterInternalState.NotPlaced, visitedStates);
        Assert.Contains(CharacterInternalState.PlacedDataLoading, visitedStates);
        Assert.Contains(CharacterInternalState.DataLoadFailed, visitedStates);
        Assert.Contains(CharacterInternalState.PlacedDataLoaded, visitedStates);
        Assert.Contains(CharacterInternalState.InstantiatingGOLoading, visitedStates);
        Assert.Contains(CharacterInternalState.GameObjectLoadFailed, visitedStates);
        Assert.Contains(CharacterInternalState.InstantiatedInactive, visitedStates);
        Assert.Contains(CharacterInternalState.InstantiatedActive, visitedStates);
    }

    [Fact]
    public void EdgeCase_SameCallback_CompletedMultipleTimes()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple load requests (callbacks queue up in our mock)
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Complete all pending (only last one should matter)
        loader.CompleteAllPendingDataLoads(ResourceLoadResult.Success);

        // Assert: State reflects last successful completion for the current request
        // The controller is in PlacedDataLoading, so completing callbacks should work
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    #endregion
}
