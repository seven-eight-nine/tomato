using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 子ノードを指定回数繰り返すノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class RepeatNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly int _count;
    private readonly List<int> _currentIterationStack;

    /// <summary>
    /// RepeatNodeを作成する。
    /// </summary>
    /// <param name="count">繰り返し回数</param>
    /// <param name="child">子ノード</param>
    public RepeatNode(int count, IFlowNode child)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");

        _child = child ?? throw new ArgumentNullException(nameof(child));
        _count = count;
        _currentIterationStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        while (_currentIterationStack[depth] < _count)
        {
            var status = _child.Tick(ref context);

            switch (status)
            {
                case NodeStatus.Running:
                    return NodeStatus.Running;

                case NodeStatus.Failure:
                    _currentIterationStack[depth] = 0;
                    return NodeStatus.Failure;

                case NodeStatus.Success:
                    _currentIterationStack[depth]++;
                    _child.Reset();
                    break;
            }
        }

        _currentIterationStack[depth] = 0;
        return NodeStatus.Success;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _currentIterationStack.Count; i++)
        {
            _currentIterationStack[i] = 0;
        }
        _child.Reset();
    }

    private void EnsureDepth(int depth)
    {
        while (_currentIterationStack.Count <= depth)
        {
            _currentIterationStack.Add(0);
        }
    }
}
