using System;

namespace Tomato.FlowTree;

/// <summary>
/// 子ノードが成功するまで繰り返すノード。
/// </summary>
public sealed class RepeatUntilSuccessNode : IFlowNode
{
    private readonly IFlowNode _child;

    /// <summary>
    /// RepeatUntilSuccessNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public RepeatUntilSuccessNode(IFlowNode child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        var status = _child.Tick(ref context);

        switch (status)
        {
            case NodeStatus.Running:
                return NodeStatus.Running;

            case NodeStatus.Success:
                return NodeStatus.Success;

            case NodeStatus.Failure:
                _child.Reset();
                return NodeStatus.Running; // 継続
        }

        return NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        _child.Reset(fireExitEvents);
    }
}
