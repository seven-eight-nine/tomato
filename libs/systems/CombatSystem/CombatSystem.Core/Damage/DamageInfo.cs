namespace Tomato.CombatSystem;

/// <summary>
/// ダメージ情報。<see cref="IDamageReceiver.OnDamage"/>に渡される。
/// </summary>
public class DamageInfo
{
    /// <summary>
    /// 攻撃ハンドル。
    /// </summary>
    public AttackHandle AttackHandle { get; set; }

    /// <summary>
    /// 攻撃情報。アプリ側の派生クラスにキャストして使う。
    /// </summary>
    public AttackInfo AttackInfo { get; set; } = null!;

    /// <summary>
    /// ダメージを受けるターゲット。
    /// </summary>
    public IDamageReceiver Target { get; set; } = null!;

    /// <summary>
    /// ヒットした部位。
    /// </summary>
    public DamageBody HitBody { get; set; } = null!;
}
