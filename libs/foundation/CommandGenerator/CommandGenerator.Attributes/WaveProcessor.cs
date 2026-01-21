using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドキューのWave処理を管理するプロセッサ。
/// 複数のCommandQueueを登録し、Wave単位で処理を実行する。
///
/// <para>Wave処理の流れ:</para>
/// <list type="number">
///   <item>Enqueue時にキューがアクティブとしてマークされる</item>
///   <item>ProcessAllWaves()でアクティブなキューのみを処理</item>
///   <item>各Waveで全キューのコマンドを実行</item>
///   <item>収束（全キューが空）まで繰り返し</item>
/// </list>
///
/// <para>フレーム処理の流れ:</para>
/// <list type="number">
///   <item>BeginFrame(): 次フレームキューをPendingにマージ</item>
///   <item>ProcessAllWaves(): Wave処理を収束まで実行</item>
///   <item>（次フレームでBeginFrame()が呼ばれる）</item>
/// </list>
/// </summary>
public sealed class WaveProcessor
{
    private readonly HashSet<IWaveProcessable> _registeredQueues = new();
    private readonly HashSet<IWaveProcessable> _activeQueues = new();
    private readonly List<IWaveProcessable> _processingList = new(16);
    private readonly int _maxWaveDepth;
    private int _currentWaveDepth;
    private bool _isProcessing;

    /// <summary>
    /// 現在のWave深度
    /// </summary>
    public int CurrentWaveDepth => _currentWaveDepth;

    /// <summary>
    /// 最大Wave深度
    /// </summary>
    public int MaxWaveDepth => _maxWaveDepth;

    /// <summary>
    /// 登録されているキュー数
    /// </summary>
    public int RegisteredQueueCount => _registeredQueues.Count;

    /// <summary>
    /// アクティブなキュー数
    /// </summary>
    public int ActiveQueueCount => _activeQueues.Count;

    /// <summary>
    /// Wave開始時に呼び出されるコールバック（デバッグ用）
    /// </summary>
    public Action<int>? OnWaveStart { get; set; }

    /// <summary>
    /// Wave深度超過時に呼び出されるコールバック
    /// </summary>
    public Action<int>? OnDepthExceeded { get; set; }

    /// <summary>
    /// WaveProcessorを生成する
    /// </summary>
    /// <param name="maxWaveDepth">最大Wave深度（デフォルト100）</param>
    public WaveProcessor(int maxWaveDepth = 100)
    {
        _maxWaveDepth = maxWaveDepth;
    }

    /// <summary>
    /// コマンドキューを登録する。
    /// 登録されたキューはOnEnqueueコールバックが設定され、
    /// Enqueue時に自動的にアクティブとしてマークされる。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(IWaveProcessable queue)
    {
        if (_registeredQueues.Add(queue))
        {
            queue.OnEnqueue = MarkActive;
        }
    }

    /// <summary>
    /// コマンドキューの登録を解除する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unregister(IWaveProcessable queue)
    {
        if (_registeredQueues.Remove(queue))
        {
            queue.OnEnqueue = null;
            _activeQueues.Remove(queue);
        }
    }

    /// <summary>
    /// キューをアクティブとしてマークする。
    /// Enqueue時に自動的に呼び出される。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkActive(IWaveProcessable queue)
    {
        _activeQueues.Add(queue);
    }

    /// <summary>
    /// フレーム開始時の処理。
    /// 全キューのNextFrameQueueをPendingQueueにマージする。
    /// </summary>
    public void BeginFrame()
    {
        foreach (var queue in _registeredQueues)
        {
            queue.MergeNextFrameToPending();

            // NextFrameからマージされた場合、アクティブにする
            if (queue.HasPendingCommands)
            {
                _activeQueues.Add(queue);
            }
        }
    }

    /// <summary>
    /// 処理待ちのコマンドがあるかどうか。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasPendingCommands()
    {
        foreach (var queue in _activeQueues)
        {
            if (queue.HasPendingCommands)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 単一Waveを処理する。
    /// </summary>
    /// <param name="executeAction">各キューに対して実行するアクション</param>
    /// <returns>処理が行われた場合true、全キューが空の場合false</returns>
    public bool ProcessSingleWave(Action<IWaveProcessable> executeAction)
    {
        if (_activeQueues.Count == 0)
        {
            return false;
        }

        // 処理対象のキューをコピー（処理中に新規追加される可能性があるため）
        _processingList.Clear();
        foreach (var queue in _activeQueues)
        {
            if (queue.HasPendingCommands)
            {
                _processingList.Add(queue);
            }
        }

        if (_processingList.Count == 0)
        {
            _activeQueues.Clear();
            return false;
        }

        _currentWaveDepth++;
        OnWaveStart?.Invoke(_currentWaveDepth);

        // 各キューのPendingをCurrentにマージ
        for (int i = 0; i < _processingList.Count; i++)
        {
            _processingList[i].MergePendingToCurrentWave();
        }

        // 各キューを実行
        for (int i = 0; i < _processingList.Count; i++)
        {
            executeAction(_processingList[i]);
        }

        // 空になったキューをアクティブから除外
        _activeQueues.RemoveWhere(q => !q.HasPendingCommands);

        return true;
    }

    /// <summary>
    /// 全Waveを処理する（収束まで）。
    /// </summary>
    /// <param name="executeAction">各キューに対して実行するアクション</param>
    /// <returns>処理結果</returns>
    public WaveProcessingResult ProcessAllWaves(Action<IWaveProcessable> executeAction)
    {
        if (_isProcessing)
        {
            throw new InvalidOperationException("WaveProcessor is already processing. Recursive call is not allowed.");
        }

        _isProcessing = true;
        _currentWaveDepth = 0;

        try
        {
            while (HasPendingCommands())
            {
                if (_currentWaveDepth >= _maxWaveDepth)
                {
                    OnDepthExceeded?.Invoke(_currentWaveDepth);
                    return WaveProcessingResult.DepthExceeded;
                }

                ProcessSingleWave(executeAction);
            }

            return _currentWaveDepth == 0
                ? WaveProcessingResult.Empty
                : WaveProcessingResult.Completed;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 全キューをクリアする。
    /// </summary>
    public void Clear()
    {
        _activeQueues.Clear();
        _currentWaveDepth = 0;
    }
}

/// <summary>
/// Wave処理の結果
/// </summary>
public enum WaveProcessingResult
{
    /// <summary>
    /// 処理するコマンドがなかった
    /// </summary>
    Empty,

    /// <summary>
    /// 正常に収束した
    /// </summary>
    Completed,

    /// <summary>
    /// Wave深度が上限を超えた
    /// </summary>
    DepthExceeded
}
