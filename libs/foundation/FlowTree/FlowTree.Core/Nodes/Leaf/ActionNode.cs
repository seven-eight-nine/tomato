using System;

namespace Tomato.FlowTree;

/// <summary>
/// アクションを実行するデリゲート（ステートレス）。
/// </summary>
/// <returns>ノードの状態</returns>
public delegate NodeStatus FlowAction();

/// <summary>
/// アクションを実行するデリゲート（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
/// <param name="state">状態オブジェクト</param>
/// <returns>ノードの状態</returns>
public delegate NodeStatus FlowAction<in T>(T state) where T : class, IFlowState;

/// <summary>
/// アクションを実行するノード（ステートレス）。
/// </summary>
public sealed class ActionNode : IFlowNode
{
    private readonly FlowAction _action;

    /// <summary>
    /// ActionNodeを作成する。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public ActionNode(FlowAction action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _action();
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
    }
}

/// <summary>
/// アクションを実行するノード（型付き）。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class ActionNode<T> : IFlowNode where T : class, IFlowState
{
    private readonly FlowAction<T> _action;

    /// <summary>
    /// ActionNodeを作成する。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public ActionNode(FlowAction<T> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _action((T)context.State!);
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
    }
}
