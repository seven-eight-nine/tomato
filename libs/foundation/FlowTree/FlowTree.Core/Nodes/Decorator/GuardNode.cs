using System;

namespace Tomato.FlowTree;

/// <summary>
/// 条件が満たされた場合のみ子ノードを実行するノード。
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
            if (!_condition(ref context))
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
