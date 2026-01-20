using System;
using Xunit;
using Tomato.ActionExecutionSystem;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// ActionDefinition テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] ActionDefinitionを作成できる
/// - [x] 必須パラメータが正しく設定される
/// - [x] オプショナルパラメータが正しく設定される
/// - [x] HitboxWindow/InvincibleWindowがnullでも動作する
/// </summary>
public class ActionDefinitionTests
{
    private enum TestCategory
    {
        Upper,
        Lower,
        Movement
    }

    [Fact]
    public void ActionDefinition_ShouldBeCreatable()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(15, 25));

        Assert.NotNull(definition);
    }

    [Fact]
    public void ActionDefinition_ShouldStoreRequiredParameters()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(15, 25));

        Assert.Equal("Attack1", definition.ActionId);
        Assert.Equal(TestCategory.Upper, definition.Category);
        Assert.Equal(30, definition.TotalFrames);
        Assert.Equal(15, definition.CancelWindow.Start);
        Assert.Equal(25, definition.CancelWindow.End);
    }

    [Fact]
    public void ActionDefinition_ShouldStoreHitboxWindow()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Attack1",
            category: TestCategory.Upper,
            totalFrames: 30,
            cancelWindow: new FrameWindow(15, 25),
            hitboxWindow: new FrameWindow(5, 10));

        Assert.NotNull(definition.HitboxWindow);
        Assert.Equal(5, definition.HitboxWindow.Value.Start);
        Assert.Equal(10, definition.HitboxWindow.Value.End);
    }

    [Fact]
    public void ActionDefinition_ShouldStoreInvincibleWindow()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Dodge",
            category: TestCategory.Movement,
            totalFrames: 20,
            cancelWindow: new FrameWindow(15, 19),
            invincibleWindow: new FrameWindow(3, 12));

        Assert.NotNull(definition.InvincibleWindow);
        Assert.Equal(3, definition.InvincibleWindow.Value.Start);
        Assert.Equal(12, definition.InvincibleWindow.Value.End);
    }

    [Fact]
    public void ActionDefinition_ShouldAllowNullOptionalParameters()
    {
        var definition = new ActionDefinition<TestCategory>(
            actionId: "Idle",
            category: TestCategory.Movement,
            totalFrames: 60,
            cancelWindow: new FrameWindow(0, 60));

        Assert.Null(definition.HitboxWindow);
        Assert.Null(definition.InvincibleWindow);
        Assert.Null(definition.MotionData);
    }
}
