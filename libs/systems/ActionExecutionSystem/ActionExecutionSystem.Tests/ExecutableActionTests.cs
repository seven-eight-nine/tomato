using System;
using Xunit;
using Tomato.ActionExecutionSystem;
using Tomato.ActionSelector;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// IExecutableAction / StandardExecutableAction テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] StandardExecutableActionを作成できる
/// - [x] OnEnterで経過時間がリセットされる
/// - [x] Updateで経過時間とフレームが増加する
/// - [x] TotalFrames経過でIsCompleteがtrueになる
/// - [x] CancelWindow内でCanCancelがtrueになる
/// - [x] CancelWindow外でCanCancelがfalseになる
/// - [x] GetTransitionableJudgmentsはCanCancel時のみ値を返す
/// </summary>
public class ExecutableActionTests
{
    private enum TestCategory
    {
        Upper,
        Lower,
        Movement
    }

    [Fact]
    public void StandardExecutableAction_ShouldBeCreatable()
    {
        var definition = CreateBasicDefinition();
        var action = new StandardExecutableAction<TestCategory>(definition);

        Assert.NotNull(action);
        Assert.Equal("Attack1", action.ActionId);
        Assert.Equal(TestCategory.Upper, action.Category);
    }

    [Fact]
    public void OnEnter_ShouldResetElapsedTicks()
    {
        var definition = CreateBasicDefinition();
        var action = new StandardExecutableAction<TestCategory>(definition);

        action.OnEnter();

        Assert.Equal(0, action.ElapsedTicks);
    }

    [Fact]
    public void Update_ShouldIncrementElapsedTicks()
    {
        var definition = CreateBasicDefinition();
        var action = new StandardExecutableAction<TestCategory>(definition);

        action.OnEnter();
        action.Tick(1);
        action.Tick(1);

        Assert.Equal(2, action.ElapsedTicks);
    }

    [Fact]
    public void IsComplete_ShouldBeTrueWhenTotalFramesReached()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "ShortAction",
            category: TestCategory.Upper,
            totalFrames: 3,
            cancelWindow: new FrameWindow(1, 2));

        var action = new StandardExecutableAction<TestCategory>(definition);

        action.OnEnter();
        Assert.False(action.IsComplete);

        action.Tick(1); // Frame 1
        Assert.False(action.IsComplete);

        action.Tick(1); // Frame 2
        Assert.False(action.IsComplete);

        action.Tick(1); // Frame 3 = TotalFrames
        Assert.True(action.IsComplete);
    }

    [Fact]
    public void CanCancel_ShouldBeTrueInCancelWindow()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(10, 20));

        var action = new StandardExecutableAction<TestCategory>(definition);

        action.OnEnter();

        // Frame 0-9: CancelWindow外
        for (int i = 0; i < 10; i++)
        {
            Assert.False(action.CanCancel, $"Frame {action.ElapsedTicks} should not be cancellable");
            action.Tick(1);
        }

        // Frame 10-20: CancelWindow内
        for (int i = 10; i <= 20; i++)
        {
            Assert.True(action.CanCancel, $"Frame {action.ElapsedTicks} should be cancellable");
            action.Tick(1);
        }

        // Frame 21+: CancelWindow外
        Assert.False(action.CanCancel);
    }

    [Fact]
    public void GetTransitionableJudgments_ShouldReturnEmptyWhenNotCancellable()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(10, 20));

        var judgment = new SimpleJudgment<TestCategory>("Attack2", TestCategory.Upper, ActionPriority.Normal);
        var action = new StandardExecutableAction<TestCategory>(
            definition,
            new IActionJudgment<TestCategory, InputState, GameState>[] { judgment });

        action.OnEnter();

        // Frame 0: CancelWindow外 -> Empty
        var judgments = action.GetTransitionableJudgments();
        Assert.True(judgments.IsEmpty);
    }

    [Fact]
    public void GetTransitionableJudgments_ShouldReturnJudgmentsWhenCancellable()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(5, 20));

        var judgment = new SimpleJudgment<TestCategory>("Attack2", TestCategory.Upper, ActionPriority.Normal);
        var action = new StandardExecutableAction<TestCategory>(
            definition,
            new IActionJudgment<TestCategory, InputState, GameState>[] { judgment });

        action.OnEnter();

        // Move to frame 5 (CancelWindow start)
        for (int i = 0; i < 5; i++)
        {
            action.Tick(1);
        }

        Assert.True(action.CanCancel);
        var judgments = action.GetTransitionableJudgments();
        Assert.Equal(1, judgments.Length);
        Assert.Equal("Attack2", judgments[0].Label);
    }

    [Fact]
    public void MotionData_ShouldBeAccessible()
    {
        var definition = CreateBasicDefinition();
        var action = new StandardExecutableAction<TestCategory>(definition);

        // MotionDataがnullでもOK
        Assert.Null(action.MotionData);
    }

    private static ActionDefinition<TestCategory> CreateBasicDefinition()
    {
        return new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(15, 25));
    }
}
