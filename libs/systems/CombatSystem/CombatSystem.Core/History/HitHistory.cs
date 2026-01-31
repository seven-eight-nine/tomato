using System.Collections.Generic;
using System.Linq;
using Tomato.Time;

namespace Tomato.CombatSystem;

/// <summary>
/// ヒット履歴を管理し、多段ヒットを制御する。
///
/// (HitGroup, Target) のペアごとにヒット回数と最終ヒットtickを記録する。
/// CombatManager.AttackTo が CanHit で判定し、成功時に RecordHit で記録する。
///
/// <example>
/// 多段ヒット攻撃の例（12tick間隔で最大5回ヒット）:
/// <code>
/// // AttackInfo側
/// info.IntervalTicks = 12;
/// info.HittableCount = 5;
///
/// // 毎tick
/// receiver.GetHitHistory().Tick(deltaTicks);
/// </code>
/// </example>
///
/// <example>
/// 1回だけヒットする攻撃:
/// <code>
/// info.HittableCount = 1;  // IntervalTicks不要
/// </code>
/// </example>
/// </summary>
public class HitHistory
{
    private readonly Dictionary<HitHistoryKey, HitHistoryEntry> _history = new();
    private readonly int _autoCleanupInterval;
    private int _currentTick;

    /// <param name="autoCleanupInterval">
    /// このtick数より古い履歴を Update 時に自動削除する。デフォルト600tick。
    /// </param>
    public HitHistory(int autoCleanupInterval = 600)
    {
        _autoCleanupInterval = autoCleanupInterval;
    }

    /// <summary>
    /// 内部tick。Tick で加算される。
    /// IntervalTicks の経過判定に使う。
    /// </summary>
    public int CurrentTick => _currentTick;

    /// <summary>
    /// 内部tickを進め、古い履歴を削除する。
    ///
    /// IntervalTicks を使う攻撃がある場合は毎tick呼ぶ。
    /// 呼ばないと内部tickが進まず、IntervalTicks 経過後の再ヒットが発生しない。
    /// </summary>
    public void Tick(int deltaTicks)
    {
        _currentTick += deltaTicks;
        CleanupExpired();
    }

    /// <summary>
    /// ヒット可能か判定する。
    ///
    /// 判定ロジック:
    /// 1. 履歴がない → true
    /// 2. hittableCount > 0 で、既に hittableCount 回ヒット済み → false
    /// 3. intervalTicks > 0 で、前回ヒットから intervalTicks tick経っていない → false
    /// 4. それ以外 → true
    /// </summary>
    /// <param name="hitGroup">攻撃のHitGroup。同じ値を持つ攻撃は履歴を共有する。</param>
    /// <param name="target">攻撃対象</param>
    /// <param name="intervalTicks">再ヒット間隔（tick）。0以下だとチェックしない。</param>
    /// <param name="hittableCount">最大ヒット数。0だと無制限。</param>
    public bool CanHit(int hitGroup, IDamageReceiver target, int intervalTicks, int hittableCount)
    {
        var key = new HitHistoryKey(hitGroup, target);

        if (!_history.TryGetValue(key, out var entry))
            return true;

        if (hittableCount > 0 && entry.HitCount >= hittableCount)
            return false;

        if (intervalTicks > 0 && (_currentTick - entry.LastHitTick) < intervalTicks)
            return false;

        return true;
    }

    /// <summary>
    /// ヒットを記録する。
    /// CombatManager.AttackTo が成功時に自動で呼ぶ。
    /// </summary>
    public void RecordHit(int hitGroup, IDamageReceiver target)
    {
        var key = new HitHistoryKey(hitGroup, target);

        if (_history.TryGetValue(key, out var entry))
        {
            entry.HitCount++;
            entry.LastHitTick = _currentTick;
            _history[key] = entry;
        }
        else
        {
            _history[key] = new HitHistoryEntry
            {
                HitCount = 1,
                LastHitTick = _currentTick
            };
        }
    }

    /// <summary>
    /// 指定 HitGroup の履歴をすべて削除する。
    /// 攻撃終了時に呼ぶと、次回同じ HitGroup で再度ヒットできる。
    /// </summary>
    public void ClearHitGroup(int hitGroup)
    {
        var keysToRemove = _history.Keys
            .Where(k => k.HitGroup == hitGroup)
            .ToList();

        foreach (var key in keysToRemove)
            _history.Remove(key);
    }

    /// <summary>全履歴を削除する。</summary>
    public void Clear() => _history.Clear();

    /// <summary>現在の履歴エントリ数。</summary>
    public int Count => _history.Count;

    /// <summary>指定の (HitGroup, Target) ペアのヒット回数を取得する。</summary>
    public int GetHitCount(int hitGroup, IDamageReceiver target)
    {
        var key = new HitHistoryKey(hitGroup, target);
        return _history.TryGetValue(key, out var entry) ? entry.HitCount : 0;
    }

    private void CleanupExpired()
    {
        var keysToRemove = _history
            .Where(kv => (_currentTick - kv.Value.LastHitTick) > _autoCleanupInterval)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToRemove)
            _history.Remove(key);
    }
}
