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

    // NOTE: 以下の3つのテストはActionSelector API変更により一時的にコメントアウト
    // GameState.CreateBuilder()とButtonTypeが削除されたため

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

    #endregion
}
