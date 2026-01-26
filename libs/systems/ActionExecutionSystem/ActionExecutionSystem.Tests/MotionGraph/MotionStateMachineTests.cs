using Xunit;
using Tomato.ActionExecutionSystem.MotionGraph;
using Tomato.HierarchicalStateMachine;
using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.Tests.MotionGraph;

/// <summary>
/// MotionStateMachine テスト
/// </summary>
public class MotionStateMachineTests
{
    [Fact]
    public void MotionStateMachine_ShouldBeCreatable()
    {
        var graph = CreateTestGraph();
        var machine = new MotionStateMachine(graph);

        Assert.NotNull(machine);
        Assert.Null(machine.CurrentStateId);
    }

    [Fact]
    public void Initialize_ShouldTransitionToInitialState()
    {
        var graph = CreateTestGraph();
        var machine = new MotionStateMachine(graph);

        machine.Initialize("Motion1");

        Assert.Equal("Motion1", machine.CurrentStateId!.Value.Value);
        Assert.NotNull(machine.CurrentMotionState);
    }

    [Fact]
    public void Update_ShouldAdvanceFrames()
    {
        var graph = CreateTestGraph();
        var machine = new MotionStateMachine(graph);
        machine.Initialize("Motion1");

        machine.Update(0.016f);
        Assert.Equal(1, machine.ElapsedFrames);

        machine.Update(0.016f);
        Assert.Equal(2, machine.ElapsedFrames);
    }

    [Fact]
    public void TryTransitionTo_ShouldSucceed_WhenConditionMet()
    {
        var graph = CreateTestGraphWithTransitions();
        var machine = new MotionStateMachine(graph);
        machine.Initialize("Motion1");

        var result = machine.TryTransitionTo("Motion2");

        Assert.True(result);
        Assert.Equal("Motion2", machine.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void TryTransitionTo_ShouldFail_WhenConditionNotMet()
    {
        var graph = CreateTestGraphWithConditionalTransition();
        var machine = new MotionStateMachine(graph);
        machine.Initialize("Motion1");

        // Transition requires frame >= 30
        var result = machine.TryTransitionTo("Motion2");

        Assert.False(result);
        Assert.Equal("Motion1", machine.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void ForceTransitionTo_ShouldIgnoreConditions()
    {
        var graph = CreateTestGraphWithConditionalTransition();
        var machine = new MotionStateMachine(graph);
        machine.Initialize("Motion1");

        // Force transition regardless of condition
        machine.ForceTransitionTo("Motion2");

        Assert.Equal("Motion2", machine.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void IsCurrentMotionComplete_ShouldReturnCorrectly()
    {
        var graph = CreateTestGraph();
        var machine = new MotionStateMachine(graph);
        machine.Initialize("Motion1");

        Assert.False(machine.IsCurrentMotionComplete());

        // Move to frame 60 (TotalFrames)
        for (int i = 0; i < 60; i++)
        {
            machine.Update(0.016f);
        }

        Assert.True(machine.IsCurrentMotionComplete());
    }

    [Fact]
    public void SetExecutor_ShouldBeCalledOnStateTransitions()
    {
        var graph = CreateTestGraphWithTransitions();
        var executor = new TestMotionExecutor();
        var machine = new MotionStateMachine(graph, executor);
        machine.Initialize("Motion1");

        Assert.True(executor.OnMotionStartCalled);
        Assert.Equal("Motion1", executor.LastMotionId);

        executor.Reset();
        machine.TryTransitionTo("Motion2");

        Assert.True(executor.OnMotionEndCalled);
        Assert.True(executor.OnMotionStartCalled);
        Assert.Equal("Motion2", executor.LastMotionId);
    }

    #region Helper Methods

    private static StateGraph<MotionContext> CreateTestGraph()
    {
        var motion1 = CreateMotionState("Motion1", 60);
        var motion2 = CreateMotionState("Motion2", 50);

        return new StateGraph<MotionContext>()
            .AddState(motion1)
            .AddState(motion2);
    }

    private static StateGraph<MotionContext> CreateTestGraphWithTransitions()
    {
        var motion1 = CreateMotionState("Motion1", 60);
        var motion2 = CreateMotionState("Motion2", 50);

        return new StateGraph<MotionContext>()
            .AddState(motion1)
            .AddState(motion2)
            .AddTransition(new Transition<MotionContext>("Motion1", "Motion2", 1f, MotionTransitionCondition.Always()))
            .AddTransition(new Transition<MotionContext>("Motion2", "Motion1", 1f, MotionTransitionCondition.Always()));
    }

    private static StateGraph<MotionContext> CreateTestGraphWithConditionalTransition()
    {
        var motion1 = CreateMotionState("Motion1", 60);
        var motion2 = CreateMotionState("Motion2", 50);

        return new StateGraph<MotionContext>()
            .AddState(motion1)
            .AddState(motion2)
            .AddTransition(new Transition<MotionContext>("Motion1", "Motion2", 1f, MotionTransitionCondition.AfterFrame(30)));
    }

    private static MotionState CreateMotionState(string motionId, int totalFrames)
    {
        var timeline = new Sequence();
        var definition = new MotionDefinition(motionId, totalFrames, timeline);
        return new MotionState(definition);
    }

    #endregion

    #region Test Helpers

    private class TestMotionExecutor : IMotionExecutor
    {
        public bool OnMotionStartCalled { get; private set; }
        public bool OnMotionUpdateCalled { get; private set; }
        public bool OnMotionEndCalled { get; private set; }
        public string? LastMotionId { get; private set; }

        public void OnMotionStart(string motionId)
        {
            OnMotionStartCalled = true;
            LastMotionId = motionId;
        }

        public void OnMotionUpdate(string motionId, int elapsedFrames, float deltaTime)
        {
            OnMotionUpdateCalled = true;
            LastMotionId = motionId;
        }

        public void OnMotionEnd(string motionId)
        {
            OnMotionEndCalled = true;
            LastMotionId = motionId;
        }

        public void Reset()
        {
            OnMotionStartCalled = false;
            OnMotionUpdateCalled = false;
            OnMotionEndCalled = false;
            LastMotionId = null;
        }
    }

    #endregion
}
