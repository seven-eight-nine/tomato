using System;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突接触情報。
/// 2つの形状が衝突した際の詳細情報を保持する。
/// </summary>
public readonly struct CollisionContact
{
    /// <summary>衝突点（ワールド座標）。</summary>
    public readonly Vector3 Point;

    /// <summary>衝突法線（第1オブジェクトから第2オブジェクトへの方向）。</summary>
    public readonly Vector3 Normal;

    /// <summary>貫通深度（正の値は重なりを示す）。</summary>
    public readonly float Penetration;

    public CollisionContact(Vector3 point, Vector3 normal, float penetration)
    {
        Point = point;
        Normal = normal;
        Penetration = penetration;
    }

    /// <summary>衝突なしを表す値。</summary>
    public static CollisionContact None => new(Vector3.Zero, Vector3.Zero, 0f);

    public override string ToString()
        => $"Contact(Point={Point}, Normal={Normal}, Penetration={Penetration})";
}
