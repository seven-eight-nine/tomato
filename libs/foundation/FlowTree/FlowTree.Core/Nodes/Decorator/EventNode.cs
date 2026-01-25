using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// ノードに入った時のイベントハンドラ（ステートレス）。
/// </summary>
public delegate void FlowEventHandler();

/// <summary>
/// ノードに入った時のイベントハンドラ（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
/// <param name="state">状態オブジェクト</param>
public delegate void FlowEventHandler<in T>(T state) where T : class, IFlowState;

/// <summary>
/// ノードから出た時のイベントハンドラ（ステートレス）。
/// </summary>
/// <param name="result">子ノードの実行結果</param>
public delegate void FlowExitEventHandler(NodeStatus result);

/// <summary>
/// ノードから出た時のイベントハンドラ（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
/// <param name="state">状態オブジェクト</param>
/// <param name="result">子ノードの実行結果</param>
public delegate void FlowExitEventHandler<in T>(T state, NodeStatus result) where T : class, IFlowState;

/// <summary>
/// 入った瞬間/出た瞬間にイベントを発火するデコレータノード（ステートレス）。
/// OnEnter: 初回Tick時のみ発火。
/// OnExit: Success/Failureになった時のみ発火（Running中は発火しない）。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class EventNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly FlowEventHandler? _onEnter;
    private readonly FlowExitEventHandler? _onExit;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// EventNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    /// <param name="onEnter">入った瞬間に発火するイベント（nullも可）</param>
    /// <param name="onExit">出た瞬間に発火するイベント（nullも可）</param>
    public EventNode(IFlowNode child, FlowEventHandler? onEnter = null, FlowExitEventHandler? onExit = null)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _onEnter = onEnter;
        _onExit = onExit;
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // 初回Tick時にOnEnterを発火
        if (!_hasStartedStack[depth])
        {
            _hasStartedStack[depth] = true;
            _onEnter?.Invoke();
        }

        var status = _child.Tick(ref context);

        // Success/Failureの場合のみOnExitを発火
        if (status != NodeStatus.Running)
        {
            _hasStartedStack[depth] = false;
            _onExit?.Invoke(status);
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
        _child.Reset();
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
        }
    }
}

/// <summary>
/// 入った瞬間/出た瞬間にイベントを発火するデコレータノード（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class EventNode<T> : IFlowNode where T : class, IFlowState
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly FlowEventHandler<T>? _onEnter;
    private readonly FlowExitEventHandler<T>? _onExit;
    private readonly List<bool> _hasStartedStack;

    /// <summary>
    /// EventNodeを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    /// <param name="onEnter">入った瞬間に発火するイベント（nullも可）</param>
    /// <param name="onExit">出た瞬間に発火するイベント（nullも可）</param>
    public EventNode(IFlowNode child, FlowEventHandler<T>? onEnter = null, FlowExitEventHandler<T>? onExit = null)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _onEnter = onEnter;
        _onExit = onExit;
        _hasStartedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        var state = (T)context.State!;

        // 初回Tick時にOnEnterを発火
        if (!_hasStartedStack[depth])
        {
            _hasStartedStack[depth] = true;
            _onEnter?.Invoke(state);
        }

        var status = _child.Tick(ref context);

        // Success/Failureの場合のみOnExitを発火
        if (status != NodeStatus.Running)
        {
            _hasStartedStack[depth] = false;
            _onExit?.Invoke(state, status);
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
        _child.Reset();
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
        }
    }
}
