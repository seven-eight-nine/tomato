namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 階層構造を持つ状態のインターフェース。
/// 内部にサブグラフを持つことができる。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public interface IHierarchicalState<TContext> : IState<TContext>
{
    /// <summary>
    /// サブグラフ（子状態のグラフ）。
    /// </summary>
    StateGraph<TContext>? SubGraph { get; }

    /// <summary>
    /// サブグラフ内の初期状態ID。
    /// </summary>
    StateId? InitialSubStateId { get; }

    /// <summary>
    /// サブグラフ内の現在の状態ID。
    /// </summary>
    StateId? CurrentSubStateId { get; }

    /// <summary>
    /// サブグラフ内の状態に入る。
    /// </summary>
    void EnterSubState(StateId subStateId, TContext context);

    /// <summary>
    /// サブグラフ内の状態から出る。
    /// </summary>
    void ExitSubState(TContext context);

    /// <summary>
    /// サブグラフが存在するか。
    /// </summary>
    bool HasSubGraph { get; }
}
