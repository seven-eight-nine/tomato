namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// パス探索の結果を表す列挙型。
/// </summary>
public enum PathfindingResult
{
    /// <summary>
    /// パスが見つかった。
    /// </summary>
    Found,

    /// <summary>
    /// パスが見つからなかった。
    /// </summary>
    NotFound,

    /// <summary>
    /// 探索がタイムアウトした（部分結果あり）。
    /// </summary>
    Timeout,

    /// <summary>
    /// 探索がキャンセルされた。
    /// </summary>
    Cancelled,

    /// <summary>
    /// 開始状態が存在しない。
    /// </summary>
    InvalidStart,

    /// <summary>
    /// 終了状態が存在しない。
    /// </summary>
    InvalidGoal
}
