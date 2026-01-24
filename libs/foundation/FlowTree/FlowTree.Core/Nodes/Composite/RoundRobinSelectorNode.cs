using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// ラウンドロビン方式で子ノードを選択するノード。
/// 0→1→2→0→... と順番に選択する。
/// ツリー全体で選択位置を保持し、呼び出しごとに次の子へ進む。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class RoundRobinSelectorNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private int _currentIndex;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// RoundRobinSelectorNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public RoundRobinSelectorNode(params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _currentIndex = 0;
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_children.Length == 0)
            return NodeStatus.Failure;

        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // 初回のみインデックスを進める（Running中は同じ子を継続）
        if (!_hasStartedStack[depth])
        {
            _hasStartedStack[depth] = true;
        }

        var status = _children[_currentIndex].Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _hasStartedStack[depth] = false;
            // 次のインデックスへ進む
            _currentIndex = (_currentIndex + 1) % _children.Length;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int d = 0; d < _hasStartedStack.Count; d++)
        {
            _hasStartedStack[d] = false;
        }
        // _currentIndex はリセットしない（ラウンドロビンの位置を維持）
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset();
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
        }
    }
}
