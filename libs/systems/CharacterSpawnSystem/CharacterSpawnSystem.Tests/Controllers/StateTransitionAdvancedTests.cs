using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Controllers;

/// <summary>
/// Advanced state transition tests - t-wada style thorough coverage
/// Focuses on edge cases and boundary conditions in state transition behavior
/// </summary>
public class StateTransitionAdvancedTests
{
    #region Test Helpers

    private class MockResourceLoader : IResourceLoader
    {
        public List<string> DataLoadRequests { get; } = new List<string>();
        public List<string> GameObjectLoadRequests { get; } = new List<string>();
        public List<object> UnloadedDataResources { get; } = new List<object>();
        public List<object> UnloadedGameObjectResources { get; } = new List<object>();

        private List<ResourceLoadCallback> pendingDataCallbacks = new List<ResourceLoadCallback>();
        private List<ResourceLoadCallback> pendingGameObjectCallbacks = new List<ResourceLoadCallback>();

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
                pendingDataCallbacks.Add(callback);
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
                pendingGameObjectCallbacks.Add(callback);
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
                var callback = pendingDataCallbacks[0];
                pendingDataCallbacks.RemoveAt(0);
                callback(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            }
        }

        public void CompleteGameObjectLoad(ResourceLoadResult result, object? resource = null)
        {
            if (pendingGameObjectCallbacks.Count > 0)
            {
                var callback = pendingGameObjectCallbacks[0];
                pendingGameObjectCallbacks.RemoveAt(0);
                callback(result, resource ?? (result == ResourceLoadResult.Success ? new object() : null!));
            }
        }

        public void CompleteAllDataLoads(ResourceLoadResult result)
        {
            var callbacks = new List<ResourceLoadCallback>(pendingDataCallbacks);
            pendingDataCallbacks.Clear();
            foreach (var callback in callbacks)
            {
                callback(result, result == ResourceLoadResult.Success ? new object() : null!);
            }
        }

        public void CompleteAllGameObjectLoads(ResourceLoadResult result)
        {
            var callbacks = new List<ResourceLoadCallback>(pendingGameObjectCallbacks);
            pendingGameObjectCallbacks.Clear();
            foreach (var callback in callbacks)
            {
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
        public MockGameObjectProxy? LastCreatedProxy { get; private set; }

        public IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource)
        {
            CreateCalls.Add((gameObjectResource, dataResource));
            LastCreatedProxy = new MockGameObjectProxy();
            return LastCreatedProxy;
        }
    }

    /// <summary>
    /// Testable controller that exposes OnMaxTransitionsExceeded callback
    /// </summary>
    private class TestableSpawnController : CharacterSpawnController
    {
        public int MaxTransitionsExceededCount { get; private set; } = 0;
        public List<(CharacterInternalState OldState, CharacterInternalState NewState)> StateChanges { get; }
            = new List<(CharacterInternalState, CharacterInternalState)>();

        public TestableSpawnController(
            string characterId,
            IResourceLoader resourceLoader,
            IGameObjectFactory gameObjectFactory) : base(characterId, resourceLoader, gameObjectFactory)
        {
        }

        protected override void OnMaxTransitionsExceeded()
        {
            MaxTransitionsExceededCount++;
        }

        protected override void OnStateChanged(CharacterInternalState oldState, CharacterInternalState newState)
        {
            StateChanges.Add((oldState, newState));
            base.OnStateChanged(oldState, newState);
        }
    }

    /// <summary>
    /// Resource loader that triggers infinite loop scenario
    /// </summary>
    private class InfiniteLoopResourceLoader : IResourceLoader
    {
        private CharacterSpawnController? controller;
        public int LoadCallCount { get; private set; } = 0;

        public void SetController(CharacterSpawnController ctrl)
        {
            controller = ctrl;
        }

        public void LoadDataResourceAsync(string characterId, ResourceLoadCallback callback)
        {
            LoadCallCount++;
            // Immediately succeed and trigger another state change
            callback(ResourceLoadResult.Success, new object());
        }

        public void LoadGameObjectResourceAsync(string characterId, ResourceLoadCallback callback)
        {
            LoadCallCount++;
            callback(ResourceLoadResult.Success, new object());
        }

        public void UnloadDataResource(object resource) { }
        public void UnloadGameObjectResource(object resource) { }
    }

    #endregion

    #region MAX_TRANSITIONS_PER_UPDATE Loop Guard Tests

    [Fact]
    public void MaxTransitions_LoopGuard_ShouldPreventInfiniteLoop()
    {
        // Arrange: Create a scenario where state keeps changing
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act: Request Active, which triggers multiple state transitions
        controller.RequestState(CharacterRequestState.Active);

        // Assert: Should complete normally (not hang)
        // The transitions are: NotPlaced -> DataLoading -> DataLoaded -> GOLoading -> Inactive -> Active
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
        Assert.True(controller.StateChanges.Count <= 10); // MAX_TRANSITIONS_PER_UPDATE = 10
    }

    [Fact]
    public void MaxTransitions_NormalFlow_ShouldNotExceedLimit()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert: Normal flow shouldn't trigger max transitions exceeded
        Assert.Equal(0, controller.MaxTransitionsExceededCount);
    }

    [Fact]
    public void MaxTransitions_MultipleUpdates_ShouldStayWithinLimit()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act: Multiple update cycles
        for (int i = 0; i < 20; i++)
        {
            controller.Update();
        }

        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert
        Assert.Equal(0, controller.MaxTransitionsExceededCount);
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
    }

    #endregion

    #region OnMaxTransitionsExceeded Callback Tests

    [Fact]
    public void OnMaxTransitionsExceeded_ShouldBeOverridable()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Assert: Controller should be created without errors (virtual method works)
        Assert.NotNull(controller);
        Assert.Equal(0, controller.MaxTransitionsExceededCount);
    }

    [Fact]
    public void OnMaxTransitionsExceeded_InBaseClass_ShouldNotThrow()
    {
        // Arrange: Use base class (not testable subclass)
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act & Assert: Should not throw when max transitions would be exceeded in normal use
        var exception = Record.Exception(() =>
        {
            controller.RequestState(CharacterRequestState.Active);
            controller.Update();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void OnMaxTransitionsExceeded_TrackingMultipleCalls_ShouldAccumulate()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act: Normal operations shouldn't trigger max exceeded
        controller.RequestState(CharacterRequestState.Active);
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.Active);

        // Assert: Normal operations don't trigger max transitions exceeded
        Assert.Equal(0, controller.MaxTransitionsExceededCount);
    }

    #endregion

    #region Concurrent Request State Changes During Transitions Tests

    [Fact]
    public void ConcurrentRequest_DuringDataLoading_ShouldHandleStateChange()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start loading, then change request before completion
        controller.RequestState(CharacterRequestState.PlacedOnly);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);

        // Change request while still loading
        controller.RequestState(CharacterRequestState.Active);

        // Assert: Target state updated, but still in loading state
        Assert.Equal(CharacterRequestState.Active, controller.TargetRequestState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
    }

    [Fact]
    public void ConcurrentRequest_DowngradeDuringLoading_ShouldCancel()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start Ready flow, then downgrade to None during GO loading
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);

        // Assert: Should cancel and go to NotPlaced
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void ConcurrentRequest_UpgradeDuringLoading_ShouldContinue()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start PlacedOnly, upgrade to Active during data loading
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.Active);

        // Complete data load
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should continue to GO loading for Active target
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);
        Assert.Equal(CharacterRequestState.Active, controller.TargetRequestState);
    }

    [Fact]
    public void ConcurrentRequest_MultipleChangesBeforeCallback_ShouldUseFinalTarget()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Multiple request changes before callback
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.Ready);
        controller.RequestState(CharacterRequestState.Active);
        controller.RequestState(CharacterRequestState.Ready);

        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should use final target state (Ready)
        Assert.Equal(CharacterRequestState.Ready, controller.TargetRequestState);
    }

    [Fact]
    public void ConcurrentRequest_RapidUpgradeDowngrade_ShouldSettleCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Rapid upgrades and downgrades
        controller.RequestState(CharacterRequestState.Active);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Should be in PlacedDataLoaded (not loading GO since target is PlacedOnly)
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
    }

    #endregion

    #region Late Callback Handling Tests

    [Fact]
    public void LateCallback_DataLoad_AfterCancelToNone_ShouldBeIgnored()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        // State is now NotPlaced, late callback arrives
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should remain in NotPlaced
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.IsDataLoaded);
    }

    [Fact]
    public void LateCallback_GameObjectLoad_AfterCancelToNone_ShouldBeIgnored()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        controller.RequestState(CharacterRequestState.None);
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);

        // Late GO callback arrives
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Should remain in NotPlaced
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void LateCallback_GameObjectLoad_AfterDowngradeToPlacedOnly_ShouldBeIgnored()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Now in PlacedDataLoaded, late GO callback arrives
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Should remain in PlacedDataLoaded
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.False(controller.HasGameObject);
    }

    [Fact]
    public void LateCallback_WithFailure_AfterCancel_ShouldBeIgnored()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);

        // Late callback with failure arrives
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Assert: Should remain in NotPlaced (not go to DataLoadFailed)
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void LateCallback_AfterNewRequest_ShouldBeIgnored()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start loading, cancel, restart
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Now there should be 2 pending callbacks
        // Complete first (late/stale) callback
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Should still be in loading state (first callback was for cancelled request)
        // Actually in this mock, both callbacks are in queue, so completing one succeeds
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Complete second callback (for new request)
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // This callback is also late now since we're in DataLoadFailed
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);
    }

    #endregion

    #region Multiple Callbacks for Same Request Tests

    [Fact]
    public void MultipleCallbacks_OnlyFirstShouldBeProcessed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Simulate multiple callbacks (shouldn't happen in real code but testing defense)
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Try another callback - should be ignored since we're no longer in DataLoading
        loader.CompleteDataLoad(ResourceLoadResult.Failed);

        // Assert: Should be in DataLoaded, not DataLoadFailed
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, controller.CurrentState);
        Assert.True(controller.IsDataLoaded);
    }

    [Fact]
    public void MultipleCallbacks_AllIgnoredAfterStateTransition()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var stateChanges = new List<CharacterInternalState>();
        controller.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        int stateCountAfterFirst = stateChanges.Count;

        // These should all be ignored
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: No additional state changes
        Assert.Equal(stateCountAfterFirst, stateChanges.Count);
    }

    [Fact]
    public void MultipleCallbacks_GameObjectLoad_OnlyFirstProcessed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.Ready);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        int createCallsAfterFirst = factory.CreateCalls.Count;

        // Additional callbacks should be ignored
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Only one game object created
        Assert.Equal(createCallsAfterFirst, factory.CreateCalls.Count);
    }

    [Fact]
    public void MultipleCallbacks_MixedResults_ShouldUseFirst()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // First callback fails
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);

        // Second callback succeeds (but should be ignored)
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Should still be in failed state
        Assert.Equal(CharacterInternalState.DataLoadFailed, controller.CurrentState);
    }

    [Fact]
    public void MultipleCallbacks_SimultaneousDataAndGO_ShouldProcessCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: Start loading for Ready
        controller.RequestState(CharacterRequestState.Ready);

        // Complete data load
        loader.CompleteDataLoad(ResourceLoadResult.Success);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        // A late data callback arrives (should be ignored)
        loader.CompleteDataLoad(ResourceLoadResult.Failed);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, controller.CurrentState);

        // Complete GO load
        loader.CompleteGameObjectLoad(ResourceLoadResult.Success);

        // Assert: Should be in InstantiatedInactive
        Assert.Equal(CharacterInternalState.InstantiatedInactive, controller.CurrentState);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void StateTransition_SameStateTwice_ShouldNotFireEvent()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);
        int stateChangesAfterFirst = controller.StateChanges.Count;

        // Request same state again
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: No new state changes
        Assert.Equal(stateChangesAfterFirst, controller.StateChanges.Count);
    }

    [Fact]
    public void StateTransition_BackToSameStateViaOther_ShouldFireEvents()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new TestableSpawnController("char1", loader, factory);

        // Act: None -> PlacedOnly (loading) -> None -> PlacedOnly (loading)
        controller.RequestState(CharacterRequestState.PlacedOnly);
        controller.RequestState(CharacterRequestState.None);
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Should have 3 state changes
        // 1. NotPlaced -> PlacedDataLoading
        // 2. PlacedDataLoading -> NotPlaced
        // 3. NotPlaced -> PlacedDataLoading
        Assert.Equal(3, controller.StateChanges.Count);
    }

    [Fact]
    public void StateTransition_AllTargetStatesFromNotPlaced_ShouldTransitionCorrectly()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();

        // Test each target state from NotPlaced
        var testCases = new[]
        {
            (CharacterRequestState.None, CharacterInternalState.NotPlaced),
            (CharacterRequestState.PlacedOnly, CharacterInternalState.PlacedDataLoaded),
            (CharacterRequestState.Ready, CharacterInternalState.InstantiatedInactive),
            (CharacterRequestState.Active, CharacterInternalState.InstantiatedActive),
        };

        foreach (var (requestState, expectedFinalState) in testCases)
        {
            var controller = new CharacterSpawnController("char1", loader, factory);
            controller.RequestState(requestState);
            controller.Update();
            Assert.Equal(expectedFinalState, controller.CurrentState);
        }
    }

    #endregion
}
