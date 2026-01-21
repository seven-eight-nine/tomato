using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline;
using Tomato.CommandGenerator;

namespace Tomato.EntitySystem.Phases;

/// <summary>
/// メッセージ処理システム。
/// WaveProcessorを使用してMessageHandlerQueueをWave単位で処理する。
/// </summary>
public sealed class MessageSystem : ISerialSystem
{
    private readonly WaveProcessor _waveProcessor;
    private readonly MessageHandlerQueue _queue;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// 最後の処理結果。
    /// </summary>
    public MessagePhaseResult LastResult { get; private set; }

    /// <summary>
    /// MessageSystemを生成する。
    /// </summary>
    /// <param name="waveProcessor">Wave処理用プロセッサ</param>
    /// <param name="queue">メッセージハンドラキュー</param>
    public MessageSystem(WaveProcessor waveProcessor, MessageHandlerQueue queue)
    {
        _waveProcessor = waveProcessor ?? throw new ArgumentNullException(nameof(waveProcessor));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));

        // WaveProcessorにキューを登録
        _waveProcessor.Register(_queue);
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // WaveProcessorを使用してWave処理
        var result = _waveProcessor.ProcessAllWaves(q =>
        {
            if (q is MessageHandlerQueue mhq)
            {
                mhq.Execute();
            }
        });

        LastResult = new MessagePhaseResult(
            _waveProcessor.CurrentWaveDepth,
            result == WaveProcessingResult.DepthExceeded);
    }
}

/// <summary>
/// メッセージ処理フェーズの結果。
/// </summary>
public readonly struct MessagePhaseResult
{
    /// <summary>処理したWave数。</summary>
    public readonly int WaveCount;

    /// <summary>最大深度に達したかどうか。</summary>
    public readonly bool MaxDepthReached;

    public MessagePhaseResult(int waveCount, bool maxDepthReached)
    {
        WaveCount = waveCount;
        MaxDepthReached = maxDepthReached;
    }
}
