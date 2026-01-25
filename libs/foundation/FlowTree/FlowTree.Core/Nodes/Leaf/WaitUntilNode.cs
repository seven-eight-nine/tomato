using System;

namespace Tomato.FlowTree;

/// <summary>
/// 条件が満たされるまで待機するノード。
/// 条件がtrueを返すまでRunningを返し、trueになったらSuccessを返す。
/// </summary>
public sealed class WaitUntilNode : IFlowNode
{
    private readonly FlowCondition _condition;
    private readonly float _interval;
    private float _elapsed;
    private bool _lastResult;
    private bool _hasResult;

    /// <summary>
    /// WaitUntilNodeを作成する（毎フレーム評価）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public WaitUntilNode(FlowCondition condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _interval = 0;
    }

    /// <summary>
    /// WaitUntilNodeを作成する（間隔評価）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    /// <param name="interval">チェック間隔（秒）。間隔内は前回の結果を維持する。</param>
    public WaitUntilNode(FlowCondition condition, float interval)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (interval < 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be non-negative.");
        _interval = interval;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_interval <= 0)
        {
            return _condition() ? NodeStatus.Success : NodeStatus.Running;
        }

        _elapsed += context.DeltaTime;

        if (!_hasResult || _elapsed >= _interval)
        {
            _lastResult = _condition();
            _elapsed = 0;
            _hasResult = true;
        }

        return _lastResult ? NodeStatus.Success : NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _elapsed = 0;
        _lastResult = false;
        _hasResult = false;
    }
}

/// <summary>
/// 条件が満たされるまで待機するノード（状態付き版）。
/// 条件がtrueを返すまでRunningを返し、trueになったらSuccessを返す。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class WaitUntilNode<T> : IFlowNode where T : class, IFlowState
{
    private readonly FlowCondition<T> _condition;
    private readonly float _interval;
    private float _elapsed;
    private bool _lastResult;
    private bool _hasResult;

    /// <summary>
    /// WaitUntilNodeを作成する（毎フレーム評価）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public WaitUntilNode(FlowCondition<T> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _interval = 0;
    }

    /// <summary>
    /// WaitUntilNodeを作成する（間隔評価）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    /// <param name="interval">チェック間隔（秒）。間隔内は前回の結果を維持する。</param>
    public WaitUntilNode(FlowCondition<T> condition, float interval)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (interval < 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be non-negative.");
        _interval = interval;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_interval <= 0)
        {
            return _condition((T)context.State!) ? NodeStatus.Success : NodeStatus.Running;
        }

        _elapsed += context.DeltaTime;

        if (!_hasResult || _elapsed >= _interval)
        {
            _lastResult = _condition((T)context.State!);
            _elapsed = 0;
            _hasResult = true;
        }

        return _lastResult ? NodeStatus.Success : NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _elapsed = 0;
        _lastResult = false;
        _hasResult = false;
    }
}
