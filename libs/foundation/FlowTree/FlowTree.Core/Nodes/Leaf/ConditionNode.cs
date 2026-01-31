using System;
using Tomato.Time;

namespace Tomato.FlowTree;

/// <summary>
/// 条件を評価するデリゲート（ステートレス）。
/// </summary>
/// <returns>条件が満たされた場合はtrue</returns>
public delegate bool FlowCondition();

/// <summary>
/// 条件を評価するデリゲート（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
/// <param name="state">状態オブジェクト</param>
/// <returns>条件が満たされた場合はtrue</returns>
public delegate bool FlowCondition<in T>(T state) where T : class, IFlowState;

/// <summary>
/// 条件を評価するノード（ステートレス）。
/// trueならSuccess、falseならFailureを返す。
/// </summary>
public sealed class ConditionNode : IFlowNode
{
    private readonly FlowCondition _condition;
    private readonly TickDuration _interval;
    private int _elapsed;
    private bool? _lastResult;

    /// <summary>
    /// ConditionNodeを作成する（毎tick評価）。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public ConditionNode(FlowCondition condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _interval = TickDuration.Zero;
    }

    /// <summary>
    /// ConditionNodeを作成する（間隔評価）。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    /// <param name="interval">チェック間隔（tick数）。間隔内は前回の結果を維持する。</param>
    public ConditionNode(FlowCondition condition, TickDuration interval)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (interval.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be non-negative.");
        _interval = interval;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_interval.IsZero)
        {
            return _condition() ? NodeStatus.Success : NodeStatus.Failure;
        }

        _elapsed += context.DeltaTicks;

        if (!_lastResult.HasValue || _elapsed >= _interval.Value)
        {
            _lastResult = _condition();
            _elapsed = 0;
        }

        return _lastResult.Value ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _elapsed = 0;
        _lastResult = null;
    }
}

/// <summary>
/// 条件を評価するノード（型付き）。
/// trueならSuccess、falseならFailureを返す。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class ConditionNode<T> : IFlowNode where T : class, IFlowState
{
    private readonly FlowCondition<T> _condition;
    private readonly TickDuration _interval;
    private int _elapsed;
    private bool? _lastResult;

    /// <summary>
    /// ConditionNodeを作成する（毎tick評価）。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public ConditionNode(FlowCondition<T> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _interval = TickDuration.Zero;
    }

    /// <summary>
    /// ConditionNodeを作成する（間隔評価）。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    /// <param name="interval">チェック間隔（tick数）。間隔内は前回の結果を維持する。</param>
    public ConditionNode(FlowCondition<T> condition, TickDuration interval)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (interval.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be non-negative.");
        _interval = interval;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_interval.IsZero)
        {
            return _condition((T)context.State!) ? NodeStatus.Success : NodeStatus.Failure;
        }

        _elapsed += context.DeltaTicks;

        if (!_lastResult.HasValue || _elapsed >= _interval.Value)
        {
            _lastResult = _condition((T)context.State!);
            _elapsed = 0;
        }

        return _lastResult.Value ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _elapsed = 0;
        _lastResult = null;
    }
}
