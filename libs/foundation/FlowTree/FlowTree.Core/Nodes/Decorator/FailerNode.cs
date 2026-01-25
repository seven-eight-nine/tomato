using System;

namespace Tomato.FlowTree;

/// <summary>
/// 常にFailureを返すノード。
/// 子ノードの結果に関わらずFailureを返す（Runningは除く）。
/// </summary>
public sealed class FailerNode : IFlowNode
{
    private readonly IFlowNode _child;

    /// <summary>
    /// FailerNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public FailerNode(IFlowNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        var status = _child.Tick(ref context);

        if (status == NodeStatus.Running)
            return NodeStatus.Running;

        return NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _child.Reset(fireExitEvents);
    }
}
