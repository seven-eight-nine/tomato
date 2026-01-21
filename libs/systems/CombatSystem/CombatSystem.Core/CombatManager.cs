using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.HandleSystem;

namespace Tomato.CombatSystem;

/// <summary>
/// 攻撃ハンドルの管理と攻撃実行を担当する。
///
/// <example>
/// <code>
/// var combat = new CombatManager();
/// var handle = combat.CreateAttack(new MyAttackInfo { Attacker = player });
/// var result = combat.AttackTo(handle, targetBody);
/// if (result.IsSuccess) PlayHitEffect();
/// combat.ReleaseAttack(handle);
/// </code>
/// </example>
/// </summary>
public class CombatManager
{
    private readonly AttackArena _attackArena;
    private int _nextHitGroup = 1;

    public CombatManager(int initialAttackCapacity = 128)
    {
        _attackArena = new AttackArena(
            initialCapacity: initialAttackCapacity,
            onSpawn: (ref Attack attack) =>
            {
                attack.HitCount = 0;
                attack.ElapsedTime = 0f;
            },
            onDespawn: (ref Attack attack) =>
            {
                attack.Info = null;
            }
        );
    }

    /// <summary>攻撃を作成しハンドルを返す。</summary>
    public AttackHandle CreateAttack(AttackInfo info)
    {
        var handle = _attackArena.Create();

        lock (_attackArena.LockObject)
        {
            ref var attack = ref _attackArena.TryGetRefInternal(handle.Index, handle.Generation, out var valid);
            if (valid)
            {
                attack.Info = info;
                attack.ResolvedHitGroup = info.HitGroup > 0
                    ? info.HitGroup
                    : _nextHitGroup++;
            }
        }

        return handle;
    }

    /// <summary>ハンドルが有効か。</summary>
    public bool IsValid(AttackHandle handle) => handle.IsValid;

    /// <summary>攻撃を解放。</summary>
    public void ReleaseAttack(AttackHandle handle) => handle.Dispose();

    /// <summary>アクティブな攻撃数。</summary>
    public int ActiveAttackCount => _attackArena.Count;

    /// <summary>CanTargetでフィルタリング。</summary>
    public IEnumerable<DamageBody> FilterTargets(AttackHandle handle, IEnumerable<DamageBody> bodies)
    {
        AttackInfo? info;
        lock (_attackArena.LockObject)
        {
            ref var attack = ref _attackArena.TryGetRefInternal(handle.Index, handle.Generation, out var valid);
            if (!valid)
                return Array.Empty<DamageBody>();
            info = attack.Info;
        }

        if (info == null)
            return Array.Empty<DamageBody>();

        return FilterTargetsCore(info, bodies);
    }

    private static IEnumerable<DamageBody> FilterTargetsCore(AttackInfo info, IEnumerable<DamageBody> bodies)
    {
        foreach (var body in bodies)
        {
            if (body?.Owner != null && info.CanTarget(body.Owner))
                yield return body;
        }
    }

    /// <summary>単体ターゲットに攻撃。</summary>
    public AttackResult AttackTo(AttackHandle handle, DamageBody target)
    {
        if (target?.Owner == null)
            return AttackResult.InvalidTarget;

        lock (_attackArena.LockObject)
        {
            ref var attack = ref _attackArena.TryGetRefInternal(handle.Index, handle.Generation, out var valid);
            if (!valid)
                return AttackResult.InvalidHandle;

            if (!attack.CanAttack())
                return AttackResult.AttackLimitReached;

            var info = attack.Info;
            if (info == null)
                return AttackResult.InvalidHandle;

            if (!info.CanTarget(target.Owner))
                return AttackResult.TargetFiltered;

            var hitHistory = target.Owner.GetHitHistory();
            if (!hitHistory.CanHit(
                attack.ResolvedHitGroup,
                target.Owner,
                info.IntervalTime,
                info.HittableCount))
            {
                return AttackResult.HitLimitReached;
            }

            var damageInfo = new DamageInfo
            {
                AttackHandle = handle,
                AttackInfo = info,
                Target = target.Owner,
                HitBody = target
            };

            var damageResult = target.Owner.OnDamage(damageInfo);

            hitHistory.RecordHit(attack.ResolvedHitGroup, target.Owner);
            attack.RecordHit();

            return new AttackResult
            {
                Status = AttackResultStatus.Success,
                DamageResult = damageResult
            };
        }
    }

    /// <summary>複数ターゲットに攻撃。Priority降順、同一Owner重複排除。</summary>
    public IReadOnlyList<AttackResult> AttackTo(AttackHandle handle, IEnumerable<DamageBody> targets)
    {
        var results = new List<AttackResult>();

        if (!handle.IsValid)
        {
            results.Add(AttackResult.InvalidHandle);
            return results;
        }

        var sortedTargets = targets
            .Where(t => t?.Owner != null)
            .OrderByDescending(t => t.Priority)
            .ToList();

        var seen = new HashSet<IDamageReceiver>();

        foreach (var target in sortedTargets)
        {
            if (!seen.Add(target.Owner!))
                continue;

            var result = AttackTo(handle, target);
            results.Add(result);

            if (result.Status == AttackResultStatus.AttackLimitReached)
                break;
        }

        return results;
    }
}
