using System;
using System.Collections.Generic;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.CommandGenerator;

namespace Tomato.EntityHandleSystem.Tests.Attributes;

/// <summary>
/// HasCommandQueueAttribute のテスト
/// </summary>
public class HasCommandQueueTests
{
    [Fact]
    public void Arena_ShouldCreateWithoutQueueArgument()
    {
        // Arrange & Act - Arenaを作成（キューはEntity単位で内部管理）
        var arena = new QueueEntityArena();

        // Assert
        Assert.NotNull(arena);
    }

    [Fact]
    public void Handle_ShouldAccessCommandQueueViaProperty()
    {
        // Arrange
        var arena = new QueueEntityArena();
        var handle = arena.Create();

        // Act
        var queue = handle.TestGameCommandQueue;

        // Assert
        Assert.NotNull(queue);
    }

    [Fact]
    public void EachEntity_ShouldHaveOwnCommandQueue()
    {
        // Arrange
        var arena = new QueueEntityArena();
        var handle1 = arena.Create();
        var handle2 = arena.Create();

        // Act
        var queue1 = handle1.TestGameCommandQueue;
        var queue2 = handle2.TestGameCommandQueue;

        // Assert - 各Entityが独自のキューを持つ
        Assert.NotNull(queue1);
        Assert.NotNull(queue2);
        Assert.NotSame(queue1, queue2);
    }

    [Fact]
    public void Handle_ShouldEnqueueAndExecuteCommand()
    {
        // Arrange
        var arena = new QueueEntityArena();
        var handle = arena.Create();
        var executedX = 0;
        var executedY = 0;
        AnyHandle executedHandle = default;

        // Act
        handle.TestGameCommandQueue.Enqueue<TestMoveCommand>(cmd =>
        {
            cmd.X = 10;
            cmd.Y = 20;
            cmd.OnExecute = (h, x, y) =>
            {
                executedHandle = h;
                executedX = x;
                executedY = y;
            };
        });

        handle.TestGameCommandQueue.ExecuteCommand(handle.ToAnyHandle());

        // Assert
        Assert.Equal(10, executedX);
        Assert.Equal(20, executedY);
        Assert.Equal(handle.ToAnyHandle().Index, executedHandle.Index);
    }

    [Fact]
    public void MultipleEntities_ShouldHaveIndependentQueues()
    {
        // Arrange
        var arena = new QueueEntityArena();
        var handle1 = arena.Create();
        var handle2 = arena.Create();
        var executionOrder = new List<string>();

        // Act - 各EntityのキューにEnqueue
        handle1.TestGameCommandQueue.Enqueue<TestMoveCommand>(cmd =>
        {
            cmd.X = 1;
            cmd.Y = 1;
            cmd.OnExecute = (_, _, _) => executionOrder.Add("Entity1");
        });

        handle2.TestGameCommandQueue.Enqueue<TestMoveCommand>(cmd =>
        {
            cmd.X = 2;
            cmd.Y = 2;
            cmd.OnExecute = (_, _, _) => executionOrder.Add("Entity2");
        });

        // Entity1のキューだけ実行
        handle1.TestGameCommandQueue.ExecuteCommand(handle1.ToAnyHandle());

        // Assert - Entity1だけ実行されている
        Assert.Single(executionOrder);
        Assert.Equal("Entity1", executionOrder[0]);

        // Entity2のキューも実行
        handle2.TestGameCommandQueue.ExecuteCommand(handle2.ToAnyHandle());

        // Assert - 両方実行されている
        Assert.Equal(2, executionOrder.Count);
        Assert.Equal("Entity2", executionOrder[1]);
    }

    [Fact]
    public void Arena_WithCapacity_ShouldWork()
    {
        // Arrange & Act
        var arena = new QueueEntityArena(64);

        // Assert
        Assert.NotNull(arena);
        Assert.Equal(64, arena.Capacity);
    }

    [Fact]
    public void DifferentEntityTypes_ShouldHaveOwnQueues()
    {
        // Arrange
        var playerArena = new QueueEntityArena();
        var enemyArena = new QueueEntity2Arena();

        var playerHandle = playerArena.Create();
        var enemyHandle = enemyArena.Create();

        // Assert - 異なるEntityタイプも独自のキューを持つ
        Assert.NotNull(playerHandle.TestGameCommandQueue);
        Assert.NotNull(enemyHandle.TestGameCommandQueue);
        Assert.NotSame(playerHandle.TestGameCommandQueue, enemyHandle.TestGameCommandQueue);
    }

    [Fact]
    public void Entity_WithMultipleCommandQueues_ShouldAccessBothQueues()
    {
        // Arrange
        var arena = new MultiQueueEntityArena();
        var handle = arena.Create();

        // Act - 両方のキューにアクセス
        var gameQueue = handle.TestGameCommandQueue;
        var aiQueue = handle.TestAICommandQueue;

        // Assert
        Assert.NotNull(gameQueue);
        Assert.NotNull(aiQueue);
        Assert.NotSame(gameQueue, aiQueue);
    }

    [Fact]
    public void Entity_WithMultipleCommandQueues_ShouldExecuteIndependently()
    {
        // Arrange
        var arena = new MultiQueueEntityArena();
        var handle = arena.Create();
        var gameExecuted = false;
        var aiExecuted = false;
        string? aiDecision = null;

        // Act - 両方のキューにEnqueue
        handle.TestGameCommandQueue.Enqueue<TestMoveCommand>(cmd =>
        {
            cmd.X = 10;
            cmd.Y = 20;
            cmd.OnExecute = (_, _, _) => gameExecuted = true;
        });

        handle.TestAICommandQueue.Enqueue<TestThinkCommand>(cmd =>
        {
            cmd.Decision = "Attack";
            cmd.OnExecute = (_, decision) =>
            {
                aiExecuted = true;
                aiDecision = decision;
            };
        });

        // GameQueueだけ実行
        handle.TestGameCommandQueue.ExecuteCommand(handle.ToAnyHandle());

        // Assert - GameQueueだけ実行されている
        Assert.True(gameExecuted);
        Assert.False(aiExecuted);

        // AIQueueも実行
        handle.TestAICommandQueue.ExecuteAICommand(handle.ToAnyHandle());

        // Assert - 両方実行されている
        Assert.True(aiExecuted);
        Assert.Equal("Attack", aiDecision);
    }

    [Fact]
    public void MultipleEntities_WithMultipleQueues_ShouldBeIndependent()
    {
        // Arrange
        var arena = new MultiQueueEntityArena();
        var handle1 = arena.Create();
        var handle2 = arena.Create();

        // Assert - 各Entityが独自のキューを持つ
        Assert.NotSame(handle1.TestGameCommandQueue, handle2.TestGameCommandQueue);
        Assert.NotSame(handle1.TestAICommandQueue, handle2.TestAICommandQueue);
    }
}

// テスト用のCommandQueue（AnyHandle引数付き）
[CommandQueue]
public partial class TestGameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// テスト用のMoveコマンド
[Command<TestGameCommandQueue>(Priority = 50)]
public partial class TestMoveCommand
{
    public int X;
    public int Y;
    public Action<AnyHandle, int, int>? OnExecute;

    public void ExecuteCommand(AnyHandle handle)
    {
        OnExecute?.Invoke(handle, X, Y);
    }
}

// テスト用Entity with CommandQueue
[Entity(InitialCapacity = 32)]
[HasCommandQueue(typeof(TestGameCommandQueue))]
public partial class QueueEntity
{
    public int X;
    public int Y;
}

// 別のテスト用Entity with 同じCommandQueue
[Entity(InitialCapacity = 32)]
[HasCommandQueue(typeof(TestGameCommandQueue))]
public partial class QueueEntity2
{
    public int Health;
}

// 2つ目のCommandQueue定義
[CommandQueue]
public partial class TestAICommandQueue
{
    [CommandMethod]
    public partial void ExecuteAICommand(AnyHandle handle);
}

// AIコマンド
[Command<TestAICommandQueue>(Priority = 10)]
public partial class TestThinkCommand
{
    public string? Decision;
    public Action<AnyHandle, string?>? OnExecute;

    public void ExecuteAICommand(AnyHandle handle)
    {
        OnExecute?.Invoke(handle, Decision);
    }
}

// 複数CommandQueueを持つEntity
[Entity(InitialCapacity = 32)]
[HasCommandQueue(typeof(TestGameCommandQueue))]
[HasCommandQueue(typeof(TestAICommandQueue))]
public partial class MultiQueueEntity
{
    public int X;
    public int Y;
}
