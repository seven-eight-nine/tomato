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
public delegate FlowTree? FlowTreeProvider<in T>(T state) where T : class, IFlowState;

/// <summary>
/// サブツリー用の新しい状態を提供するデリゲート。
/// </summary>
/// <typeparam name="TParent">親状態の型</typeparam>
/// <typeparam name="TChild">子状態の型</typeparam>
/// <param name="parentState">親状態オブジェクト</param>
/// <returns>サブツリーで使用する新しい状態</returns>
public delegate TChild FlowStateProvider<in TParent, out TChild>(TParent parentState)
    where TParent : class, IFlowState
    where TChild : class, IFlowState;

/// <summary>
/// サブツリーを呼び出すノード（ステートレス）。
/// FlowTree参照を直接保持することで、自己再帰・相互再帰をサポート。
/// </summary>
public sealed class SubTreeNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly FlowTree? _staticTree;
    private readonly FlowTreeProvider? _dynamicProvider;
    private readonly List<FlowTree?> _currentTreeStack;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// 静的なサブツリーを呼び出すSubTreeNodeを作成する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public SubTreeNode(FlowTree tree)
    {
        _staticTree = tree ?? throw new ArgumentNullException(nameof(tree));
        _dynamicProvider = null;
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <summary>
    /// 動的にサブツリーを決定するSubTreeNodeを作成する。
    /// </summary>
    /// <param name="provider">サブツリーを提供するラムダ</param>
    public SubTreeNode(FlowTreeProvider provider)
    {
        _staticTree = null;
        _dynamicProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // ツリーを決定
        FlowTree? tree;
        if (_staticTree != null)
        {
            tree = _staticTree;
        }
        else
        {
            // 初回または前回完了時は新しいツリーを取得
            if (!_hasStartedStack[depth])
            {
                _currentTreeStack[depth] = _dynamicProvider!();
                _hasStartedStack[depth] = true;
            }
            tree = _currentTreeStack[depth];
        }

        // ツリーがnullまたはRootがnullの場合は失敗
        if (tree?.Root == null)
        {
            ResetAtDepth(depth);
            return NodeStatus.Failure;
        }

        // コールスタックに追加
        if (context.CallStack != null)
        {
            if (context.CallStack.Count >= context.MaxCallDepth)
            {
                ResetAtDepth(depth);
                return NodeStatus.Failure;
            }

            if (!context.CallStack.TryPush(new CallFrame(tree)))
            {
                ResetAtDepth(depth);
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
            ResetAtDepth(depth);
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _hasStartedStack.Count; i++)
        {
            _hasStartedStack[i] = false;
            _currentTreeStack[i]?.Reset(fireExitEvents);
            _currentTreeStack[i] = null;
        }
    }

    private void ResetAtDepth(int depth)
    {
        _hasStartedStack[depth] = false;
        _currentTreeStack[depth] = null;
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
/// サブツリーを呼び出すノード（型付き、State注入対応）。
/// 静的/動的ツリーの選択と、オプションでサブツリー用の新しいStateを注入できる。
/// </summary>
/// <typeparam name="TParent">親状態の型</typeparam>
public sealed class SubTreeNode<TParent> : IFlowNode where TParent : class, IFlowState
{
    private const int InitialCapacity = 4;

    private readonly FlowTree? _staticTree;
    private readonly FlowTreeProvider<TParent>? _dynamicProvider;
    private readonly List<FlowTree?> _currentTreeStack;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// 静的なサブツリーを呼び出すSubTreeNodeを作成する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public SubTreeNode(FlowTree tree)
    {
        _staticTree = tree ?? throw new ArgumentNullException(nameof(tree));
        _dynamicProvider = null;
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <summary>
    /// 動的にサブツリーを決定するSubTreeNodeを作成する。
    /// </summary>
    /// <param name="provider">サブツリーを提供するラムダ</param>
    public SubTreeNode(FlowTreeProvider<TParent> provider)
    {
        _staticTree = null;
        _dynamicProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        var parentState = (TParent)context.State!;

        // ツリーを決定
        FlowTree? tree;
        if (_staticTree != null)
        {
            tree = _staticTree;
        }
        else
        {
            // 初回または前回完了時は新しいツリーを取得
            if (!_hasStartedStack[depth])
            {
                _currentTreeStack[depth] = _dynamicProvider!(parentState);
                _hasStartedStack[depth] = true;
            }
            tree = _currentTreeStack[depth];
        }

        // ツリーがnullまたはRootがnullの場合は失敗
        if (tree?.Root == null)
        {
            ResetAtDepth(depth);
            return NodeStatus.Failure;
        }

        // コールスタックに追加
        if (context.CallStack != null)
        {
            if (context.CallStack.Count >= context.MaxCallDepth)
            {
                ResetAtDepth(depth);
                return NodeStatus.Failure;
            }

            if (!context.CallStack.TryPush(new CallFrame(tree)))
            {
                ResetAtDepth(depth);
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
            ResetAtDepth(depth);
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _hasStartedStack.Count; i++)
        {
            _hasStartedStack[i] = false;
            _currentTreeStack[i]?.Reset(fireExitEvents);
            _currentTreeStack[i] = null;
        }
    }

    private void ResetAtDepth(int depth)
    {
        _hasStartedStack[depth] = false;
        _currentTreeStack[depth] = null;
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
/// サブツリーを呼び出すノード（State注入対応）。
/// サブツリー実行時に新しいStateを注入し、親Stateへの参照を設定する。
/// </summary>
/// <typeparam name="TParent">親状態の型</typeparam>
/// <typeparam name="TChild">子状態の型</typeparam>
public sealed class SubTreeNode<TParent, TChild> : IFlowNode
    where TParent : class, IFlowState
    where TChild : class, IFlowState
{
    private const int InitialCapacity = 4;

    private readonly FlowTree? _staticTree;
    private readonly FlowTreeProvider<TParent>? _dynamicProvider;
    private readonly FlowStateProvider<TParent, TChild> _stateProvider;
    private readonly List<FlowTree?> _currentTreeStack;
    private readonly List<TChild?> _currentStateStack;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// 静的なサブツリーを新しいStateで呼び出すSubTreeNodeを作成する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    /// <param name="stateProvider">サブツリー用のStateを提供するラムダ</param>
    public SubTreeNode(FlowTree tree, FlowStateProvider<TParent, TChild> stateProvider)
    {
        _staticTree = tree ?? throw new ArgumentNullException(nameof(tree));
        _dynamicProvider = null;
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _currentStateStack = new List<TChild?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <summary>
    /// 動的にサブツリーを決定し、新しいStateで呼び出すSubTreeNodeを作成する。
    /// </summary>
    /// <param name="treeProvider">サブツリーを提供するラムダ</param>
    /// <param name="stateProvider">サブツリー用のStateを提供するラムダ</param>
    public SubTreeNode(FlowTreeProvider<TParent> treeProvider, FlowStateProvider<TParent, TChild> stateProvider)
    {
        _staticTree = null;
        _dynamicProvider = treeProvider ?? throw new ArgumentNullException(nameof(treeProvider));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _currentTreeStack = new List<FlowTree?>(InitialCapacity) { null };
        _currentStateStack = new List<TChild?>(InitialCapacity) { null };
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        var parentState = (TParent)context.State!;

        // ツリーとStateを決定
        FlowTree? tree;
        TChild? childState;

        if (!_hasStartedStack[depth])
        {
            // 初回：ツリーとStateを取得
            tree = _staticTree ?? _dynamicProvider!(parentState);
            _currentTreeStack[depth] = tree;

            if (tree?.Root != null)
            {
                childState = _stateProvider(parentState);
                childState.Parent = parentState;
                _currentStateStack[depth] = childState;
            }
            else
            {
                childState = null;
            }

            _hasStartedStack[depth] = true;
        }
        else
        {
            // 継続中
            tree = _currentTreeStack[depth];
            childState = _currentStateStack[depth];
        }

        // ツリーがnullまたはRootがnullの場合は失敗
        if (tree?.Root == null)
        {
            ResetAtDepth(depth);
            return NodeStatus.Failure;
        }

        // コールスタックに追加
        if (context.CallStack != null)
        {
            if (context.CallStack.Count >= context.MaxCallDepth)
            {
                ResetAtDepth(depth);
                return NodeStatus.Failure;
            }

            if (!context.CallStack.TryPush(new CallFrame(tree)))
            {
                ResetAtDepth(depth);
                return NodeStatus.Failure;
            }
        }

        // Stateを差し替えてサブツリーを実行
        var originalState = context.State;
        context.State = childState;

        var status = tree.Tick(ref context);

        // Stateを復元
        context.State = originalState;

        // コールスタックからポップ
        if (context.CallStack != null)
        {
            context.CallStack.TryPop(out _);
        }

        // 完了したらリセット
        if (status != NodeStatus.Running)
        {
            ResetAtDepth(depth);
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _hasStartedStack.Count; i++)
        {
            _hasStartedStack[i] = false;
            _currentTreeStack[i]?.Reset(fireExitEvents);
            _currentTreeStack[i] = null;
            _currentStateStack[i] = null;
        }
    }

    private void ResetAtDepth(int depth)
    {
        _hasStartedStack[depth] = false;
        _currentTreeStack[depth] = null;
        _currentStateStack[depth] = null;
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
            _currentTreeStack.Add(null);
            _currentStateStack.Add(null);
        }
    }
}
