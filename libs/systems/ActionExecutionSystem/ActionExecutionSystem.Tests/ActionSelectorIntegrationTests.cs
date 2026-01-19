using System;
using System.Collections.Generic;
using Xunit;
using Tomato.ActionExecutionSystem;
using Tomato.ActionSelector;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// ActionSelector統合テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] ActionSelectorで選択されたアクションをActionStateMachineで実行できる
/// - [x] ActionSelectorの遷移先ジャッジメントを活用してコンボ遷移ができる
/// - [x] CancelWindow内でのみ遷移が可能
/// - [x] ActionDefinitionRegistryからアクションを取得できる
/// </summary>
public class ActionSelectorIntegrationTests
{
    private enum TestCategory
    {
        Upper,
        Lower
    }

    [Fact]
    public void Integration_SelectAndExecuteAction()
    {
        // Arrange
        var registry = new ActionDefinitionRegistry<TestCategory>();
        registry.Register(CreateAttackDefinition("Attack1", TestCategory.Upper, 30, 10, 20));

        var judgment = new SimpleJudgment<TestCategory>("Attack1", TestCategory.Upper, ActionPriority.Normal);
        var engine = new ActionSelector<TestCategory, InputState, GameState>();
        var machine = new ActionStateMachine<TestCategory>();

        var state = GameState.CreateBuilder()
            .WithButtonPressed(ButtonType.Attack)
            .Build();

        // Act
        var result = ProcessFrameHelper(engine, new[] { judgment }, state);

        // Assert: ActionSelectorがアクションを選択
        Assert.True(result.TryGetRequested(TestCategory.Upper, out var requested));
        Assert.Equal("Attack1", requested!.ActionId);

        // Act: ActionStateMachineでアクションを実行
        var definition = registry.Get(requested.ActionId)!;
        var action = new StandardExecutableAction<TestCategory>(definition);
        machine.StartAction(TestCategory.Upper, action);

        // Assert: アクションが実行中
        Assert.True(machine.IsRunning(TestCategory.Upper));
        Assert.Equal("Attack1", machine.GetCurrentAction(TestCategory.Upper)!.ActionId);
    }

    [Fact]
    public void Integration_ComboTransition()
    {
        // Arrange
        var registry = new ActionDefinitionRegistry<TestCategory>();
        registry.Register(CreateAttackDefinition("Attack1", TestCategory.Upper, 30, 5, 20));
        registry.Register(CreateAttackDefinition("Attack2", TestCategory.Upper, 25, 10, 20));

        // Attack1 -> Attack2 への遷移ジャッジメント
        var attack2Judgment = new SimpleJudgment<TestCategory>("Attack2", TestCategory.Upper, ActionPriority.Normal);
        var attack1Definition = registry.Get("Attack1")!;
        var attack1Action = new StandardExecutableAction<TestCategory>(
            attack1Definition,
            new IActionJudgment<TestCategory, InputState, GameState>[] { attack2Judgment });

        var machine = new ActionStateMachine<TestCategory>();
        var engine = new ActionSelector<TestCategory, InputState, GameState>();
        var state = GameState.CreateBuilder()
            .WithButtonPressed(ButtonType.Attack)
            .Build();

        // Act: Attack1を開始
        machine.StartAction(TestCategory.Upper, attack1Action);

        // Frame 0-4: CancelWindow外 (CancelWindowは5から)
        Assert.False(machine.CanCancel(TestCategory.Upper)); // Frame 0
        var transitions = attack1Action.GetTransitionableJudgments();
        Assert.True(transitions.IsEmpty);

        for (int i = 0; i < 5; i++)
        {
            machine.Update(0.016f);
        }

        // Frame 5: CancelWindow開始
        Assert.True(attack1Action.CanCancel);

        // 遷移先ジャッジメントが取得できる
        transitions = attack1Action.GetTransitionableJudgments();
        Assert.Equal(1, transitions.Length);
        Assert.Equal("Attack2", transitions[0].ActionId);

        // Act: 遷移先でActionSelectorを実行
        var result = ProcessFrameHelper(engine, transitions.ToArray(), state);

        // Assert: Attack2が選択される
        Assert.True(result.TryGetRequested(TestCategory.Upper, out var requested));
        Assert.Equal("Attack2", requested!.ActionId);

        // Attack2を開始
        var attack2Definition = registry.Get("Attack2")!;
        var attack2Action = new StandardExecutableAction<TestCategory>(attack2Definition);
        machine.StartAction(TestCategory.Upper, attack2Action);

        Assert.Equal("Attack2", machine.GetCurrentAction(TestCategory.Upper)!.ActionId);
    }

    [Fact]
    public void Integration_MultipleCategoriesRunConcurrently()
    {
        // Arrange
        var registry = new ActionDefinitionRegistry<TestCategory>();
        registry.Register(CreateAttackDefinition("Punch", TestCategory.Upper, 20, 5, 15));
        registry.Register(CreateAttackDefinition("Kick", TestCategory.Lower, 25, 8, 20));

        var punchJudgment = new SimpleJudgment<TestCategory>("Punch", TestCategory.Upper, ActionPriority.Normal);
        var kickJudgment = new SimpleJudgment<TestCategory>("Kick", TestCategory.Lower, ActionPriority.Normal);

        var engine = new ActionSelector<TestCategory, InputState, GameState>();
        var machine = new ActionStateMachine<TestCategory>();

        var state = GameState.CreateBuilder()
            .WithButtonPressed(ButtonType.Attack | ButtonType.Kick)
            .Build();

        // Act: 両カテゴリのアクションを選択
        var result = ProcessFrameHelper(engine, new IActionJudgment<TestCategory, InputState, GameState>[] { punchJudgment, kickJudgment }, state);

        Assert.True(result.TryGetRequested(TestCategory.Upper, out var upperRequested));
        Assert.True(result.TryGetRequested(TestCategory.Lower, out var lowerRequested));

        // 両方のアクションを開始
        machine.StartAction(TestCategory.Upper, new StandardExecutableAction<TestCategory>(registry.Get("Punch")!));
        machine.StartAction(TestCategory.Lower, new StandardExecutableAction<TestCategory>(registry.Get("Kick")!));

        // Assert: 両カテゴリが同時に実行中
        Assert.True(machine.IsRunning(TestCategory.Upper));
        Assert.True(machine.IsRunning(TestCategory.Lower));

        // Update
        machine.Update(0.016f);

        Assert.Equal(1, machine.GetCurrentAction(TestCategory.Upper)!.ElapsedFrames);
        Assert.Equal(1, machine.GetCurrentAction(TestCategory.Lower)!.ElapsedFrames);
    }

    [Fact]
    public void ActionDefinitionRegistry_ShouldGetDefinitionById()
    {
        var registry = new ActionDefinitionRegistry<TestCategory>();
        var definition = CreateAttackDefinition("Test", TestCategory.Upper, 30, 10, 20);

        registry.Register(definition);

        var retrieved = registry.Get("Test");
        Assert.NotNull(retrieved);
        Assert.Equal("Test", retrieved!.ActionId);
    }

    [Fact]
    public void ActionDefinitionRegistry_ShouldReturnNullForUnknownId()
    {
        var registry = new ActionDefinitionRegistry<TestCategory>();

        var retrieved = registry.Get("Unknown");
        Assert.Null(retrieved);
    }

    #region Helper Methods

    private static ActionDefinition<TestCategory> CreateAttackDefinition(
        string actionId, TestCategory category, int totalFrames, int cancelStart, int cancelEnd)
    {
        return new ActionDefinition<TestCategory>(
            actionId: actionId,
            category: category,
            totalFrames: totalFrames,
            cancelWindow: new FrameWindow(cancelStart, cancelEnd),
            hitboxWindow: new FrameWindow(5, 10));
    }

    private static SelectionResult<TestCategory, InputState, GameState> ProcessFrameHelper(
        ActionSelector<TestCategory, InputState, GameState> engine,
        IActionJudgment<TestCategory, InputState, GameState>[] judgments,
        GameState state)
    {
        var list = new JudgmentList<TestCategory, InputState, GameState>();
        foreach (var j in judgments) list.Add(j);
        var frameState = new FrameState<InputState, GameState>(state.Input, state);
        return engine.ProcessFrame(list, in frameState);
    }

    #endregion
}

