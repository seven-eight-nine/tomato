using System;
using System.Collections.Generic;
using Xunit;
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

namespace Tomato.CommandGenerator.Tests;

/// <summary>
/// CommandGenerator統合テスト
/// コマンドキューの基本機能と優先度順の実行をテストする。
/// </summary>
public class CommandIntegrationTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void MessageHandlerQueue_ShouldExecuteEnqueuedCommands()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var executedCount = 0;

        queue.Enqueue<TestCommand>(cmd =>
        {
            cmd.OnExecute = () => executedCount++;
        });

        // Act
        queue.Execute();

        // Assert
        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void MessageHandlerQueue_ShouldExecuteInPriorityOrder()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var order = new List<string>();

        // 低優先度を先にEnqueue
        queue.Enqueue<LowPriorityTestCommand>(cmd =>
        {
            cmd.OnExecute = () => order.Add("Low");
        });

        // 高優先度を後にEnqueue
        queue.Enqueue<HighPriorityTestCommand>(cmd =>
        {
            cmd.OnExecute = () => order.Add("High");
        });

        // Act
        queue.Execute();

        // Assert - 高優先度が先に実行される
        Assert.Equal(2, order.Count);
        Assert.Equal("High", order[0]);
        Assert.Equal("Low", order[1]);
    }

    [Fact]
    public void Commands_ShouldExecuteWithCorrectPriority()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var receivedValue = 0;
        var target = _arena.CreateHandle(1);

        queue.Enqueue<TestDamageCommand>(cmd =>
        {
            cmd.Target = target;
            cmd.Amount = 50;
            cmd.OnExecute = (t, amount) => receivedValue = amount;
        });

        // Act
        queue.Execute();

        // Assert
        Assert.Equal(50, receivedValue);
    }

    [Fact]
    public void MixedPriorityCommands_ShouldExecuteInPriorityOrder()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var order = new List<string>();

        // Priority=-10
        queue.Enqueue<LowPriorityTestCommand>(cmd =>
        {
            cmd.OnExecute = () => order.Add("Low(-10)");
        });

        // Priority=0
        queue.Enqueue<TestCommand>(cmd =>
        {
            cmd.OnExecute = () => order.Add("Default(0)");
        });

        // Priority=50
        queue.Enqueue<TestDamageCommand>(cmd =>
        {
            cmd.Target = _arena.CreateHandle(1);
            cmd.Amount = 10;
            cmd.OnExecute = (_, _) => order.Add("Damage(50)");
        });

        // Priority=100
        queue.Enqueue<HighPriorityTestCommand>(cmd =>
        {
            cmd.OnExecute = () => order.Add("High(100)");
        });

        // Act
        queue.Execute();

        // Assert - 優先度順: High(100) > Damage(50) > Default(0) > Low(-10)
        Assert.Equal(4, order.Count);
        Assert.Equal("High(100)", order[0]);
        Assert.Equal("Damage(50)", order[1]);
        Assert.Equal("Default(0)", order[2]);
        Assert.Equal("Low(-10)", order[3]);
    }

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public AnyHandle CreateHandle(int index) => new AnyHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
