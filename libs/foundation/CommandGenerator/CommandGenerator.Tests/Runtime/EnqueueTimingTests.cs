using Tomato.CommandGenerator;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// EnqueueTiming機能のテスト
/// </summary>
public class EnqueueTimingTests
{
    [Fact]
    public void EnqueueTiming_NextStep_IsDefaultValue()
    {
        Assert.Equal(0, (int)EnqueueTiming.NextStep);
    }

    [Fact]
    public void EnqueueTiming_NextFrame_HasValue1()
    {
        Assert.Equal(1, (int)EnqueueTiming.NextFrame);
    }
}

/// <summary>
/// StepProcessable統合テスト用のキュー
/// </summary>
[CommandQueue]
public partial class StepTestQueue
{
    [CommandMethod]
    public partial void Execute();
}

/// <summary>
/// StepProcessable統合テスト用のコマンド
/// </summary>
[Command<StepTestQueue>(Priority = 0)]
public partial class StepTestCommand
{
    public int Value;
    public static int ExecutedSum;

    public void Execute()
    {
        ExecutedSum += Value;
    }
}

/// <summary>
/// IStepProcessable統合テスト
/// </summary>
public class StepProcessableIntegrationTests
{
    public StepProcessableIntegrationTests()
    {
        StepTestCommand.ExecutedSum = 0;
    }

    [Fact]
    public void GeneratedQueue_ShouldImplementIStepProcessable()
    {
        var queue = new StepTestQueue();

        Assert.IsAssignableFrom<IStepProcessable>(queue);
    }

    [Fact]
    public void GeneratedQueue_HasPendingCommands_ShouldReturnFalse_WhenEmpty()
    {
        var queue = new StepTestQueue();

        Assert.False(queue.HasPendingCommands);
    }

    [Fact]
    public void GeneratedQueue_HasPendingCommands_ShouldReturnTrue_AfterEnqueue()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1);

        Assert.True(queue.HasPendingCommands);
    }

    [Fact]
    public void GeneratedQueue_OnEnqueue_ShouldBeCalledWhenEnqueuing()
    {
        var queue = new StepTestQueue();
        bool callbackCalled = false;
        queue.OnEnqueue = _ => callbackCalled = true;

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1);

        Assert.True(callbackCalled);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextFrame_ShouldNotAffectCurrentStep()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.MergePendingToCurrentStep();
        queue.Execute();

        // 次フレームキューに入っているのでCurrentには来ない
        Assert.Equal(0, StepTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextFrame_ShouldBeMergedOnNextFrame()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 5, EnqueueTiming.NextFrame);

        // フレーム開始処理
        queue.MergeNextFrameToPending();
        queue.MergePendingToCurrentStep();
        queue.Execute();

        Assert.Equal(5, StepTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_EnqueueNextStep_ShouldBeProcessedInCurrentFrame()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 3, EnqueueTiming.NextStep);
        queue.MergePendingToCurrentStep();
        queue.Execute();

        Assert.Equal(3, StepTestCommand.ExecutedSum);
    }

    [Fact]
    public void GeneratedQueue_NextFrameCount_ShouldReturnCorrectCount()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 2, EnqueueTiming.NextFrame);

        Assert.Equal(2, queue.NextFrameCount);
    }

    [Fact]
    public void GeneratedQueue_Count_ShouldIncludeNextFrameQueue()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextStep);
        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 2, EnqueueTiming.NextFrame);

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void GeneratedQueue_Clear_ShouldClearNextFrameQueue()
    {
        var queue = new StepTestQueue();

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 1, EnqueueTiming.NextFrame);
        queue.Clear();

        Assert.Equal(0, queue.NextFrameCount);
    }

    [Fact]
    public void StepProcessor_WithGeneratedQueue_ShouldWork()
    {
        var processor = new StepProcessor();
        var queue = new StepTestQueue();
        processor.Register(queue);

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 10);

        // OnEnqueueでアクティブになっているはず
        Assert.Equal(1, processor.ActiveQueueCount);

        var result = processor.ProcessAllSteps(q =>
        {
            ((StepTestQueue)q).MergePendingToCurrentStep();
            ((StepTestQueue)q).Execute();
        });

        Assert.Equal(StepProcessingResult.Completed, result);
        Assert.Equal(10, StepTestCommand.ExecutedSum);
    }

    [Fact]
    public void StepProcessor_BeginFrame_ShouldMergeNextFrameCommands()
    {
        var processor = new StepProcessor();
        var queue = new StepTestQueue();
        processor.Register(queue);

        queue.Enqueue<StepTestCommand>(cmd => cmd.Value = 7, EnqueueTiming.NextFrame);

        // まだアクティブではない（OnEnqueueは呼ばれるが、HasPendingはfalse）
        // 次フレームキューはHasPendingに含まれない

        processor.BeginFrame();

        // BeginFrame後にアクティブになるはず
        var result = processor.ProcessAllSteps(q =>
        {
            ((StepTestQueue)q).MergePendingToCurrentStep();
            ((StepTestQueue)q).Execute();
        });

        Assert.Equal(StepProcessingResult.Completed, result);
        Assert.Equal(7, StepTestCommand.ExecutedSum);
    }
}
