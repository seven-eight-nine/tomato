using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// ランダムに子ノードを選択して実行するノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class RandomSelectorNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly Random _random;
    private readonly List<int> _selectedIndexStack;
    private readonly List<bool> _hasSelectedStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// RandomSelectorNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public RandomSelectorNode(params IFlowNode[] children)
        : this(new Random(), children)
    {
    }

    /// <summary>
    /// RandomSelectorNodeを作成する（シード指定）。
    /// </summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="children">子ノードの配列</param>
    public RandomSelectorNode(int seed, params IFlowNode[] children)
        : this(new Random(seed), children)
    {
    }

    /// <summary>
    /// RandomSelectorNodeを作成する（Random指定）。
    /// </summary>
    /// <param name="random">乱数生成器</param>
    /// <param name="children">子ノードの配列</param>
    public RandomSelectorNode(Random random, params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _selectedIndexStack = new List<int>(InitialCapacity) { -1 };
        _hasSelectedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_children.Length == 0)
            return NodeStatus.Failure;

        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // まだ選択されていない場合はランダムに選択
        if (!_hasSelectedStack[depth])
        {
            _selectedIndexStack[depth] = _random.Next(_children.Length);
            _hasSelectedStack[depth] = true;
        }

        var status = _children[_selectedIndexStack[depth]].Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _hasSelectedStack[depth] = false;
            _selectedIndexStack[depth] = -1;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int d = 0; d < _selectedIndexStack.Count; d++)
        {
            _hasSelectedStack[d] = false;
            _selectedIndexStack[d] = -1;
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset();
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_selectedIndexStack.Count <= depth)
        {
            _selectedIndexStack.Add(-1);
            _hasSelectedStack.Add(false);
        }
    }
}
