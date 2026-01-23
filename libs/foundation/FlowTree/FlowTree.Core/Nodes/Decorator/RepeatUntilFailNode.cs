using System;

namespace Tomato.FlowTree;

/// <summary>
/// 子ノードが失敗するまで繰り返すノード。
/// </summary>
public sealed class RepeatUntilFailNode : IFlowNode
{
    private readonly IFlowNode _child;

    /// <summary>
    /// RepeatUntilFailNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public RepeatUntilFailNode(IFlowNode child)
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

            case NodeStatus.Failure:
                return NodeStatus.Success; // 失敗で終了（成功として）

            case NodeStatus.Success:
                _child.Reset();
                return NodeStatus.Running; // 継続
        }

        return NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _child.Reset();
    }
}
