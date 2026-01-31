using Tomato.HandleSystem;

namespace Tomato.CombatSystem;

/// <summary>
/// 攻撃の状態。HandleSystemで管理される。
/// AttackHandleとAttackArenaがSource Generatorで生成される。
/// </summary>
[Handleable(InitialCapacity = 128)]
public partial struct Attack
{
    internal AttackInfo? Info;
    internal int ResolvedHitGroup;
    internal int HitCount;
    internal int ElapsedTicks;

    /// <summary>攻撃情報を取得。</summary>
    [HandleableMethod]
    public AttackInfo? GetInfo() => Info;

    /// <summary>まだ攻撃可能か（AttackableCount未到達）。</summary>
    [HandleableMethod]
    public bool CanAttack()
        => Info != null && (Info.AttackableCount == 0 || HitCount < Info.AttackableCount);

    /// <summary>ヒットを記録。</summary>
    [HandleableMethod]
    public void RecordHit() => HitCount++;

    /// <summary>経過tickを更新。</summary>
    [HandleableMethod]
    public void UpdateTicks(int deltaTicks) => ElapsedTicks += deltaTicks;

    /// <summary>現在のヒット数。</summary>
    [HandleableMethod]
    public int GetHitCount() => HitCount;

    /// <summary>経過tick。</summary>
    [HandleableMethod]
    public int GetElapsedTicks() => ElapsedTicks;

    /// <summary>解決済みHitGroup。</summary>
    [HandleableMethod]
    public int GetResolvedHitGroup() => ResolvedHitGroup;
}
