using System;
using System.Collections.Generic;
using Xunit;
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

namespace Tomato.CommandGenerator.Tests;

/// <summary>
/// Entity単位のコマンド実行テスト。
/// VoidHandle引数付きのCommandQueueとCommandの動作確認。
/// </summary>
public class EntityCommandTests
{
    private readonly TestArena _arena = new();

    [Fact]
    public void EntityCommandQueue_ShouldExecuteWithHandle()
    {
        // Arrange
        var queue = new EntityCommandQueue();
        var handle = _arena.CreateHandle(1);
        var receivedHandle = default(VoidHandle);
        var receivedX = 0;
        var receivedY = 0;

        queue.Enqueue<MoveEntityCommand>(cmd =>
        {
            cmd.X = 10;
            cmd.Y = 20;
            cmd.OnExecute = (h, x, y) =>
            {
                receivedHandle = h;
                receivedX = x;
                receivedY = y;
            };
        });

        // Act
        queue.ExecuteCommand(handle);

        // Assert
        Assert.Equal(handle.Index, receivedHandle.Index);
        Assert.Equal(10, receivedX);
        Assert.Equal(20, receivedY);
    }

    [Fact]
    public void EntityCommandQueue_ShouldExecuteMultipleCommands()
    {
        // Arrange
        var queue = new EntityCommandQueue();
        var handle = _arena.CreateHandle(5);
        var order = new List<string>();

        // 低優先度を先にEnqueue
        queue.Enqueue<LowPriorityEntityCommand>(cmd =>
        {
            cmd.OnExecute = h => order.Add($"Low:{h.Index}");
        });

        // 高優先度を後にEnqueue
        queue.Enqueue<HighPriorityEntityCommand>(cmd =>
        {
            cmd.OnExecute = h => order.Add($"High:{h.Index}");
        });

        // Act
        queue.ExecuteCommand(handle);

        // Assert - 高優先度が先に実行される
        Assert.Equal(2, order.Count);
        Assert.Equal("High:5", order[0]);
        Assert.Equal("Low:5", order[1]);
    }

    #region Helper Classes

    private class TestArena : IEntityArena
    {
        public VoidHandle CreateHandle(int index) => new VoidHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}

/// <summary>
/// Entity単位で実行されるコマンドキュー。
/// ExecuteCommand(VoidHandle)でEntityごとにコマンドを実行する。
/// </summary>
[CommandQueue]
public partial class EntityCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(VoidHandle handle);
}

/// <summary>
/// 移動コマンド（Priority = 50）
/// </summary>
[Command<EntityCommandQueue>(Priority = 50)]
public partial class MoveEntityCommand
{
    public int X;
    public int Y;
    public Action<VoidHandle, int, int>? OnExecute;

    public void ExecuteCommand(VoidHandle handle)
    {
        OnExecute?.Invoke(handle, X, Y);
    }
}

/// <summary>
/// 高優先度エンティティコマンド（Priority = 100）
/// </summary>
[Command<EntityCommandQueue>(Priority = 100)]
public partial class HighPriorityEntityCommand
{
    public Action<VoidHandle>? OnExecute;

    public void ExecuteCommand(VoidHandle handle)
    {
        OnExecute?.Invoke(handle);
    }
}

/// <summary>
/// 低優先度エンティティコマンド（Priority = -10）
/// </summary>
[Command<EntityCommandQueue>(Priority = -10)]
public partial class LowPriorityEntityCommand
{
    public Action<VoidHandle>? OnExecute;

    public void ExecuteCommand(VoidHandle handle)
    {
        OnExecute?.Invoke(handle);
    }
}
