namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 常にゼロを返すヒューリスティック。
/// これを使用するとA*はダイクストラ法と等価になる。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class ZeroHeuristic<TContext> : IHeuristic<TContext>
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly ZeroHeuristic<TContext> Instance = new();

    private ZeroHeuristic() { }

    public float Estimate(StateId current, StateId goal, TContext context) => 0f;
}
