using Tomato.HierarchicalStateMachine;
using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション状態。
/// HierarchicalStateMachineの状態として使用される。
/// </summary>
public sealed class MotionState : IState<MotionContext>
{
    /// <summary>
    /// 状態の識別子。
    /// </summary>
    public StateId Id { get; }

    /// <summary>
    /// モーション定義。
    /// </summary>
    public MotionDefinition Definition { get; }

    public MotionState(MotionDefinition definition)
    {
        Definition = definition;
        Id = new StateId(definition.MotionId);
    }

    public MotionState(StateId id, MotionDefinition definition)
    {
        Id = id;
        Definition = definition;
    }

    /// <summary>
    /// 状態に入った時に呼び出される。
    /// </summary>
    public void OnEnter(MotionContext context)
    {
        context.ResetTicks();
        context.CurrentMotionState = this;
        context.Executor?.OnMotionStart(Definition.MotionId);
    }

    /// <summary>
    /// 状態から出る時に呼び出される。
    /// </summary>
    public void OnExit(MotionContext context)
    {
        context.Executor?.OnMotionEnd(Definition.MotionId);
        context.CurrentMotionState = null;
    }

    /// <summary>
    /// tick処理。
    /// </summary>
    public void OnTick(MotionContext context, int deltaTicks)
    {
        context.AdvanceTicks(deltaTicks);

        // Timelineをクエリ
        Definition.Timeline.Query(context.ElapsedTicks, deltaTicks, context.QueryContext);

        context.Executor?.OnMotionTick(Definition.MotionId, context.ElapsedTicks, deltaTicks);
    }

    /// <summary>
    /// モーションが完了したかどうか。
    /// </summary>
    public bool IsComplete(MotionContext context)
    {
        return context.ElapsedTicks >= Definition.TotalFrames;
    }
}
