using Tomato.Math;

namespace Tomato.GameLoop.Collision;

/// <summary>
/// 衝突ペア。2つのEntityの衝突情報。
/// ゲーム側がSpatialSystemで検出し、GameLoopに渡す。
/// </summary>
public readonly struct CollisionPair
{
    /// <summary>
    /// Entity AのユーザーID（SpatialWorldのuserData）。
    /// </summary>
    public readonly int EntityIdA;

    /// <summary>
    /// Entity BのユーザーID（SpatialWorldのuserData）。
    /// </summary>
    public readonly int EntityIdB;

    /// <summary>
    /// 接触点。
    /// </summary>
    public readonly Vector3 Point;

    /// <summary>
    /// 接触法線（AからBへの方向）。
    /// </summary>
    public readonly Vector3 Normal;

    public CollisionPair(int entityIdA, int entityIdB, in Vector3 point, in Vector3 normal)
    {
        EntityIdA = entityIdA;
        EntityIdB = entityIdB;
        Point = point;
        Normal = normal;
    }
}
