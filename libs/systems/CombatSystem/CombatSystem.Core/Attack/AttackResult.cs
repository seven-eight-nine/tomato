namespace Tomato.CombatSystem;

/// <summary>
/// 攻撃結果のステータス。
/// </summary>
public enum AttackResultStatus
{
    /// <summary>成功</summary>
    Success,
    /// <summary>無効なハンドル（解放済み等）</summary>
    InvalidHandle,
    /// <summary>無効なターゲット（null等）</summary>
    InvalidTarget,
    /// <summary>CanTargetがfalse</summary>
    TargetFiltered,
    /// <summary>同一ターゲットへのヒット上限（HittableCount）</summary>
    HitLimitReached,
    /// <summary>全体のヒット上限（AttackableCount）</summary>
    AttackLimitReached
}

/// <summary>
/// 攻撃実行の結果。
/// </summary>
public struct AttackResult
{
    /// <summary>ステータス。</summary>
    public AttackResultStatus Status;

    /// <summary>ダメージ結果。Successの場合のみ有効。</summary>
    public DamageResult DamageResult;

    /// <summary>成功かどうか。</summary>
    public bool IsSuccess => Status == AttackResultStatus.Success;

    public static AttackResult InvalidHandle => new() { Status = AttackResultStatus.InvalidHandle };
    public static AttackResult InvalidTarget => new() { Status = AttackResultStatus.InvalidTarget };
    public static AttackResult TargetFiltered => new() { Status = AttackResultStatus.TargetFiltered };
    public static AttackResult HitLimitReached => new() { Status = AttackResultStatus.HitLimitReached };
    public static AttackResult AttackLimitReached => new() { Status = AttackResultStatus.AttackLimitReached };
}
