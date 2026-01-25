using System;

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

    /// <summary>
    /// ConditionNodeを作成する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public ConditionNode(FlowCondition condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _condition() ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset()
    {
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

    /// <summary>
    /// ConditionNodeを作成する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public ConditionNode(FlowCondition<T> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _condition((T)context.State!) ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset()
    {
    }
}
