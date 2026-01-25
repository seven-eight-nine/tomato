using System;

namespace Tomato.FlowTree;

/// <summary>
/// 子ノードの結果を反転するノード。
/// Success → Failure、Failure → Success
/// </summary>
public sealed class InverterNode : IFlowNode
{
    private readonly IFlowNode _child;

    /// <summary>
    /// InverterNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public InverterNode(IFlowNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        var status = _child.Tick(ref context);

        return status switch
        {
            NodeStatus.Success => NodeStatus.Failure,
            NodeStatus.Failure => NodeStatus.Success,
            _ => status
        };
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _child.Reset(fireExitEvents);
    }
}
