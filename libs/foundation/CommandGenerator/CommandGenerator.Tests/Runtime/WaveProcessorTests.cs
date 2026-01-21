using System;
using Tomato.CommandGenerator;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// WaveProcessorのテスト
/// </summary>
public class WaveProcessorTests
{
    #region Registration Tests

    [Fact]
    public void Register_ShouldAddQueueToRegistered()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();

        processor.Register(queue);

        Assert.Equal(1, processor.RegisteredQueueCount);
    }

    [Fact]
    public void Register_ShouldSetOnEnqueueCallback()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();

        processor.Register(queue);

        Assert.NotNull(queue.OnEnqueue);
    }

    [Fact]
    public void Unregister_ShouldRemoveQueueFromRegistered()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
        processor.Register(queue);

        processor.Unregister(queue);

        Assert.Equal(0, processor.RegisteredQueueCount);
    }

    [Fact]
    public void Unregister_ShouldClearOnEnqueueCallback()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
        processor.Register(queue);

        processor.Unregister(queue);

        Assert.Null(queue.OnEnqueue);
    }

    #endregion

    #region MarkActive Tests

    [Fact]
    public void MarkActive_ShouldAddQueueToActiveSet()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
        processor.Register(queue);

        processor.MarkActive(queue);

        Assert.Equal(1, processor.ActiveQueueCount);
    }

    [Fact]
    public void OnEnqueue_ShouldMarkQueueAsActive()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
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
        var processor = new WaveProcessor();

        Assert.False(processor.HasPendingCommands());
    }

    [Fact]
    public void HasPendingCommands_ShouldReturnTrue_WhenActiveQueueHasPending()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        Assert.True(processor.HasPendingCommands());
    }

    [Fact]
    public void HasPendingCommands_ShouldReturnFalse_WhenActiveQueueIsEmpty()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = false };
        processor.Register(queue);
        processor.MarkActive(queue);

        Assert.False(processor.HasPendingCommands());
    }

    #endregion

    #region ProcessSingleWave Tests

    [Fact]
    public void ProcessSingleWave_ShouldReturnFalse_WhenNoActiveQueues()
    {
        var processor = new WaveProcessor();
        int executedCount = 0;

        bool result = processor.ProcessSingleWave(_ => executedCount++);

        Assert.False(result);
        Assert.Equal(0, executedCount);
    }

    [Fact]
    public void ProcessSingleWave_ShouldExecuteActionForActiveQueues()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int executedCount = 0;

        processor.ProcessSingleWave(_ => executedCount++);

        Assert.Equal(1, executedCount);
    }

    [Fact]
    public void ProcessSingleWave_ShouldCallMergePendingToCurrentWave()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.ProcessSingleWave(_ => { });

        Assert.True(queue.MergePendingToCurrentWaveCalled);
    }

    [Fact]
    public void ProcessSingleWave_ShouldIncrementWaveDepth()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.ProcessSingleWave(_ => { });

        Assert.Equal(1, processor.CurrentWaveDepth);
    }

    [Fact]
    public void ProcessSingleWave_ShouldRemoveEmptyQueuesFromActive()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        // 実行後にキューを空にする
        processor.ProcessSingleWave(_ => queue.HasPendingCommands = false);

        Assert.Equal(0, processor.ActiveQueueCount);
    }

    #endregion

    #region ProcessAllWaves Tests

    [Fact]
    public void ProcessAllWaves_ShouldReturnEmpty_WhenNoCommands()
    {
        var processor = new WaveProcessor();

        var result = processor.ProcessAllWaves(_ => { });

        Assert.Equal(WaveProcessingResult.Empty, result);
    }

    [Fact]
    public void ProcessAllWaves_ShouldReturnCompleted_WhenConverged()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        var result = processor.ProcessAllWaves(_ => queue.HasPendingCommands = false);

        Assert.Equal(WaveProcessingResult.Completed, result);
    }

    [Fact]
    public void ProcessAllWaves_ShouldProcessMultipleWaves()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int waveCount = 0;

        processor.ProcessAllWaves(_ =>
        {
            waveCount++;
            if (waveCount >= 3)
            {
                queue.HasPendingCommands = false;
            }
        });

        Assert.Equal(3, waveCount);
    }

    [Fact]
    public void ProcessAllWaves_ShouldReturnDepthExceeded_WhenMaxDepthReached()
    {
        var processor = new WaveProcessor(maxWaveDepth: 5);
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        bool depthExceededCalled = false;
        processor.OnDepthExceeded = _ => depthExceededCalled = true;

        var result = processor.ProcessAllWaves(_ => { }); // 永遠に収束しない

        Assert.Equal(WaveProcessingResult.DepthExceeded, result);
        Assert.True(depthExceededCalled);
    }

    [Fact]
    public void ProcessAllWaves_ShouldThrowOnRecursiveCall()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        Exception? caughtException = null;

        processor.ProcessAllWaves(_ =>
        {
            try
            {
                processor.ProcessAllWaves(_ => { });
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
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
        processor.Register(queue);

        processor.BeginFrame();

        Assert.True(queue.MergeNextFrameToPendingCalled);
    }

    [Fact]
    public void BeginFrame_ShouldMarkQueueAsActive_WhenHasPendingAfterMerge()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable();
        queue.SetPendingAfterNextFrameMerge = true;
        processor.Register(queue);

        processor.BeginFrame();

        Assert.Equal(1, processor.ActiveQueueCount);
    }

    #endregion

    #region OnWaveStart Tests

    [Fact]
    public void OnWaveStart_ShouldBeCalledWithWaveDepth()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);
        int calledWithDepth = -1;
        processor.OnWaveStart = depth => calledWithDepth = depth;

        processor.ProcessSingleWave(_ => { });

        Assert.Equal(1, calledWithDepth);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_ShouldClearActiveQueues()
    {
        var processor = new WaveProcessor();
        var queue = new MockWaveProcessable { HasPendingCommands = true };
        processor.Register(queue);
        processor.MarkActive(queue);

        processor.Clear();

        Assert.Equal(0, processor.ActiveQueueCount);
    }

    #endregion

    #region Mock Implementation

    private class MockWaveProcessable : IWaveProcessable
    {
        public bool HasPendingCommands { get; set; }
        public bool MergePendingToCurrentWaveCalled { get; private set; }
        public bool MergeNextFrameToPendingCalled { get; private set; }
        public bool SetPendingAfterNextFrameMerge { get; set; }
        public Action<IWaveProcessable>? OnEnqueue { get; set; } = null!;

        public void MergePendingToCurrentWave()
        {
            MergePendingToCurrentWaveCalled = true;
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
