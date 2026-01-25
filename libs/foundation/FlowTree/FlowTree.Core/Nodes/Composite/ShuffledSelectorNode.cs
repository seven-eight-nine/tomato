using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// シャッフル済みの順序で子ノードを選択するノード。
/// 全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class ShuffledSelectorNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode[] _children;
    private readonly Random _random;
    private readonly List<int[]> _shuffledIndicesStack;
    private readonly List<int> _currentPositionStack;

    /// <summary>
    /// 子ノードの配列。
    /// </summary>
    public ReadOnlySpan<IFlowNode> Children => _children;

    /// <summary>
    /// ShuffledSelectorNodeを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public ShuffledSelectorNode(params IFlowNode[] children)
        : this(new Random(), children)
    {
    }

    /// <summary>
    /// ShuffledSelectorNodeを作成する（シード指定）。
    /// </summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="children">子ノードの配列</param>
    public ShuffledSelectorNode(int seed, params IFlowNode[] children)
        : this(new Random(seed), children)
    {
    }

    /// <summary>
    /// ShuffledSelectorNodeを作成する（Random指定）。
    /// </summary>
    /// <param name="random">乱数生成器</param>
    /// <param name="children">子ノードの配列</param>
    public ShuffledSelectorNode(Random random, params IFlowNode[] children)
    {
        _children = children ?? throw new ArgumentNullException(nameof(children));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _shuffledIndicesStack = new List<int[]>(InitialCapacity) { CreateShuffledIndices() };
        _currentPositionStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_children.Length == 0)
            return NodeStatus.Failure;

        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // 現在位置が配列末尾を超えたら再シャッフル
        if (_currentPositionStack[depth] >= _children.Length)
        {
            Shuffle(_shuffledIndicesStack[depth]);
            _currentPositionStack[depth] = 0;
        }

        int index = _shuffledIndicesStack[depth][_currentPositionStack[depth]];
        var status = _children[index].Tick(ref context);

        if (status != NodeStatus.Running)
        {
            // 次の位置へ進む
            _currentPositionStack[depth]++;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int d = 0; d < _currentPositionStack.Count; d++)
        {
            _currentPositionStack[d] = 0;
            Shuffle(_shuffledIndicesStack[d]);
        }
        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].Reset(fireExitEvents);
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_shuffledIndicesStack.Count <= depth)
        {
            _shuffledIndicesStack.Add(CreateShuffledIndices());
            _currentPositionStack.Add(0);
        }
    }

    private int[] CreateShuffledIndices()
    {
        var indices = new int[_children.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }
        Shuffle(indices);
        return indices;
    }

    private void Shuffle(int[] array)
    {
        // Fisher-Yates シャッフル
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
