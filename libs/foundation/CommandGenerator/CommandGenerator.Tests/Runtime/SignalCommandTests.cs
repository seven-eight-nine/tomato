using System;
using System.Collections.Generic;
using Xunit;

namespace Tomato.CommandGenerator.Tests;

/// <summary>
/// シグナルコマンドのテスト
/// </summary>
public class SignalCommandTests
{
    [Fact]
    public void SignalCommand_EnqueueOnlyOnce_SecondEnqueueIsIgnored()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        int executeCount = 0;

        // Act - 同じシグナルコマンドを3回エンキュー
        var result1 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        var result2 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        var result3 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);

        queue.Execute();

        // Assert - 最初のEnqueueのみ成功
        Assert.True(result1);
        Assert.False(result2);
        Assert.False(result3);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void SignalCommand_AfterClear_CanEnqueueAgain()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        int executeCount = 0;

        // Act - エンキュー→実行（クリア）→再度エンキュー
        queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.Execute();

        var result = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.Execute();

        // Assert
        Assert.True(result);
        Assert.Equal(2, executeCount);
    }

    [Fact]
    public void SignalCommand_DifferentTypes_CanEnqueueBoth()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var executedCommands = new List<string>();

        // Act - 異なるタイプのシグナルコマンドは両方エンキューできる
        var result1 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("Signal"));
        var result2 = queue.Enqueue<HighPrioritySignalCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("HighPriority"));

        queue.Execute();

        // Assert - 両方成功
        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal(2, executedCommands.Count);
        // 優先度順（HighPrioritySignalCommand = 100, SignalCommand = 0）
        Assert.Equal("HighPriority", executedCommands[0]);
        Assert.Equal("Signal", executedCommands[1]);
    }

    [Fact]
    public void SignalCommand_MixedWithNormalCommands()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        var executedCommands = new List<string>();

        // Act - 通常コマンドとシグナルコマンドを混在
        queue.Enqueue<TestCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("Normal1"));
        queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("Signal1"));
        queue.Enqueue<TestCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("Normal2"));
        queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executedCommands.Add("Signal2")); // 無視される

        queue.Execute();

        // Assert - 通常コマンドは複数、シグナルコマンドは1つ
        Assert.Equal(3, executedCommands.Count);
        Assert.Contains("Normal1", executedCommands);
        Assert.Contains("Normal2", executedCommands);
        Assert.Contains("Signal1", executedCommands);
        Assert.DoesNotContain("Signal2", executedCommands);
    }

    [Fact]
    public void SignalCommand_ForceClear_ResetsSignalTracking()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        int executeCount = 0;

        // Act - エンキュー→ForceClear→再度エンキュー
        queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.ForceClear();

        var result = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.Execute();

        // Assert
        Assert.True(result);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void SignalCommand_Clear_ResetsSignalTracking()
    {
        // Arrange
        var queue = new MessageHandlerQueue();
        int executeCount = 0;

        // Act - エンキュー→Clear→再度エンキュー
        queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.Clear();

        var result = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++);
        queue.Execute();

        // Assert
        Assert.True(result);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void SignalCommand_IsSignalProperty_ReturnsTrue()
    {
        // Arrange
        var command = new SignalCommand();

        // Assert
        Assert.True(command.IsSignalForMessageHandlerQueue);
    }

    [Fact]
    public void NormalCommand_IsSignalProperty_ReturnsFalse()
    {
        // Arrange
        var command = new TestCommand();

        // Assert
        Assert.False(command.IsSignalForMessageHandlerQueue);
    }

    [Fact]
    public void SignalCommand_NextFrameTiming_AlsoTracked()
    {
        // Arrange
        var stepProcessor = new StepProcessor(10);
        var queue = new MessageHandlerQueue();
        stepProcessor.Register(queue);
        int executeCount = 0;

        // Act - NextFrame タイミングでエンキュー
        var result1 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++, EnqueueTiming.NextFrame);
        var result2 = queue.Enqueue<SignalCommand>(cmd => cmd.OnExecute = () => executeCount++, EnqueueTiming.NextFrame);

        stepProcessor.BeginFrame();
        stepProcessor.ProcessAllSteps(_ => queue.Execute());

        // Assert - 最初のみ成功
        Assert.True(result1);
        Assert.False(result2);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void SignalCommand_EnqueueReturnsBoolean()
    {
        // Arrange
        var queue = new MessageHandlerQueue();

        // Act
        var success = queue.Enqueue<SignalCommand>(_ => { });
        var ignored = queue.Enqueue<SignalCommand>(_ => { });

        // Assert
        Assert.True(success);
        Assert.False(ignored);
    }
}
