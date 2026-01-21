namespace Tomato.CombatSystem;

/// <summary>
/// ダメージ処理の結果。<see cref="IDamageReceiver.OnDamage"/>の戻り値。
/// </summary>
public struct DamageResult
{
    /// <summary>
    /// ダメージが適用されたか。
    /// </summary>
    public bool Applied { get; set; }

    /// <summary>
    /// 実際に与えたダメージ量。
    /// </summary>
    public float ActualDamage { get; set; }

    /// <summary>
    /// ターゲットが死亡したか。
    /// </summary>
    public bool Killed { get; set; }

    /// <summary>
    /// ブロックされたか。
    /// </summary>
    public bool Blocked { get; set; }
}
