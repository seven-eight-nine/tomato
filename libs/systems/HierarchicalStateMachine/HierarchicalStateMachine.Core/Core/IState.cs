namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態インターフェース。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public interface IState<TContext>
{
    /// <summary>
    /// 状態の識別子。
    /// </summary>
    StateId Id { get; }

    /// <summary>
    /// 状態に入った時に呼び出される。
    /// </summary>
    void OnEnter(TContext context);

    /// <summary>
    /// 状態から出る時に呼び出される。
    /// </summary>
    void OnExit(TContext context);

    /// <summary>
    /// 状態の更新処理。
    /// </summary>
    void OnUpdate(TContext context, float deltaTime);
}
