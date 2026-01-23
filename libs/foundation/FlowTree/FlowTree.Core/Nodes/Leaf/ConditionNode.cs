using System;

namespace Tomato.FlowTree;

/// <summary>
/// 条件を評価するデリゲート。
/// </summary>
/// <param name="context">実行コンテキスト</param>
/// <returns>条件が満たされた場合はtrue</returns>
public delegate bool FlowCondition(ref FlowContext context);

/// <summary>
/// 条件を評価するノード。
/// trueならSuccess、falseならFailureを返す。
/// </summary>
public sealed class ConditionNode : IFlowNode
{
    private readonly FlowCondition _condition;

    /// <summary>
    /// ConditionNodeを作成する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public ConditionNode(FlowCondition condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return _condition(ref context) ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // ConditionNodeは状態を持たないためリセット不要
    }
}
