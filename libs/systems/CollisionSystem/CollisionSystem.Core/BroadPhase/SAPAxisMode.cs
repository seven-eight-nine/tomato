namespace Tomato.CollisionSystem;

/// <summary>
/// SAP（Sweep and Prune）のソート軸モード。
/// </summary>
public enum SAPAxisMode
{
    /// <summary>
    /// X軸でソート・判定（デフォルト）。
    /// </summary>
    X,

    /// <summary>
    /// Z軸でソート・判定。
    /// </summary>
    Z,

    /// <summary>
    /// X軸でソート、Z軸も追加判定。
    /// </summary>
    XZ,
}
