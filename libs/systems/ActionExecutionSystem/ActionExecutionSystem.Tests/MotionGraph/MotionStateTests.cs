using Xunit;
using Tomato.ActionExecutionSystem.MotionGraph;
using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.Tests.MotionGraph;

/// <summary>
/// MotionState テスト
/// </summary>
public class MotionStateTests
{
    [Fact]
    public void MotionState_ShouldBeCreatable()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);

        Assert.NotNull(state);
        Assert.Equal("Motion1", state.Id.Value);
        Assert.Equal(definition, state.Definition);
    }

    [Fact]
    public void OnEnter_ShouldResetElapsedFrames()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();
        context.ElapsedTicks = 10;

        state.OnEnter(context);

        Assert.Equal(0, context.ElapsedTicks);
    }

    [Fact]
    public void OnEnter_ShouldSetCurrentMotionState()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);

        Assert.Equal(state, context.CurrentMotionState);
    }

    [Fact]
    public void OnEnter_ShouldNotifyExecutor()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();
        var executor = new TestMotionExecutor();
        context.Executor = executor;

        state.OnEnter(context);

        Assert.True(executor.OnMotionStartCalled);
        Assert.Equal("Motion1", executor.LastMotionId);
    }

    [Fact]
    public void OnUpdate_ShouldIncrementElapsedFrames()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        state.OnTick(context, 1);

        Assert.Equal(1, context.ElapsedTicks);
    }

    [Fact]
    public void OnUpdate_ShouldQueryTimeline()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        state.OnTick(context, 1);

        // QueryContextが更新されている（ResultFrame = currentFrame + deltaFrames = 1 + 1 = 2）
        Assert.Equal(2, context.QueryContext.ResultFrame);
    }

    [Fact]
    public void OnUpdate_ShouldNotifyExecutor()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();
        var executor = new TestMotionExecutor();
        context.Executor = executor;

        state.OnEnter(context);
        state.OnTick(context, 1);

        Assert.True(executor.OnMotionTickCalled);
        Assert.Equal(1, executor.LastElapsedFrames);
    }

    [Fact]
    public void OnExit_ShouldNotifyExecutor()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();
        var executor = new TestMotionExecutor();
        context.Executor = executor;

        state.OnEnter(context);
        state.OnExit(context);

        Assert.True(executor.OnMotionEndCalled);
    }

    [Fact]
    public void OnExit_ShouldClearCurrentMotionState()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        state.OnExit(context);

        Assert.Null(context.CurrentMotionState);
    }

    [Fact]
    public void IsComplete_ShouldReturnFalse_WhenFramesLessThanTotal()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        for (int i = 0; i < 30; i++)
        {
            state.OnTick(context, 1);
        }

        Assert.False(state.IsComplete(context));
    }

    [Fact]
    public void IsComplete_ShouldReturnTrue_WhenFramesEqualTotal()
    {
        var definition = CreateTestDefinition("Motion1", 60);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        for (int i = 0; i < 60; i++)
        {
            state.OnTick(context, 1);
        }

        Assert.True(state.IsComplete(context));
    }

    #region Helper Methods

    private static MotionDefinition CreateTestDefinition(string motionId, int totalFrames)
    {
        var timeline = new Sequence();
        return new MotionDefinition(motionId, totalFrames, timeline);
    }

    #endregion

    #region Test Helpers

    private class TestMotionExecutor : IMotionExecutor
    {
        public bool OnMotionStartCalled { get; private set; }
        public bool OnMotionTickCalled { get; private set; }
        public bool OnMotionEndCalled { get; private set; }
        public string? LastMotionId { get; private set; }
        public int LastElapsedFrames { get; private set; }

        public void OnMotionStart(string motionId)
        {
            OnMotionStartCalled = true;
            LastMotionId = motionId;
        }

        public void OnMotionTick(string motionId, int elapsedTicks, int deltaTicks)
        {
            OnMotionTickCalled = true;
            LastMotionId = motionId;
            LastElapsedFrames = elapsedTicks;
        }

        public void OnMotionEnd(string motionId)
        {
            OnMotionEndCalled = true;
            LastMotionId = motionId;
        }
    }

    #endregion
}
