namespace Tomato.CombatSystem;

/// <summary>
/// ヒット履歴の1エントリ。HitHistory 内部で使用。
/// </summary>
public struct HitHistoryEntry
{
    /// <summary>ヒット回数。HittableCount との比較に使う。</summary>
    public int HitCount;

    /// <summary>最終ヒットtick（HitHistory.CurrentTick 基準）。IntervalTicks との比較に使う。</summary>
    public int LastHitTick;
}
