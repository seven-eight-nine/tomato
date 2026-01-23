using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 並列ノードのポリシー。
/// </summary>
public enum ParallelPolicy
{
    /// <summary>
    /// 全ての子ノードが成功した場合にSuccess。
    /// 1つでも失敗した場合はFailure。
    /// </summary>
    RequireAll,

    /// <summary>
    /// 1つでも成功した場合にSuccess。
    /// 全て失敗した場合のみFailure。
    /// </summary>
    RequireOne
}

/// <summary>
/// 全ての子ノードを並列に評価するノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class ParallelNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly List<NodeStatus[]> _statusesStack;
    private readonly ParallelPolicy _policy;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// ParallelNodeを作成する。
    /// </summary>
    /// <param name="policy">評価ポリシー</param>
    /// <param name="children">子ノードの配列</param>
    public ParallelNode(ParallelPolicy policy, params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _policy = policy;
        _statusesStack = new List<NodeStatus[]>(InitialCapacity) { CreateStatusArray() };
    }

    /// <summary>
    /// RequireAllポリシーでParallelNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public ParallelNode(params IFlowNode[] children)
        : this(ParallelPolicy.RequireAll, children)
    {
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);
        var statuses = _statusesStack[depth];

        bool anyRunning = false;
        bool anySuccess = false;
        bool anyFailure = false;

        for (int i = 0; i < _children.Length; i++)
        {
            if (statuses[i] != NodeStatus.Running)
            {
                if (statuses[i] == NodeStatus.Success) anySuccess = true;
                if (statuses[i] == NodeStatus.Failure) anyFailure = true;
                continue;
            }

            statuses[i] = _children[i].Tick(ref context);

            switch (statuses[i])
            {
                case NodeStatus.Running:
                    anyRunning = true;
                    break;
                case NodeStatus.Success:
                    anySuccess = true;
                    break;
                case NodeStatus.Failure:
                    anyFailure = true;
                    break;
            }
        }

        switch (_policy)
        {
            case ParallelPolicy.RequireAll:
                if (anyFailure)
                {
                    ResetStatuses(statuses);
                    return NodeStatus.Failure;
                }
                if (anyRunning) return NodeStatus.Running;
                ResetStatuses(statuses);
                return NodeStatus.Success;

            case ParallelPolicy.RequireOne:
                if (anySuccess)
                {
                    ResetStatuses(statuses);
                    return NodeStatus.Success;
                }
                if (anyRunning) return NodeStatus.Running;
                ResetStatuses(statuses);
                return NodeStatus.Failure;

            default:
                return NodeStatus.Failure;
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int d = 0; d < _statusesStack.Count; d++)
        {
            ResetStatuses(_statusesStack[d]);
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset();
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
