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
    /// <param name="onEnter">入った瞬間に発火するイベント（nullも可）</param>
    /// <param name="onExit">出た瞬間に発火するイベント（nullも可）</param>
    /// <param name="child">子ノード</param>
    public EventNode(FlowEventHandler? onEnter, FlowExitEventHandler? onExit, IFlowNode child)
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
    public void Reset(bool fireExitEvents = true)
    {
        for (int d = 0; d < _hasStartedStack.Count; d++)
        {
            if (fireExitEvents && _hasStartedStack[d])
            {
                _onExit?.Invoke(NodeStatus.Failure);
            }
            _hasStartedStack[d] = false;
        }
        _child.Reset(fireExitEvents);
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
    private T? _lastState;

    /// <summary>
    /// EventNodeを作成する。
    /// </summary>
    /// <param name="onEnter">入った瞬間に発火するイベント（nullも可）</param>
    /// <param name="onExit">出た瞬間に発火するイベント（nullも可）</param>
    /// <param name="child">子ノード</param>
    public EventNode(FlowEventHandler<T>? onEnter, FlowExitEventHandler<T>? onExit, IFlowNode child)
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
        _lastState = state;

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
    public void Reset(bool fireExitEvents = true)
    {
        for (int d = 0; d < _hasStartedStack.Count; d++)
        {
            if (fireExitEvents && _hasStartedStack[d] && _lastState != null)
            {
                _onExit?.Invoke(_lastState, NodeStatus.Failure);
            }
            _hasStartedStack[d] = false;
        }
        _lastState = null;
        _child.Reset(fireExitEvents);
    }

    private void EnsureDepth(int depth)
    {
        while (_hasStartedStack.Count <= depth)
        {
            _hasStartedStack.Add(false);
        }
    }
}
