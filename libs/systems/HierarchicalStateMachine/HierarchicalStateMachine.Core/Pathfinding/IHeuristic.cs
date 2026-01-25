namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// A*探索で使用するヒューリスティック関数のインターフェース。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public interface IHeuristic<TContext>
{
    /// <summary>
    /// 現在の状態からゴール状態までの推定コストを計算。
    /// </summary>
    /// <param name="current">現在の状態ID</param>
    /// <param name="goal">ゴール状態ID</param>
    /// <param name="context">コンテキスト</param>
    /// <returns>推定コスト（0以上、実際のコスト以下であること）</returns>
    float Estimate(StateId current, StateId goal, TContext context);
}
