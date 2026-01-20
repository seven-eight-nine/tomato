using Tomato.CommandGenerator;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// EnqueueTiming機能のテスト
/// </summary>
public class EnqueueTimingTests
{
    [Fact]
    public void EnqueueTiming_NextWave_IsDefaultValue()
    {
        Assert.Equal(0, (int)EnqueueTiming.NextWave);
    }

    [Fact]
    public void EnqueueTiming_NextFrame_HasValue1()
    {
        Assert.Equal(1, (int)EnqueueTiming.NextFrame);
    }
}

/// <summary>
/// WaveProcessable統合テスト用のキュー
/// </summary>
[CommandQueue]
public partial class WaveTestQueue
{
    [CommandMethod]
    public partial void Execute();
}

/// <summary>
/// WaveProcessable統合テスト用のコマンド
/// </summary>
[Command<WaveTestQueue>(Priority = 0)]
public partial class WaveTestCommand
{
    public int Value;
    public static int ExecutedSum;

    public void Execute()
    {
        ExecutedSum += Value;
    }
}

/// <summary>
/// IWaveProcessable統合テスト
/// </summary>
public class WaveProcessableIntegrationTests
{
    public WaveProcessableIntegrationTests()
    {
        WaveTestCommand.ExecutedSum = 0;
    }

    [Fact]
    public void GeneratedQueue_ShouldImplementIWaveProcessable()
    {
        var queue = new WaveTestQueue();

        Assert.IsAssignableFrom<IWaveProcessable>(queue);
    }

    [Fact]
    public void GeneratedQueue_HasPendingCommands_ShouldReturnFalse_WhenEmpty()
    {
        var queue = new WaveTestQueue();

        Assert.False(queue.HasPendingCommands);
    }

    [Fact]
    public void GeneratedQueue_HasPendingCommands_ShouldReturnTrue_AfterEnqueue()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1);

        Assert.True(queue.HasPendingCommands);
    }

    [Fact]
    public void GeneratedQueue_OnEnqueue_ShouldBeCalledWhenEnqueuing()
    {
        var queue = new WaveTestQueue();
        bool callbackCalled = false;
        queue.OnEnqueue = _ => callbackCalled = true;

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1);

        Assert.True(callbackCalled);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextFrame_ShouldNotAffectCurrentWave()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.MergePendingToCurrentWave();
        queue.Execute();

        // 次フレームキューに入っているのでCurrentには来ない
        Assert.Equal(0, WaveTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextFrame_ShouldBeMergedOnNextFrame()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 5, EnqueueTiming.NextFrame);

        // フレーム開始処理
        queue.MergeNextFrameToPending();
        queue.MergePendingToCurrentWave();
        queue.Execute();

        Assert.Equal(5, WaveTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextWave_ShouldBeProcessedInCurrentFrame()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 3, EnqueueTiming.NextWave);
        queue.MergePendingToCurrentWave();
        queue.Execute();

        Assert.Equal(3, WaveTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_NextFrameCount_ShouldReturnCorrectCount()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 2, EnqueueTiming.NextFrame);

        Assert.Equal(2, queue.NextFrameCount);
    }

    [Fact]
    public void GeneratedQueue_Count_ShouldIncludeNextFrameQueue()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextWave);
        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 2, EnqueueTiming.NextFrame);

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void GeneratedQueue_Clear_ShouldClearNextFrameQueue()
    {
        var queue = new WaveTestQueue();

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.Clear();

        Assert.Equal(0, queue.NextFrameCount);
    }

    [Fact]
    public void WaveProcessor_WithGeneratedQueue_ShouldWork()
    {
        var processor = new WaveProcessor();
        var queue = new WaveTestQueue();
        processor.Register(queue);

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 10);

        // OnEnqueueでアクティブになっているはず
        Assert.Equal(1, processor.ActiveQueueCount);

        var result = processor.ProcessAllWaves(q =>
        {
            ((WaveTestQueue)q).MergePendingToCurrentWave();
            ((WaveTestQueue)q).Execute();
        });

        Assert.Equal(WaveProcessingResult.Completed, result);
        Assert.Equal(10, WaveTestCommand.ExecutedSum);
    }

    [Fact]
    public void WaveProcessor_BeginFrame_ShouldMergeNextFrameCommands()
    {
        var processor = new WaveProcessor();
        var queue = new WaveTestQueue();
        processor.Register(queue);

        queue.Enqueue<WaveTestCommand>(cmd => cmd.Value = 7, EnqueueTiming.NextFrame);

        // まだアクティブではない（OnEnqueueは呼ばれるが、HasPendingはfalse）
        // 次フレームキューはHasPendingに含まれない

        processor.BeginFrame();

        // BeginFrame後にアクティブになるはず
        var result = processor.ProcessAllWaves(q =>
        {
            ((WaveTestQueue)q).MergePendingToCurrentWave();
            ((WaveTestQueue)q).Execute();
        });

        Assert.Equal(WaveProcessingResult.Completed, result);
        Assert.Equal(7, WaveTestCommand.ExecutedSum);
    }
}
