using Tomato.Math;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// 調停ルールを定義する基底クラス。
/// </summary>
public abstract class ReconciliationRule
{
    /// <summary>
    /// 2つのEntityが衝突した際の押し出し処理を行う。
    /// </summary>
    /// <param name="entityA">Entity A</param>
    /// <param name="typeA">Entity Aの種別</param>
    /// <param name="entityB">Entity B</param>
    /// <param name="typeB">Entity Bの種別</param>
    /// <param name="normal">衝突法線（AからBへの方向）</param>
    /// <param name="penetration">貫通深度</param>
    /// <param name="pushoutA">Aの押し出しベクトル（出力）</param>
    /// <param name="pushoutB">Bの押し出しベクトル（出力）</param>
    public abstract void ComputePushout(
        AnyHandle entityA, EntityType typeA,
        AnyHandle entityB, EntityType typeB,
        in Vector3 normal, float penetration,
        out Vector3 pushoutA,
        out Vector3 pushoutB);
}
