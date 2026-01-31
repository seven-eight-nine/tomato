using Xunit;
using Tomato.ActionExecutionSystem.MotionGraph;
using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.Tests.MotionGraph;

/// <summary>
/// MotionTransitionCondition テスト
/// </summary>
public class MotionTransitionConditionTests
{
    [Fact]
    public void IsComplete_ShouldReturnTrue_WhenMotionComplete()
    {
        var context = CreateContextAtFrame(60, totalFrames: 60);
        var condition = MotionTransitionCondition.IsComplete();

        Assert.True(condition(context));
    }

    [Fact]
    public void IsComplete_ShouldReturnFalse_WhenMotionNotComplete()
    {
        var context = CreateContextAtFrame(30, totalFrames: 60);
        var condition = MotionTransitionCondition.IsComplete();

        Assert.False(condition(context));
    }

    [Fact]
    public void IsComplete_ShouldReturnTrue_WhenNoCurrentMotion()
    {
        var context = new MotionContext();
        var condition = MotionTransitionCondition.IsComplete();

        Assert.True(condition(context));
    }

    [Fact]
    public void Always_ShouldAlwaysReturnTrue()
    {
        var context = new MotionContext();
        var condition = MotionTransitionCondition.Always();

        Assert.True(condition(context));
    }

    [Fact]
    public void Never_ShouldAlwaysReturnFalse()
    {
        var context = new MotionContext();
        var condition = MotionTransitionCondition.Never();

        Assert.False(condition(context));
    }

    [Fact]
    public void AfterTick_ShouldReturnTrue_WhenFrameReached()
    {
        var context = new MotionContext { ElapsedTicks = 30 };
        var condition = MotionTransitionCondition.AfterTick(30);

        Assert.True(condition(context));
    }

    [Fact]
    public void AfterTick_ShouldReturnFalse_WhenFrameNotReached()
    {
        var context = new MotionContext { ElapsedTicks = 29 };
        var condition = MotionTransitionCondition.AfterTick(30);

        Assert.False(condition(context));
    }

    [Fact]
    public void InTickRange_ShouldReturnTrue_WhenInRange()
    {
        var context = new MotionContext { ElapsedTicks = 35 };
        var condition = MotionTransitionCondition.InTickRange(30, 50);

        Assert.True(condition(context));
    }

    [Fact]
    public void InTickRange_ShouldReturnFalse_WhenOutOfRange()
    {
        var context = new MotionContext { ElapsedTicks = 20 };
        var condition = MotionTransitionCondition.InTickRange(30, 50);

        Assert.False(condition(context));
    }

    [Fact]
    public void InTickRange_ShouldReturnTrue_AtBoundaries()
    {
        var conditionStart = MotionTransitionCondition.InTickRange(30, 50);
        var conditionEnd = MotionTransitionCondition.InTickRange(30, 50);

        Assert.True(conditionStart(new MotionContext { ElapsedTicks = 30 }));
        Assert.True(conditionEnd(new MotionContext { ElapsedTicks = 50 }));
    }

    [Fact]
    public void And_ShouldReturnTrue_WhenAllConditionsTrue()
    {
        var condition = MotionTransitionCondition.And(
            MotionTransitionCondition.Always(),
            MotionTransitionCondition.Always());

        Assert.True(condition(new MotionContext()));
    }

    [Fact]
    public void And_ShouldReturnFalse_WhenAnyConditionFalse()
    {
        var condition = MotionTransitionCondition.And(
            MotionTransitionCondition.Always(),
            MotionTransitionCondition.Never());

        Assert.False(condition(new MotionContext()));
    }

    [Fact]
    public void Or_ShouldReturnTrue_WhenAnyConditionTrue()
    {
        var condition = MotionTransitionCondition.Or(
            MotionTransitionCondition.Never(),
            MotionTransitionCondition.Always());

        Assert.True(condition(new MotionContext()));
    }

    [Fact]
    public void Or_ShouldReturnFalse_WhenAllConditionsFalse()
    {
        var condition = MotionTransitionCondition.Or(
            MotionTransitionCondition.Never(),
            MotionTransitionCondition.Never());

        Assert.False(condition(new MotionContext()));
    }

    #region Helper Methods

    private static MotionContext CreateContextAtFrame(int frame, int totalFrames)
    {
        var timeline = new Sequence();
        var definition = new MotionDefinition("Motion1", totalFrames, timeline);
        var state = new MotionState(definition);
        var context = new MotionContext();

        state.OnEnter(context);
        for (int i = 0; i < frame; i++)
        {
            state.OnTick(context, 1);
        }

        return context;
    }

    #endregion
}
