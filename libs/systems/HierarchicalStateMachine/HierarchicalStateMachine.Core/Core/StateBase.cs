namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態の基底クラス。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public abstract class StateBase<TContext> : IState<TContext>
{
    public StateId Id { get; }

    protected StateBase(StateId id)
    {
        Id = id;
    }

    public virtual void OnEnter(TContext context) { }
    public virtual void OnExit(TContext context) { }
    public virtual void OnUpdate(TContext context, float deltaTime) { }
}
