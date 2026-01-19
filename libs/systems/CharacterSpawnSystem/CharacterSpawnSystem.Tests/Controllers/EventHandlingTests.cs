using System;
using System.Collections.Generic;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Controllers;

/// <summary>
/// Event handling tests - t-wada style thorough coverage
/// Focuses on StateChanged event behavior, subscriber management, and exception handling
/// </summary>
public class EventHandlingTests
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

    private class EventTracker
    {
        public List<StateChangedEventArgs> Events { get; } = new List<StateChangedEventArgs>();
        public List<object> Senders { get; } = new List<object>();
        public int CallCount { get; private set; } = 0;

        public void Handler(object sender, StateChangedEventArgs e)
        {
            CallCount++;
            Senders.Add(sender);
            Events.Add(e);
        }
    }

    private class ThrowingEventTracker
    {
        public int CallCount { get; private set; } = 0;
        public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Test exception");

        public void Handler(object sender, StateChangedEventArgs e)
        {
            CallCount++;
            throw ExceptionToThrow;
        }
    }

    #endregion

    #region StateChanged Not Fired When State Doesn't Change Tests

    [Fact]
    public void StateChanged_NotFired_WhenRequestSameTargetState()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        controller.RequestState(CharacterRequestState.PlacedOnly);
        int eventsAfterFirst = tracker.CallCount;

        // Act: Request same state again
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: No new events
        Assert.Equal(eventsAfterFirst, tracker.CallCount);
    }

    [Fact]
    public void StateChanged_NotFired_WhenRequestNone_AlreadyInNotPlaced()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        // Act: Request None when already in NotPlaced
        controller.RequestState(CharacterRequestState.None);

        // Assert: No events fired
        Assert.Equal(0, tracker.CallCount);
    }

    [Fact]
    public void StateChanged_NotFired_OnUpdate_WhenStable()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        controller.RequestState(CharacterRequestState.PlacedOnly);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        // Act: Multiple updates in stable state
        controller.Update();
        controller.Update();
        controller.Update();

        // Assert: No events fired
        Assert.Equal(0, tracker.CallCount);
    }

    [Fact]
    public void StateChanged_NotFired_WhenInternalStateUnchanged()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        controller.RequestState(CharacterRequestState.PlacedOnly);
        Assert.Equal(1, tracker.CallCount); // NotPlaced -> PlacedDataLoading

        // Act: Update while still loading (no state change)
        controller.Update();

        // Assert: Still only 1 event
        Assert.Equal(1, tracker.CallCount);
    }

    [Fact]
    public void StateChanged_OnlyFired_OnActualTransition()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);
        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);
        loader.CompleteDataLoad(ResourceLoadResult.Success);

        // Assert: Exactly 2 transitions
        Assert.Equal(2, tracker.CallCount);
        Assert.Equal(CharacterInternalState.NotPlaced, tracker.Events[0].OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, tracker.Events[0].NewState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, tracker.Events[1].OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, tracker.Events[1].NewState);
    }

    #endregion

    #region Exception in Event Subscriber Doesn't Break State Machine Tests

    [Fact]
    public void ExceptionInSubscriber_StateMachine_ContinuesNormally()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var thrower = new ThrowingEventTracker();
        controller.StateChanged += thrower.Handler;

        // Act & Assert: Exception bubbles up but state still changes
        var exception = Assert.Throws<InvalidOperationException>(() =>
            controller.RequestState(CharacterRequestState.PlacedOnly));

        Assert.Equal("Test exception", exception.Message);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
    }

    [Fact]
    public void ExceptionInSubscriber_StateTransition_Completed()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        bool exceptionThrown = false;
        controller.StateChanged += (s, e) =>
        {
            if (!exceptionThrown)
            {
                exceptionThrown = true;
                throw new Exception("First subscriber throws");
            }
        };

        // Act
        try
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
        }
        catch
        {
            // Expected
        }

        // Assert: State was updated before exception
        Assert.Equal(CharacterInternalState.PlacedDataLoading, controller.CurrentState);
        Assert.Equal(CharacterRequestState.PlacedOnly, controller.TargetRequestState);
    }

    [Fact]
    public void ExceptionInSubscriber_CanContinue_AfterCatch()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        bool shouldThrow = true;
        controller.StateChanged += (s, e) =>
        {
            if (shouldThrow && e.NewState == CharacterInternalState.PlacedDataLoading)
            {
                throw new Exception("Subscriber exception");
            }
        };

        // First transition throws
        try
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
        }
        catch { }

        // Disable throwing
        shouldThrow = false;

        // Act: Continue operations
        controller.RequestState(CharacterRequestState.None);

        // Assert: State machine recovered
        Assert.Equal(CharacterInternalState.NotPlaced, controller.CurrentState);
    }

    [Fact]
    public void ExceptionInSubscriber_DoesNotAffect_InternalState()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        int callCount = 0;
        controller.StateChanged += (s, e) =>
        {
            callCount++;
            if (callCount == 2) // Throw on second transition
            {
                throw new Exception("Mid-sequence exception");
            }
        };

        // Act
        try
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
        }
        catch { }

        // Assert: Should have processed first transition, second threw
        Assert.Equal(2, callCount);
    }

    #endregion

    #region Multiple Subscribers All Called Tests

    [Fact]
    public void MultipleSubscribers_AllCalled_InOrder()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var callOrder = new List<int>();
        controller.StateChanged += (s, e) => callOrder.Add(1);
        controller.StateChanged += (s, e) => callOrder.Add(2);
        controller.StateChanged += (s, e) => callOrder.Add(3);

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: All called in subscription order
        Assert.Equal(new[] { 1, 2, 3 }, callOrder);
    }

    [Fact]
    public void MultipleSubscribers_AllReceive_SameEventArgs()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker1 = new EventTracker();
        var tracker2 = new EventTracker();
        var tracker3 = new EventTracker();

        controller.StateChanged += tracker1.Handler;
        controller.StateChanged += tracker2.Handler;
        controller.StateChanged += tracker3.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: All received same event data
        Assert.Single(tracker1.Events);
        Assert.Single(tracker2.Events);
        Assert.Single(tracker3.Events);

        Assert.Equal(tracker1.Events[0].OldState, tracker2.Events[0].OldState);
        Assert.Equal(tracker2.Events[0].OldState, tracker3.Events[0].OldState);
        Assert.Equal(tracker1.Events[0].NewState, tracker2.Events[0].NewState);
        Assert.Equal(tracker2.Events[0].NewState, tracker3.Events[0].NewState);
    }

    [Fact]
    public void MultipleSubscribers_AllReceive_CorrectSender()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker1 = new EventTracker();
        var tracker2 = new EventTracker();

        controller.StateChanged += tracker1.Handler;
        controller.StateChanged += tracker2.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Both received correct sender
        Assert.Same(controller, tracker1.Senders[0]);
        Assert.Same(controller, tracker2.Senders[0]);
    }

    [Fact]
    public void MultipleSubscribers_OneThrows_OthersMayNotBeCalled()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var calledBefore = false;
        var calledAfter = false;

        controller.StateChanged += (s, e) => calledBefore = true;
        controller.StateChanged += (s, e) => throw new Exception("Middle throws");
        controller.StateChanged += (s, e) => calledAfter = true;

        // Act
        try
        {
            controller.RequestState(CharacterRequestState.PlacedOnly);
        }
        catch { }

        // Assert: First was called, exception stopped propagation to third
        Assert.True(calledBefore);
        Assert.False(calledAfter); // Standard .NET event behavior
    }

    [Fact]
    public void MultipleSubscribers_SameHandler_CalledMultipleTimes()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;
        controller.StateChanged += tracker.Handler; // Same handler twice

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Called twice for one transition
        Assert.Equal(2, tracker.CallCount);
    }

    [Fact]
    public void MultipleSubscribers_ForMultipleTransitions_AllCalled()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker1 = new EventTracker();
        var tracker2 = new EventTracker();

        controller.StateChanged += tracker1.Handler;
        controller.StateChanged += tracker2.Handler;

        // Act: This causes multiple transitions
        controller.RequestState(CharacterRequestState.Ready);

        // Assert: All subscribers called for all transitions
        Assert.Equal(3, tracker1.CallCount); // NotPlaced -> Loading -> Loaded -> GOLoading
        Assert.Equal(3, tracker2.CallCount);
    }

    #endregion

    #region Unsubscribe Works Correctly Tests

    [Fact]
    public void Unsubscribe_StopsReceiving_Events()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        controller.RequestState(CharacterRequestState.PlacedOnly);
        Assert.Equal(1, tracker.CallCount);

        // Act: Unsubscribe
        controller.StateChanged -= tracker.Handler;
        controller.RequestState(CharacterRequestState.None);

        // Assert: No more events received
        Assert.Equal(1, tracker.CallCount);
    }

    [Fact]
    public void Unsubscribe_OneOfMany_OthersStillCalled()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker1 = new EventTracker();
        var tracker2 = new EventTracker();
        var tracker3 = new EventTracker();

        controller.StateChanged += tracker1.Handler;
        controller.StateChanged += tracker2.Handler;
        controller.StateChanged += tracker3.Handler;

        // Unsubscribe middle one
        controller.StateChanged -= tracker2.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: First and third called, second not
        Assert.Equal(1, tracker1.CallCount);
        Assert.Equal(0, tracker2.CallCount);
        Assert.Equal(1, tracker3.CallCount);
    }

    [Fact]
    public void Unsubscribe_AllSubscribers_NoEventsFired()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker1 = new EventTracker();
        var tracker2 = new EventTracker();

        controller.StateChanged += tracker1.Handler;
        controller.StateChanged += tracker2.Handler;

        controller.StateChanged -= tracker1.Handler;
        controller.StateChanged -= tracker2.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: No events received
        Assert.Equal(0, tracker1.CallCount);
        Assert.Equal(0, tracker2.CallCount);
    }

    [Fact]
    public void Unsubscribe_NonSubscribed_NoError()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();

        // Act: Unsubscribe handler that was never subscribed
        var exception = Record.Exception(() => controller.StateChanged -= tracker.Handler);

        // Assert: No exception
        Assert.Null(exception);
    }

    [Fact]
    public void Unsubscribe_Twice_NoError()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        // Act: Unsubscribe twice
        controller.StateChanged -= tracker.Handler;
        var exception = Record.Exception(() => controller.StateChanged -= tracker.Handler);

        // Assert: No exception
        Assert.Null(exception);
    }

    [Fact]
    public void Unsubscribe_Resubscribe_Works()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        controller.RequestState(CharacterRequestState.PlacedOnly);
        Assert.Equal(1, tracker.CallCount);

        controller.StateChanged -= tracker.Handler;
        controller.RequestState(CharacterRequestState.None);
        Assert.Equal(1, tracker.CallCount); // Still 1

        // Act: Resubscribe
        controller.StateChanged += tracker.Handler;
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Events received again
        Assert.Equal(2, tracker.CallCount);
    }

    [Fact]
    public void Unsubscribe_DuplicateHandler_RemovesOne()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;
        controller.StateChanged += tracker.Handler; // Added twice

        // Act: Unsubscribe once
        controller.StateChanged -= tracker.Handler;
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Still called once (one subscription remains)
        Assert.Equal(1, tracker.CallCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void NoSubscribers_StateTransition_WorksNormally()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        loader.AutoCompleteGameObjectLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        // Act: No subscribers, just state transitions
        controller.RequestState(CharacterRequestState.Active);
        controller.Update();

        // Assert: Works fine
        Assert.Equal(CharacterInternalState.InstantiatedActive, controller.CurrentState);
    }

    [Fact]
    public void EventArgs_ContainsCorrect_StateInformation()
    {
        // Arrange
        var loader = new MockResourceLoader();
        loader.AutoCompleteDataLoad = true;
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        var tracker = new EventTracker();
        controller.StateChanged += tracker.Handler;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Verify full transition sequence
        Assert.Equal(2, tracker.Events.Count);

        // First transition: NotPlaced -> PlacedDataLoading
        Assert.Equal(CharacterInternalState.NotPlaced, tracker.Events[0].OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, tracker.Events[0].NewState);

        // Second transition: PlacedDataLoading -> PlacedDataLoaded
        Assert.Equal(CharacterInternalState.PlacedDataLoading, tracker.Events[1].OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, tracker.Events[1].NewState);
    }

    [Fact]
    public void EventArgs_Immutable_CannotBeModified()
    {
        // Arrange
        var loader = new MockResourceLoader();
        var factory = new MockGameObjectFactory();
        var controller = new CharacterSpawnController("char1", loader, factory);

        StateChangedEventArgs? capturedArgs = null;
        controller.StateChanged += (s, e) => capturedArgs = e;

        // Act
        controller.RequestState(CharacterRequestState.PlacedOnly);

        // Assert: Args are read-only (properties have private setters)
        Assert.NotNull(capturedArgs);
        Assert.Equal(CharacterInternalState.NotPlaced, capturedArgs.OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, capturedArgs.NewState);
    }

    #endregion
}
