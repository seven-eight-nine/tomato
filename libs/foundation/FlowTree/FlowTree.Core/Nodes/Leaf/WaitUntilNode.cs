namespace Tomato.FlowTree;

/// <summary>
/// 条件が満たされるまで待機するノード。
/// 条件がtrueを返すまでRunningを返し、trueになったらSuccessを返す。
/// </summary>
public sealed class WaitUntilNode : IFlowNode
{
    private readonly FlowCondition _condition;

    /// <summary>
    /// WaitUntilNodeを作成する。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public WaitUntilNode(FlowCondition condition)
    {
        _condition = condition;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _condition() ? NodeStatus.Success : NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // 状態なし
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

    /// <summary>
    /// WaitUntilNodeを作成する。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public WaitUntilNode(FlowCondition<T> condition)
    {
        _condition = condition;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _condition((T)context.State!) ? NodeStatus.Success : NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // 状態なし
    }
}
