namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// パス探索で使用するコンテキストの基底クラス。
/// 継承して具体的なコンテキストを実装する。
/// </summary>
public abstract class PathfindingContextBase
{
    /// <summary>
    /// 現在の探索深度。階層状態での探索に使用。
    /// </summary>
    public int CurrentDepth { get; set; }

    /// <summary>
    /// 最大探索深度。無限ループ防止。
    /// </summary>
    public int MaxDepth { get; set; } = 100;

    /// <summary>
    /// 探索がキャンセルされたか。
    /// </summary>
    public bool IsCancelled { get; private set; }

    /// <summary>
    /// 探索をキャンセル。
    /// </summary>
    public void Cancel()
    {
        IsCancelled = true;
    }

    /// <summary>
    /// 探索状態をリセット。
    /// </summary>
    public virtual void Reset()
    {
        CurrentDepth = 0;
        IsCancelled = false;
    }
}
