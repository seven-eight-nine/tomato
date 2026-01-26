using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline;
using Tomato.CommandGenerator;

namespace Tomato.GameLoop.Phases;

/// <summary>
/// メッセージ処理システム。
/// StepProcessorを使用してMessageHandlerQueueをStep単位で処理する。
/// </summary>
public sealed class MessageSystem : ISerialSystem
{
    private readonly StepProcessor _waveProcessor;
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
    /// <param name="waveProcessor">Step処理用プロセッサ</param>
    /// <param name="queue">メッセージハンドラキュー</param>
    public MessageSystem(StepProcessor waveProcessor, MessageHandlerQueue queue)
    {
        _waveProcessor = waveProcessor ?? throw new ArgumentNullException(nameof(waveProcessor));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));

        // StepProcessorにキューを登録
        _waveProcessor.Register(_queue);
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // StepProcessorを使用してStep処理
        var result = _waveProcessor.ProcessAllSteps(q =>
        {
            if (q is MessageHandlerQueue mhq)
            {
                mhq.Execute();
            }
        });

        LastResult = new MessagePhaseResult(
            _waveProcessor.CurrentStepDepth,
            result == StepProcessingResult.DepthExceeded);
    }
}

/// <summary>
/// メッセージ処理フェーズの結果。
/// </summary>
public readonly struct MessagePhaseResult
{
    /// <summary>処理したStep数。</summary>
    public readonly int StepCount;

    /// <summary>最大深度に達したかどうか。</summary>
    public readonly bool MaxDepthReached;

    public MessagePhaseResult(int waveCount, bool maxDepthReached)
    {
        StepCount = waveCount;
        MaxDepthReached = maxDepthReached;
    }
}
