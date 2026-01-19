using System;

namespace Tomato.CommandGenerator;

/// <summary>
/// WaveProcessorによって処理可能なコマンドキューのインターフェース。
/// CommandQueue属性を持つクラスに自動実装される。
/// </summary>
public interface IWaveProcessable
{
    /// <summary>
    /// 処理待ちのコマンドがあるかどうか。
    /// CurrentQueue + PendingQueue のいずれかにコマンドがある場合にtrue。
    /// </summary>
    bool HasPendingCommands { get; }

    /// <summary>
    /// PendingQueueのコマンドをCurrentQueueにマージする。
    /// Wave処理の開始時に呼び出される。
    /// </summary>
    void MergePendingToCurrentWave();

    /// <summary>
    /// NextFrameQueueのコマンドをPendingQueueにマージする。
    /// フレーム開始時に呼び出される。
    /// </summary>
    void MergeNextFrameToPending();

    /// <summary>
    /// Enqueue時に呼び出されるコールバック。
    /// WaveProcessorがこのキューをアクティブとしてマークするために使用。
    /// nullの場合は何も呼び出されない。
    /// </summary>
    Action<IWaveProcessable>? OnEnqueue { get; set; }
}
