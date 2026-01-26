using Xunit;
using Tomato.ActionExecutionSystem.MotionGraph;

namespace Tomato.ActionExecutionSystem.Tests.MotionGraph;

/// <summary>
/// MotionContext テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] MotionContextを作成できる
/// - [x] QueryContextが初期化されている
/// - [x] ResetFramesでフレームがリセットされる
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
        Assert.Equal(0, context.ElapsedFrames);
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
    public void ResetFrames_ShouldResetToZero()
    {
        var context = new MotionContext();
        context.ElapsedFrames = 10;

        context.ResetFrames();

        Assert.Equal(0, context.ElapsedFrames);
    }

    [Fact]
    public void AdvanceFrame_ShouldIncrementFrames()
    {
        var context = new MotionContext();

        context.AdvanceFrame();
        Assert.Equal(1, context.ElapsedFrames);

        context.AdvanceFrame();
        Assert.Equal(2, context.ElapsedFrames);
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
        public void OnMotionUpdate(string motionId, int elapsedFrames, float deltaTime) { }
        public void OnMotionEnd(string motionId) { }
    }
}
