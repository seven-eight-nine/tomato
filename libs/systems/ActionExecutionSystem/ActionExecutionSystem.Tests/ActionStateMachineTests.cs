using System;
using System.Collections.Generic;
using Xunit;
using Tomato.ActionExecutionSystem;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// ActionStateMachine テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] ActionStateMachineを作成できる
/// - [x] 各カテゴリの初期状態はnull
/// - [x] StartActionでアクションを開始できる
/// - [x] StartAction時にOnEnterが呼ばれる
/// - [x] Updateでアクションが更新される
/// - [x] アクション完了時にnullに戻る
/// - [x] 新しいアクション開始時に古いアクションのOnExitが呼ばれる
/// - [x] IsRunningでアクション実行中か判定できる
/// - [x] CanCancelでキャンセル可能か判定できる
/// - [x] Executorが登録されている場合、コールバックが呼ばれる
/// </summary>
public class ActionStateMachineTests
{
    private enum TestCategory
    {
        Upper,
        Lower,
        Movement
    }

    [Fact]
    public void ActionStateMachine_ShouldBeCreatable()
    {
        var machine = new ActionStateMachine<TestCategory>();

        Assert.NotNull(machine);
    }

    [Fact]
    public void GetCurrentAction_ShouldReturnNull_Initially()
    {
        var machine = new ActionStateMachine<TestCategory>();

        Assert.Null(machine.GetCurrentAction(TestCategory.Upper));
        Assert.Null(machine.GetCurrentAction(TestCategory.Lower));
        Assert.Null(machine.GetCurrentAction(TestCategory.Movement));
    }

    [Fact]
    public void StartAction_ShouldSetCurrentAction()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action = CreateAction("Attack1", TestCategory.Upper, 30);

        machine.StartAction(TestCategory.Upper, action);

        Assert.NotNull(machine.GetCurrentAction(TestCategory.Upper));
        Assert.Equal("Attack1", machine.GetCurrentAction(TestCategory.Upper)!.ActionId);
    }

    [Fact]
    public void StartAction_ShouldCallOnEnter()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action = new TestableAction("Attack1", TestCategory.Upper);

        machine.StartAction(TestCategory.Upper, action);

        Assert.True(action.OnEnterCalled);
    }

    [Fact]
    public void Update_ShouldUpdateAction()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action = CreateAction("Attack1", TestCategory.Upper, 30);

        machine.StartAction(TestCategory.Upper, action);
        machine.Update(0.016f);

        Assert.Equal(1, action.ElapsedFrames);
    }

    [Fact]
    public void Update_ShouldClearCompletedAction()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action = CreateAction("ShortAction", TestCategory.Upper, 3);

        machine.StartAction(TestCategory.Upper, action);

        machine.Update(0.016f); // Frame 1
        Assert.NotNull(machine.GetCurrentAction(TestCategory.Upper));

        machine.Update(0.016f); // Frame 2
        Assert.NotNull(machine.GetCurrentAction(TestCategory.Upper));

        machine.Update(0.016f); // Frame 3 = TotalFrames -> Complete
        Assert.Null(machine.GetCurrentAction(TestCategory.Upper));
    }

    [Fact]
    public void StartAction_ShouldCallOnExit_WhenReplacingAction()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action1 = new TestableAction("Attack1", TestCategory.Upper);
        var action2 = new TestableAction("Attack2", TestCategory.Upper);

        machine.StartAction(TestCategory.Upper, action1);
        machine.StartAction(TestCategory.Upper, action2);

        Assert.True(action1.OnExitCalled);
        Assert.True(action2.OnEnterCalled);
    }

    [Fact]
    public void IsRunning_ShouldReturnTrue_WhenActionIsRunning()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var action = CreateAction("Attack1", TestCategory.Upper, 30);

        Assert.False(machine.IsRunning(TestCategory.Upper));

        machine.StartAction(TestCategory.Upper, action);

        Assert.True(machine.IsRunning(TestCategory.Upper));
        Assert.False(machine.IsRunning(TestCategory.Lower));
    }

    [Fact]
    public void CanCancel_ShouldReturnTrue_WhenInCancelWindow()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(5, 20));
        var action = new StandardExecutableAction<TestCategory>(definition);

        machine.StartAction(TestCategory.Upper, action);

        Assert.False(machine.CanCancel(TestCategory.Upper)); // Frame 0

        for (int i = 0; i < 5; i++)
        {
            machine.Update(0.016f);
        }

        Assert.True(machine.CanCancel(TestCategory.Upper)); // Frame 5
    }

    [Fact]
    public void RegisterExecutor_ShouldReceiveCallbacks()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var executor = new TestableExecutor();
        machine.RegisterExecutor(TestCategory.Upper, executor);

        var action = CreateAction("Attack1", TestCategory.Upper, 3);

        machine.StartAction(TestCategory.Upper, action);
        Assert.True(executor.OnActionStartCalled);

        machine.Update(0.016f);
        Assert.True(executor.OnActionUpdateCalled);

        machine.Update(0.016f);
        machine.Update(0.016f); // Complete
        Assert.True(executor.OnActionEndCalled);
    }

    [Fact]
    public void Update_ShouldUpdateMultipleCategoriesIndependently()
    {
        var machine = new ActionStateMachine<TestCategory>();
        var upperAction = CreateAction("Attack1", TestCategory.Upper, 30);
        var lowerAction = CreateAction("Kick1", TestCategory.Lower, 20);

        machine.StartAction(TestCategory.Upper, upperAction);
        machine.StartAction(TestCategory.Lower, lowerAction);

        machine.Update(0.016f);

        Assert.Equal(1, upperAction.ElapsedFrames);
        Assert.Equal(1, lowerAction.ElapsedFrames);
    }

    #region Helper Classes

    private static StandardExecutableAction<TestCategory> CreateAction(string actionId, TestCategory category, int totalFrames)
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: actionId,
            category: category,
            totalFrames: totalFrames,
            cancelWindow: new FrameWindow(totalFrames / 2, totalFrames - 1));

        var action = new StandardExecutableAction<TestCategory>(definition);
        action.OnEnter();
        return action;
    }

    private class TestableAction : IExecutableAction<TestCategory>
    {
        public string ActionId { get; }
        public TestCategory Category { get; }
        public float ElapsedTime { get; private set; }
        public int ElapsedFrames { get; private set; }
        public bool IsComplete => false;
        public bool CanCancel => true;
        public IMotionData? MotionData => null;

        public bool OnEnterCalled { get; private set; }
        public bool OnExitCalled { get; private set; }

        public TestableAction(string actionId, TestCategory category)
        {
            ActionId = actionId;
            Category = category;
        }

        public void OnEnter() => OnEnterCalled = true;
        public void OnExit() => OnExitCalled = true;
        public void Update(float deltaTime) => ElapsedFrames++;
        public ReadOnlySpan<Tomato.ActionSelector.IActionJudgment<TestCategory, Tomato.ActionSelector.InputState, Tomato.ActionSelector.GameState>> GetTransitionableJudgments() => default;
    }

    private class TestableExecutor : IActionExecutor<TestCategory>
    {
        public bool OnActionStartCalled { get; private set; }
        public bool OnActionUpdateCalled { get; private set; }
        public bool OnActionEndCalled { get; private set; }

        public void OnActionStart(IExecutableAction<TestCategory> action) => OnActionStartCalled = true;
        public void OnActionUpdate(IExecutableAction<TestCategory> action, float deltaTime) => OnActionUpdateCalled = true;
        public void OnActionEnd(IExecutableAction<TestCategory> action) => OnActionEndCalled = true;
    }

    #endregion
}
