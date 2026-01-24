using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// サブツリーを動的に提供するデリゲート（ステートレス）。
/// </summary>
/// <returns>実行するFlowTree（nullの場合はFailure）</returns>
public delegate FlowTree? FlowTreeProvider();

/// <summary>
/// サブツリーを動的に提供するデリゲート（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
/// <param name="state">状態オブジェクト</param>
/// <returns>実行するFlowTree（nullの場合はFailure）</returns>
public delegate FlowTree? FlowTreeProvider<in T>(T state) where T : class;

/// <summary>
/// ラムダ式でサブツリーを動的に渡すノード（ステートレス）。
/// ノードに入るたびにラムダを評価し、Running中は同じツリーを継続。
/// Success/Failureになったらリセット。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class DynamicSubTreeNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly FlowTreeProvider _provider;
    private readonly List<FlowTree?> _currentTreeStack;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// DynamicSubTreeNodeを作成する。
    /// </summary>
    /// <param name="provider">サブツリーを提供するラムダ</param>
    public DynamicSubTreeNode(FlowTreeProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // 初回または前回完了時は新しいツリーを取得
        if (!_hasStartedStack[depth])
        {
            _currentTreeStack[depth] = _provider();
            _hasStartedStack[depth] = true;
        }

        var tree = _currentTreeStack[depth];

        // ツリーがnullまたはRootがnullの場合は失敗
        if (tree?.Root == null)
        {
            _hasStartedStack[depth] = false;
            _currentTreeStack[depth] = null;
            return NodeStatus.Failure;
        }

        // コールスタックに追加
        if (context.CallStack != null)
        {
            if (context.CallStack.Count >= context.MaxCallDepth)
            {
                _hasStartedStack[depth] = false;
                _currentTreeStack[depth] = null;
                return NodeStatus.Failure;
            }

            if (!context.CallStack.TryPush(new CallFrame(tree)))
            {
                _hasStartedStack[depth] = false;
                _currentTreeStack[depth] = null;
                return NodeStatus.Failure;
            }
        }

        // サブツリーを実行
        var status = tree.Tick(ref context);

        // コールスタックからポップ
        if (context.CallStack != null)
        {
            context.CallStack.TryPop(out _);
        }

        // 完了したらリセット
        if (status != NodeStatus.Running)
        {
            _hasStartedStack[depth] = false;
            _currentTreeStack[depth] = null;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int d = 0; d < _hasStartedStack.Count; d++)
        {
            _hasStartedStack[d] = false;
            _currentTreeStack[d] = null;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
            _currentTreeStack.Add(null);
        }
    }
}

/// <summary>
/// ラムダ式でサブツリーを動的に渡すノード（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class DynamicSubTreeNode<T> : IFlowNode where T : class
{
    private const int InitialCapacity = 4;

    private readonly FlowTreeProvider<T> _provider;
    private readonly List<FlowTree?> _currentTreeStack;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// DynamicSubTreeNodeを作成する。
    /// </summary>
    /// <param name="provider">サブツリーを提供するラムダ</param>
    public DynamicSubTreeNode(FlowTreeProvider<T> provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // 初回または前回完了時は新しいツリーを取得
        if (!_hasStartedStack[depth])
        {
            _currentTreeStack[depth] = _provider((T)context.State!);
            _hasStartedStack[depth] = true;
        }

        var tree = _currentTreeStack[depth];

        // ツリーがnullまたはRootがnullの場合は失敗
        if (tree?.Root == null)
        {
            _hasStartedStack[depth] = false;
            _currentTreeStack[depth] = null;
            return NodeStatus.Failure;
        }

        // コールスタックに追加
        if (context.CallStack != null)
        {
            if (context.CallStack.Count >= context.MaxCallDepth)
            {
                _hasStartedStack[depth] = false;
                _currentTreeStack[depth] = null;
                return NodeStatus.Failure;
            }

            if (!context.CallStack.TryPush(new CallFrame(tree)))
            {
                _hasStartedStack[depth] = false;
                _currentTreeStack[depth] = null;
                return NodeStatus.Failure;
            }
        }

        // サブツリーを実行
        var status = tree.Tick(ref context);

        // コールスタックからポップ
        if (context.CallStack != null)
        {
            context.CallStack.TryPop(out _);
        }

        // 完了したらリセット
        if (status != NodeStatus.Running)
        {
            _hasStartedStack[depth] = false;
            _currentTreeStack[depth] = null;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int d = 0; d < _hasStartedStack.Count; d++)
        {
            _hasStartedStack[d] = false;
            _currentTreeStack[d] = null;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
            _currentTreeStack.Add(null);
        }
    }
}
