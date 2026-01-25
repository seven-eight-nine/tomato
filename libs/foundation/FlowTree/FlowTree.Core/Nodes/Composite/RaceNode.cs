using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 最初に完了した子ノードの結果を採用するノード（WaitAny）。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class RaceNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly List<NodeStatus[]> _statusesStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// RaceNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public RaceNode(params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _statusesStack = new List<NodeStatus[]>(InitialCapacity) { CreateStatusArray() };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);
        var statuses = _statusesStack[depth];

        for (int i = 0; i < _children.Length; i++)
        {
            if (statuses[i] == NodeStatus.Running)
            {
                statuses[i] = _children[i].Tick(ref context);

                if (statuses[i] != NodeStatus.Running)
                {
                    var result = statuses[i];
                    ResetStatuses(statuses);
                    return result;
                }
            }
        }

        return NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int d = 0; d < _statusesStack.Count; d++)
        {
            ResetStatuses(_statusesStack[d]);
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset(fireExitEvents);
        }
    }

    private NodeStatus[] CreateStatusArray()
    {
        var statuses = new NodeStatus[_children.Length];
        for (int i = 0; i < statuses.Length; i++)
        {
            statuses[i] = NodeStatus.Running;
        }
        return statuses;
    }

    private void EnsureDepth(int depth)
    {
        while (_statusesStack.Count <= depth)
        {
            _statusesStack.Add(CreateStatusArray());
        }
    }

    private static void ResetStatuses(NodeStatus[] statuses)
    {
        for (int i = 0; i < statuses.Length; i++)
        {
            statuses[i] = NodeStatus.Running;
        }
    }
}
