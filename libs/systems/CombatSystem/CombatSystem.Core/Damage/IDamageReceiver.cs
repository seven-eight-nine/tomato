using System;

namespace Tomato.CombatSystem;

/// <summary>
/// ダメージを受ける主体。アプリ側で実装する。
///
/// IEquatable.Equals を正しく実装すること。
/// HitHistory が同一ターゲット判定に使う。
///
/// <example>
/// <code>
/// public class Character : IDamageReceiver
/// {
///     public int Id { get; }
///     private readonly HitHistory _hitHistory = new();
///
///     public DamageResult OnDamage(DamageInfo info)
///     {
///         var damage = (info.AttackInfo as MyAttackInfo)?.Damage ?? 10f;
///         Health -= damage;
///         return new DamageResult { Applied = true, ActualDamage = damage };
///     }
///
///     public HitHistory GetHitHistory() => _hitHistory;
///
///     public bool Equals(IDamageReceiver? other)
///         => other is Character c &amp;&amp; c.Id == Id;
/// }
/// </code>
/// </example>
/// </summary>
public interface IDamageReceiver : IEquatable<IDamageReceiver>
{
    /// <summary>ダメージを受けた時に呼ばれる。ダメージ計算・HP減少を行う。</summary>
    DamageResult OnDamage(DamageInfo damageInfo);

    /// <summary>
    /// ヒット履歴を返す。
    /// IntervalTime を使う場合は毎フレーム Update を呼ぶこと。
    /// </summary>
    HitHistory GetHitHistory();
}
