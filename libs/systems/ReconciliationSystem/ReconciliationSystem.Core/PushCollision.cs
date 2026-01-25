using Tomato.Math;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// 押し出し衝突情報。2つのEntityの衝突を表す。
/// </summary>
public readonly struct PushCollision
{
    /// <summary>
    /// 衝突したEntity A。
    /// </summary>
    public readonly AnyHandle EntityA;

    /// <summary>
    /// 衝突したEntity B。
    /// </summary>
    public readonly AnyHandle EntityB;

    /// <summary>
    /// 衝突法線（AからBへの方向）。
    /// </summary>
    public readonly Vector3 Normal;

    /// <summary>
    /// 貫通深度。
    /// </summary>
    public readonly float Penetration;

    public PushCollision(AnyHandle entityA, AnyHandle entityB, in Vector3 normal, float penetration)
    {
        EntityA = entityA;
        EntityB = entityB;
        Normal = normal;
        Penetration = penetration;
    }
}
