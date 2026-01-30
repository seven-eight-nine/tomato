using System.Collections.Generic;
using Tomato.Math;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// 優先度ベースの調停ルール。
/// 高優先度のEntityは押し出されにくい。
/// </summary>
public sealed class PriorityBasedReconciliationRule : ReconciliationRule
{
    private readonly Dictionary<EntityType, int> _priorities;

    public PriorityBasedReconciliationRule()
    {
        // デフォルト優先度（高いほど押し出されにくい）
        _priorities = new Dictionary<EntityType, int>
        {
            { EntityType.Wall, 1000 },      // 壁は絶対に動かない
            { EntityType.Obstacle, 500 },   // 障害物も基本動かない
            { EntityType.Enemy, 100 },      // 大型敵
            { EntityType.Player, 50 },      // プレイヤー
            { EntityType.NPC, 30 },         // NPC
            { EntityType.Projectile, 0 }    // 飛び道具
        };
    }

    /// <summary>
    /// 特定のEntity種別の優先度を設定する。
    /// </summary>
    public void SetPriority(EntityType type, int priority)
    {
        _priorities[type] = priority;
    }

    public override void ComputePushout(
        AnyHandle entityA, EntityType typeA,
        AnyHandle entityB, EntityType typeB,
        in Vector3 normal, float penetration,
        out Vector3 pushoutA,
        out Vector3 pushoutB)
    {
        int priorityA = _priorities.TryGetValue(typeA, out var pA) ? pA : 0;
        int priorityB = _priorities.TryGetValue(typeB, out var pB) ? pB : 0;

        // 押し出しベクトル（接触法線 * 深度）
        var totalPushout = normal * penetration;

        if (priorityA == priorityB)
        {
            // 同優先度：半分ずつ押し出し
            pushoutA = -totalPushout * 0.5f;
            pushoutB = totalPushout * 0.5f;
        }
        else if (priorityA > priorityB)
        {
            // Aが高優先度：Bのみ押し出し
            pushoutA = Vector3.Zero;
            pushoutB = totalPushout;
        }
        else
        {
            // Bが高優先度：Aのみ押し出し
            pushoutA = -totalPushout;
            pushoutB = Vector3.Zero;
        }
    }
}
