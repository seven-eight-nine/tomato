using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// Joinノードのポリシー。
/// </summary>
public enum JoinPolicy
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
    RequireAny
}

/// <summary>
/// 全ての子ノードが完了するまで待機するノード（WaitAll）。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class JoinNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly List<NodeStatus[]> _statusesStack;
    private readonly JoinPolicy _policy;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// JoinNodeを作成する。
    /// </summary>
    /// <param name="policy">評価ポリシー</param>
    /// <param name="children">子ノードの配列</param>
    public JoinNode(JoinPolicy policy, params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _policy = policy;
        _statusesStack = new List<NodeStatus[]>(InitialCapacity) { CreateStatusArray() };
    }

    /// <summary>
    /// RequireAllポリシーでJoinNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public JoinNode(params IFlowNode[] children)
        : this(JoinPolicy.RequireAll, children)
    {
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);
        var statuses = _statusesStack[depth];

        bool anyRunning = false;
        bool anyFailed = false;
        bool anySuccess = false;

        for (int i = 0; i < _children.Length; i++)
        {
            if (statuses[i] == NodeStatus.Running)
            {
                statuses[i] = _children[i].Tick(ref context);
            }

            switch (statuses[i])
            {
                case NodeStatus.Running:
                    anyRunning = true;
                    break;
                case NodeStatus.Failure:
                    anyFailed = true;
                    break;
                case NodeStatus.Success:
                    anySuccess = true;
                    break;
            }
        }

        if (anyRunning)
            return NodeStatus.Running;

        NodeStatus result;
        switch (_policy)
        {
            case JoinPolicy.RequireAll:
                result = anyFailed ? NodeStatus.Failure : NodeStatus.Success;
                break;
            case JoinPolicy.RequireAny:
                result = anySuccess ? NodeStatus.Success : NodeStatus.Failure;
                break;
            default:
                result = NodeStatus.Failure;
                break;
        }

        ResetStatuses(statuses);
        return result;
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
