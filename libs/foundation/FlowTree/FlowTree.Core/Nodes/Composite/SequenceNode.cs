using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 全ての子ノードが成功するまで順次実行するノード。
/// いずれかが失敗した場合は即座にFailureを返す。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class SequenceNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly List<int> _currentIndexStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// SequenceNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public SequenceNode(params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _currentIndexStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        while (_currentIndexStack[depth] < _children.Length)
        {
            var status = _children[_currentIndexStack[depth]].Tick(ref context);

            switch (status)
            {
                case NodeStatus.Running:
                    return NodeStatus.Running;

                case NodeStatus.Failure:
                    _currentIndexStack[depth] = 0;
                    return NodeStatus.Failure;

                case NodeStatus.Success:
                    _currentIndexStack[depth]++;
                    break;
            }
        }

        _currentIndexStack[depth] = 0;
        return NodeStatus.Success;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _currentIndexStack.Count; i++)
        {
            _currentIndexStack[i] = 0;
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset(fireExitEvents);
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_currentIndexStack.Count <= depth)
        {
            _currentIndexStack.Add(0);
        }
    }
}
