using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 重み付きランダム選択を行うノード。
/// 各子ノードの重みに基づいて確率的に選択する。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class WeightedRandomSelectorNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly float[] _weights;
    private readonly float _totalWeight;
    private readonly Random _random;
    private readonly List<int> _selectedIndexStack;
    private readonly List<bool> _hasSelectedStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// WeightedRandomSelectorNodeを作成する。
    /// </summary>
    /// <param name="children">重みとノードのタプル配列</param>
    public WeightedRandomSelectorNode(params (float weight, IFlowNode node)[] children)
        : this(new Random(), children)
    {
    }

    /// <summary>
    /// WeightedRandomSelectorNodeを作成する（シード指定）。
    /// </summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="children">重みとノードのタプル配列</param>
    public WeightedRandomSelectorNode(int seed, params (float weight, IFlowNode node)[] children)
        : this(new Random(seed), children)
    {
    }

    /// <summary>
    /// WeightedRandomSelectorNodeを作成する（Random指定）。
    /// </summary>
    /// <param name="random">乱数生成器</param>
    /// <param name="children">重みとノードのタプル配列</param>
    public WeightedRandomSelectorNode(Random random, params (float weight, IFlowNode node)[] children)
    {
        if (children == null) throw new ArgumentNullException(nameof(children));
        _random = random ?? throw new ArgumentNullException(nameof(random));

        _children = new IFlowNode[children.Length];
        _weights = new float[children.Length];
        _totalWeight = 0;

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].weight < 0)
                throw new ArgumentException($"Weight cannot be negative: {children[i].weight}", nameof(children));

            _children[i] = children[i].node ?? throw new ArgumentNullException(nameof(children));
            _weights[i] = children[i].weight;
            _totalWeight += children[i].weight;
        }

        _selectedIndexStack = new List<int>(InitialCapacity) { -1 };
        _hasSelectedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_children.Length == 0 || _totalWeight <= 0)
            return NodeStatus.Failure;

        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // まだ選択されていない場合は重み付きランダムで選択
        if (!_hasSelectedStack[depth])
        {
            _selectedIndexStack[depth] = SelectWeightedRandom();
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
    public void Reset(bool fireExitEvents = true)
    {
        for (int d = 0; d < _selectedIndexStack.Count; d++)
        {
            _hasSelectedStack[d] = false;
            _selectedIndexStack[d] = -1;
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset(fireExitEvents);
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

    private int SelectWeightedRandom()
    {
        float value = (float)_random.NextDouble() * _totalWeight;
        float cumulative = 0;

        for (int i = 0; i < _weights.Length; i++)
        {
            cumulative += _weights[i];
            if (value <= cumulative)
                return i;
        }

        // 浮動小数点の誤差対策として最後の要素を返す
        return _children.Length - 1;
    }
}
