using Tomato.Time;

namespace Tomato.FlowTree;

/// <summary>
/// フローツリー実行コンテキスト（struct）。
/// 通常はFlowTree.Tick()経由で使用され、直接操作は不要。
/// </summary>
public struct FlowContext
{
    /// <summary>
    /// 状態オブジェクト。
    /// Build&lt;T&gt;(T state)で渡された状態を保持する。
    /// </summary>
    public object? State;

    /// <summary>
    /// コールスタック（サブツリー呼び出し追跡用）。
    /// </summary>
    public FlowCallStack? CallStack;

    /// <summary>
    /// 最大コールスタック深度。
    /// </summary>
    public int MaxCallDepth;

    /// <summary>
    /// 前回からの経過tick数。
    /// </summary>
    public int DeltaTicks;

    /// <summary>
    /// 現在のゲームティック。
    /// </summary>
    public GameTick CurrentTick;

    /// <summary>
    /// 現在の呼び出し深度を取得する。
    /// 再帰呼び出し時のノード状態管理に使用。
    /// </summary>
    public readonly int CurrentCallDepth => CallStack?.Count ?? 0;

    /// <summary>
    /// Returnが要求されたかどうか。
    /// ReturnNodeによって設定される。
    /// </summary>
    public bool ReturnRequested;

    /// <summary>
    /// Returnで返すステータス。
    /// ReturnRequestedがtrueの場合に使用される。
    /// </summary>
    public NodeStatus ReturnStatus;
}
