using System;

namespace Tomato.CommandGenerator;

/// <summary>
/// StepProcessorによって処理可能なコマンドキューのインターフェース。
/// CommandQueue属性を持つクラスに自動実装される。
/// </summary>
public interface IStepProcessable
{
    /// <summary>
    /// 処理待ちのコマンドがあるかどうか。
    /// CurrentQueue + PendingQueue のいずれかにコマンドがある場合にtrue。
    /// </summary>
    bool HasPendingCommands { get; }

    /// <summary>
    /// PendingQueueのコマンドをCurrentQueueにマージする。
    /// Step処理の開始時に呼び出される。
    /// </summary>
    void MergePendingToCurrentStep();

    /// <summary>
    /// NextFrameQueueのコマンドをPendingQueueにマージする。
    /// フレーム開始時に呼び出される。
    /// </summary>
    void MergeNextFrameToPending();

    /// <summary>
    /// Enqueue時に呼び出されるコールバック。
    /// StepProcessorがこのキューをアクティブとしてマークするために使用。
    /// nullの場合は何も呼び出されない。
    /// </summary>
    Action<IStepProcessable>? OnEnqueue { get; set; }
}
