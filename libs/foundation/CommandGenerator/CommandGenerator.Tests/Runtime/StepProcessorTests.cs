using System;
using System.Collections.Generic;
using System.Threading;
using Tomato.CommandGenerator;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// StepProcessorのテスト
/// </summary>
public class StepProcessorTests
{
    #region Registration Tests

    [Fact]
    public void Register_ShouldAddQueueToRegistered()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();

        processor.Register(queue);

        Assert.Equal(1, processor.RegisteredQueueCount);
    }

    [Fact]
    public void Register_ShouldSetOnEnqueueCallback()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();

        processor.Register(queue);

        Assert.NotNull(queue.OnEnqueue);
    }

    [Fact]
    public void Unregister_ShouldRemoveQueueFromRegistered()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        processor.Register(queue);

        processor.Unregister(queue);

        Assert.Equal(0, processor.RegisteredQueueCount);
    }

    [Fact]
    public void Unregister_ShouldClearOnEnqueueCallback()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        processor.Register(queue);

        processor.Unregister(queue);

        Assert.Null(queue.OnEnqueue);
    }

    #endregion

    #region MarkActive Tests

    [Fact]
    public void MarkActive_ShouldAddQueueToActiveSet()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        processor.Register(queue);

        processor.MarkActive(queue);

        Assert.Equal(1, processor.ActiveQueueCount);
    }

    [Fact]
    public void OnEnqueue_ShouldMarkQueueAsActive()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        processor.Register(queue);

        // OnEnqueueを呼び出す（Enqueue時に自動で呼ばれる想定）
        queue.OnEnqueue?.Invoke(queue);

        Assert.Equal(1, processor.ActiveQueueCount);
    }

    #endregion

    #region HasPendingCommands Tests

    [Fact]
    public void HasPendingCommands_ShouldReturnFalse_WhenNoActiveQueues()
    {
        var processor = new StepProcessor();

        Assert.False(processor.HasPendingCommands());
    }

    [Fact]
    public void HasPendingCommands_ShouldReturnTrue_WhenActiveQueueHasPending()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        Assert.True(processor.HasPendingCommands());
    }

    [Fact]
    public void HasPendingCommands_ShouldReturnFalse_WhenActiveQueueIsEmpty()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = false };
        processor.Register(queue);
        processor.MarkActive(queue);

        Assert.False(processor.HasPendingCommands());
    }

    #endregion

    #region ProcessSingleStep Tests

    [Fact]
    public void ProcessSingleStep_ShouldReturnFalse_WhenNoActiveQueues()
    {
        var processor = new StepProcessor();
        int executedCount = 0;

        bool result = processor.ProcessSingleStep(_ => executedCount++);

        Assert.False(result);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void ProcessSingleStep_ShouldExecuteActionForActiveQueues()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int executedCount = 0;

        processor.ProcessSingleStep(_ => executedCount++);

        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void ProcessSingleStep_ShouldCallMergePendingToCurrentStep()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.ProcessSingleStep(_ => { });

        Assert.True(queue.MergePendingToCurrentStepCalled);
    }

    [Fact]
    public void ProcessSingleStep_ShouldIncrementStepDepth()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.ProcessSingleStep(_ => { });

        Assert.Equal(1, processor.CurrentStepDepth);
    }

    [Fact]
    public void ProcessSingleStep_ShouldRemoveEmptyQueuesFromActive()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        // 実行後にキューを空にする
        processor.ProcessSingleStep(_ => queue.HasPendingCommands = false);

        Assert.Equal(0, processor.ActiveQueueCount);
    }

    #endregion

    #region ProcessAllSteps Tests

    [Fact]
    public void ProcessAllSteps_ShouldReturnEmpty_WhenNoCommands()
    {
        var processor = new StepProcessor();

        var result = processor.ProcessAllSteps(_ => { });

        Assert.Equal(StepProcessingResult.Empty, result);
    }

    [Fact]
    public void ProcessAllSteps_ShouldReturnCompleted_WhenConverged()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        var result = processor.ProcessAllSteps(_ => queue.HasPendingCommands = false);

        Assert.Equal(StepProcessingResult.Completed, result);
    }

    [Fact]
    public void ProcessAllSteps_ShouldProcessMultipleSteps()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int stepCount = 0;

        processor.ProcessAllSteps(_ =>
        {
            stepCount++;
            if (stepCount >= 3)
            {
                queue.HasPendingCommands = false;
            }
        });

        Assert.Equal(3, stepCount);
    }

    [Fact]
    public void ProcessAllSteps_ShouldReturnDepthExceeded_WhenMaxDepthReached()
    {
        var processor = new StepProcessor(maxStepDepth: 5);
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        bool depthExceededCalled = false;
        processor.OnDepthExceeded = _ => depthExceededCalled = true;

        var result = processor.ProcessAllSteps(_ => { }); // 永遠に収束しない

        Assert.Equal(StepProcessingResult.DepthExceeded, result);
        Assert.True(depthExceededCalled);
    }

    [Fact]
    public void ProcessAllSteps_ShouldThrowOnRecursiveCall()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        Exception? caughtException = null;

        processor.ProcessAllSteps(_ =>
        {
            try
            {
                processor.ProcessAllSteps(_ => { });
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            queue.HasPendingCommands = false;
        });

        Assert.NotNull(caughtException);
        Assert.IsType<InvalidOperationException>(caughtException);
    }

    #endregion

    #region BeginFrame Tests

    [Fact]
    public void BeginFrame_ShouldCallMergeNextFrameToPending()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        processor.Register(queue);

        processor.BeginFrame();

        Assert.True(queue.MergeNextFrameToPendingCalled);
    }

    [Fact]
    public void BeginFrame_ShouldMarkQueueAsActive_WhenHasPendingAfterMerge()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable();
        queue.SetPendingAfterNextFrameMerge = true;
        processor.Register(queue);

        processor.BeginFrame();

        Assert.Equal(1, processor.ActiveQueueCount);
    }

    #endregion

    #region OnStepStart Tests

    [Fact]
    public void OnStepStart_ShouldBeCalledWithStepDepth()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int calledWithDepth = -1;
        processor.OnStepStart = depth => calledWithDepth = depth;

        processor.ProcessSingleStep(_ => { });

        Assert.Equal(1, calledWithDepth);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_ShouldClearActiveQueues()
    {
        var processor = new StepProcessor();
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.Clear();

        Assert.Equal(0, processor.ActiveQueueCount);
    }

    #endregion

    #region EnableParallelProcessing Tests

    [Fact]
    public void EnableParallelProcessing_ShouldDefaultToFalse()
    {
        var processor = new StepProcessor();

        Assert.False(processor.EnableParallelProcessing);
    }

    [Fact]
    public void ProcessSingleStep_WithParallelProcessing_ShouldProcessAllQueues()
    {
        var processor = new StepProcessor { EnableParallelProcessing = true };
        var queue1 = new MockStepProcessable { HasPendingCommands = true };
        var queue2 = new MockStepProcessable { HasPendingCommands = true };
        var queue3 = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue1);
        processor.Register(queue2);
        processor.Register(queue3);
        processor.MarkActive(queue1);
        processor.MarkActive(queue2);
        processor.MarkActive(queue3);
        var processedQueues = new List<IStepProcessable>();
        var lockObj = new object();

        processor.ProcessSingleStep(q =>
        {
            lock (lockObj)
            {
                processedQueues.Add(q);
            }
            ((MockStepProcessable)q).HasPendingCommands = false;
        });

        Assert.Equal(3, processedQueues.Count);
        Assert.Contains(queue1, processedQueues);
        Assert.Contains(queue2, processedQueues);
        Assert.Contains(queue3, processedQueues);
    }

    [Fact]
    public void ProcessAllSteps_WithParallelProcessing_ShouldMaintainStepSynchronization()
    {
        var processor = new StepProcessor { EnableParallelProcessing = true };
        var queue1 = new MockStepProcessable { HasPendingCommands = true };
        var queue2 = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue1);
        processor.Register(queue2);
        processor.MarkActive(queue1);
        processor.MarkActive(queue2);
        var stepStartCount = 0;
        processor.OnStepStart = _ => Interlocked.Increment(ref stepStartCount);
        int processCount = 0;
        var lockObj = new object();

        var result = processor.ProcessAllSteps(q =>
        {
            lock (lockObj)
            {
                processCount++;
                if (processCount >= 4) // 2 queues * 2 steps
                {
                    ((MockStepProcessable)q).HasPendingCommands = false;
                }
            }
        });

        Assert.Equal(StepProcessingResult.Completed, result);
        Assert.True(stepStartCount >= 1);
    }

    [Fact]
    public void ProcessSingleStep_WithParallelProcessing_SingleQueue_ShouldProcessSequentially()
    {
        var processor = new StepProcessor { EnableParallelProcessing = true };
        var queue = new MockStepProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int executedCount = 0;

        processor.ProcessSingleStep(_ =>
        {
            executedCount++;
            queue.HasPendingCommands = false;
        });

        Assert.Equal(1, executedCount);
    }

    #endregion

    #region Mock Implementation

    private class MockStepProcessable : IStepProcessable
    {
        public bool HasPendingCommands { get; set; }
        public bool MergePendingToCurrentStepCalled { get; private set; }
        public bool MergeNextFrameToPendingCalled { get; private set; }
        public bool SetPendingAfterNextFrameMerge { get; set; }
        public Action<IStepProcessable>? OnEnqueue { get; set; } = null!;

        public void MergePendingToCurrentStep()
        {
            MergePendingToCurrentStepCalled = true;
        }

        public void MergeNextFrameToPending()
        {
            MergeNextFrameToPendingCalled = true;
            if (SetPendingAfterNextFrameMerge)
            {
                HasPendingCommands = true;
            }
        }
    }

    #endregion
}
