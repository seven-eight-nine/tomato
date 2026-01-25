using System;

namespace Tomato.FlowTree;

/// <summary>
/// 常にSuccessを返すノード。
/// 子ノードの結果に関わらずSuccessを返す（Runningは除く）。
/// </summary>
public sealed class SucceederNode : IFlowNode
{
    private readonly IFlowNode _child;

    /// <summary>
    /// SucceederNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public SucceederNode(IFlowNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        var status = _child.Tick(ref context);

        if (status == NodeStatus.Running)
            return NodeStatus.Running;

        return NodeStatus.Success;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _child.Reset(fireExitEvents);
    }
}
