using Xunit;
using Tomato.ActionExecutionSystem.MotionGraph;

namespace Tomato.ActionExecutionSystem.Tests.MotionGraph;

/// <summary>
/// MotionContext テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] MotionContextを作成できる
/// - [x] QueryContextが初期化されている
/// - [x] ResetTicksでフレームがリセットされる
/// - [x] AdvanceFrameでフレームが増加する
/// - [x] Executorを設定できる
/// </summary>
public class MotionContextTests
{
    [Fact]
    public void MotionContext_ShouldBeCreatable()
    {
        var context = new MotionContext();

        Assert.NotNull(context);
        Assert.Equal(0, context.ElapsedTicks);
        Assert.Null(context.CurrentMotionState);
        Assert.Null(context.Executor);
    }

    [Fact]
    public void QueryContext_ShouldBeInitialized()
    {
        var context = new MotionContext();

        Assert.NotNull(context.QueryContext);
    }

    [Fact]
    public void ResetTicks_ShouldResetToZero()
    {
        var context = new MotionContext();
        context.ElapsedTicks = 10;

        context.ResetTicks();

        Assert.Equal(0, context.ElapsedTicks);
    }

    [Fact]
    public void AdvanceFrame_ShouldIncrementFrames()
    {
        var context = new MotionContext();

        context.AdvanceTicks(1);
        Assert.Equal(1, context.ElapsedTicks);

        context.AdvanceTicks(1);
        Assert.Equal(2, context.ElapsedTicks);
    }

    [Fact]
    public void Executor_ShouldBeSettable()
    {
        var context = new MotionContext();
        var executor = new TestMotionExecutor();

        context.Executor = executor;

        Assert.Equal(executor, context.Executor);
    }

    private class TestMotionExecutor : IMotionExecutor
    {
        public void OnMotionStart(string motionId) { }
        public void OnMotionTick(string motionId, int elapsedTicks, int deltaTicks) { }
        public void OnMotionEnd(string motionId) { }
    }
}
