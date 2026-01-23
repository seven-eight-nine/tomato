using System;

namespace Tomato.FlowTree;

/// <summary>
/// アクションを実行するデリゲート。
/// </summary>
/// <param name="context">実行コンテキスト</param>
/// <returns>ノードの状態</returns>
public delegate NodeStatus FlowAction(ref FlowContext context);

/// <summary>
/// アクションを実行するノード。
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
        return _action(ref context);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // ActionNodeは状態を持たないためリセット不要
    }
}
