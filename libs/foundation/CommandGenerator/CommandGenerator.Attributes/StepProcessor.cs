using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドキューのStep処理を管理するプロセッサ。
/// 複数のCommandQueueを登録し、Step単位で処理を実行する。
///
/// <para>Step処理の流れ:</para>
/// <list type="number">
///   <item>Enqueue時にキューがアクティブとしてマークされる</item>
///   <item>ProcessAllSteps()でアクティブなキューのみを処理</item>
///   <item>各Stepで全キューのコマンドを実行</item>
///   <item>収束（全キューが空）まで繰り返し</item>
/// </list>
///
/// <para>フレーム処理の流れ:</para>
/// <list type="number">
///   <item>BeginFrame(): 次フレームキューをPendingにマージ</item>
///   <item>ProcessAllSteps(): Step処理を収束まで実行</item>
///   <item>（次フレームでBeginFrame()が呼ばれる）</item>
/// </list>
/// </summary>
public sealed class StepProcessor
{
    private readonly HashSet<IStepProcessable> _registeredQueues = new();
    private readonly HashSet<IStepProcessable> _activeQueues = new();
    private readonly List<IStepProcessable> _processingList = new(16);
    private readonly int _maxStepDepth;
    private int _currentStepDepth;
    private bool _isProcessing;

    /// <summary>
    /// 現在のStep深度
    /// </summary>
    public int CurrentStepDepth => _currentStepDepth;

    /// <summary>
    /// 最大Step深度
    /// </summary>
    public int MaxStepDepth => _maxStepDepth;

    /// <summary>
    /// 登録されているキュー数
    /// </summary>
    public int RegisteredQueueCount => _registeredQueues.Count;

    /// <summary>
    /// アクティブなキュー数
    /// </summary>
    public int ActiveQueueCount => _activeQueues.Count;

    /// <summary>
    /// Step開始時に呼び出されるコールバック（デバッグ用）
    /// </summary>
    public Action<int>? OnStepStart { get; set; }

    /// <summary>
    /// Step深度超過時に呼び出されるコールバック
    /// </summary>
    public Action<int>? OnDepthExceeded { get; set; }

    /// <summary>
    /// 並列処理を有効にするかどうか。
    /// trueの場合、各Step内で複数のキューを並列に処理する。
    /// </summary>
    public bool EnableParallelProcessing { get; set; }

    /// <summary>
    /// StepProcessorを生成する
    /// </summary>
    /// <param name="maxStepDepth">最大Step深度（デフォルト100）</param>
    public StepProcessor(int maxStepDepth = 100)
    {
        _maxStepDepth = maxStepDepth;
    }

    /// <summary>
    /// コマンドキューを登録する。
    /// 登録されたキューはOnEnqueueコールバックが設定され、
    /// Enqueue時に自動的にアクティブとしてマークされる。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(IStepProcessable queue)
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
    public void Unregister(IStepProcessable queue)
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
    public void MarkActive(IStepProcessable queue)
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
    /// 単一Stepを処理する。
    /// </summary>
    /// <param name="executeAction">各キューに対して実行するアクション</param>
    /// <returns>処理が行われた場合true、全キューが空の場合false</returns>
    public bool ProcessSingleStep(Action<IStepProcessable> executeAction)
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

        _currentStepDepth++;
        OnStepStart?.Invoke(_currentStepDepth);

        // 各キューのPendingをCurrentにマージ
        for (int i = 0; i < _processingList.Count; i++)
        {
            _processingList[i].MergePendingToCurrentStep();
        }

        // 各キューを実行
        if (EnableParallelProcessing && _processingList.Count > 1)
        {
            // 並列実行
            Parallel.ForEach(_processingList, executeAction);
        }
        else
        {
            // 逐次実行（既存動作）
            for (int i = 0; i < _processingList.Count; i++)
            {
                executeAction(_processingList[i]);
            }
        }

        // 空になったキューをアクティブから除外
        _activeQueues.RemoveWhere(q => !q.HasPendingCommands);

        return true;
    }

    /// <summary>
    /// 全Stepを処理する（収束まで）。
    /// </summary>
    /// <param name="executeAction">各キューに対して実行するアクション</param>
    /// <returns>処理結果</returns>
    public StepProcessingResult ProcessAllSteps(Action<IStepProcessable> executeAction)
    {
        if (_isProcessing)
        {
            throw new InvalidOperationException("StepProcessor is already processing. Recursive call is not allowed.");
        }

        _isProcessing = true;
        _currentStepDepth = 0;

        try
        {
            while (HasPendingCommands())
            {
                if (_currentStepDepth >= _maxStepDepth)
                {
                    OnDepthExceeded?.Invoke(_currentStepDepth);
                    return StepProcessingResult.DepthExceeded;
                }

                ProcessSingleStep(executeAction);
            }

            return _currentStepDepth == 0
                ? StepProcessingResult.Empty
                : StepProcessingResult.Completed;
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
        _currentStepDepth = 0;
    }
}

/// <summary>
/// Step処理の結果
/// </summary>
public enum StepProcessingResult
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
    /// Step深度が上限を超えた
    /// </summary>
    DepthExceeded
}
