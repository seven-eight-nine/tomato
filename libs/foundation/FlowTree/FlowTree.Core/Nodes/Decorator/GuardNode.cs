using System;

namespace Tomato.FlowTree;

/// <summary>
/// 条件が満たされた場合のみ子ノードを実行するノード（ステートレス）。
/// </summary>
public sealed class GuardNode : IFlowNode
{
    private readonly FlowCondition _condition;
    private readonly IFlowNode _child;
    private bool _isRunning;

    /// <summary>
    /// GuardNodeを作成する。
    /// </summary>
    /// <param name="condition">実行条件</param>
    /// <param name="child">子ノード</param>
    public GuardNode(FlowCondition condition, IFlowNode child)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _isRunning = false;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        // 実行中でなければ条件をチェック
        if (!_isRunning)
        {
            if (!_condition())
                return NodeStatus.Failure;

            _isRunning = true;
        }

        var status = _child.Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _isRunning = false;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _isRunning = false;
        _child.Reset();
    }
}

/// <summary>
/// 条件が満たされた場合のみ子ノードを実行するノード（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class GuardNode<T> : IFlowNode where T : class
{
    private readonly FlowCondition<T> _condition;
    private readonly IFlowNode _child;
    private bool _isRunning;

    /// <summary>
    /// GuardNodeを作成する。
    /// </summary>
    /// <param name="condition">実行条件</param>
    /// <param name="child">子ノード</param>
    public GuardNode(FlowCondition<T> condition, IFlowNode child)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _isRunning = false;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        // 実行中でなければ条件をチェック
        if (!_isRunning)
        {
            if (!_condition((T)context.State!))
                return NodeStatus.Failure;

            _isRunning = true;
        }

        var status = _child.Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _isRunning = false;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _isRunning = false;
        _child.Reset();
    }
}
