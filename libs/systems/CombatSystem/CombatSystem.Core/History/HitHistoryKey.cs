using System;

namespace Tomato.CombatSystem;

/// <summary>
/// ヒット履歴のキー。(HitGroup, Target) の組み合わせ。
/// HitHistory 内部で Dictionary のキーとして使用。
/// </summary>
public readonly struct HitHistoryKey : IEquatable<HitHistoryKey>
{
    /// <summary>攻撃の HitGroup。同じ値を持つ攻撃は履歴を共有する。</summary>
    public readonly int HitGroup;

    /// <summary>攻撃対象。IDamageReceiver.Equals で同一判定する。</summary>
    public readonly IDamageReceiver Target;

    public HitHistoryKey(int hitGroup, IDamageReceiver target)
    {
        HitGroup = hitGroup;
        Target = target;
    }

    public bool Equals(HitHistoryKey other)
        => HitGroup == other.HitGroup &&
           (Target?.Equals(other.Target) ?? other.Target == null);

    public override bool Equals(object? obj)
        => obj is HitHistoryKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(HitGroup, Target?.GetHashCode() ?? 0);

    public static bool operator ==(HitHistoryKey left, HitHistoryKey right) => left.Equals(right);
    public static bool operator !=(HitHistoryKey left, HitHistoryKey right) => !left.Equals(right);
}
