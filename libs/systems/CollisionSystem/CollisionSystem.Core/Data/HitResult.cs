using System.Runtime.InteropServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突判定結果。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct HitResult
{
    /// <summary>
    /// ヒットした Shape のインデックス。
    /// </summary>
    public int ShapeIndex;

    /// <summary>
    /// 距離または TOI（Time of Impact）。
    /// </summary>
    public float Distance;

    /// <summary>
    /// 接触点。
    /// </summary>
    public Vector3 Point;

    /// <summary>
    /// 接触面の法線（Shape 表面から外向き）。
    /// </summary>
    public Vector3 Normal;

    public HitResult(int shapeIndex, float distance, Vector3 point, Vector3 normal)
    {
        ShapeIndex = shapeIndex;
        Distance = distance;
        Point = point;
        Normal = normal;
    }

    /// <summary>
    /// 無効な結果。
    /// </summary>
    public static HitResult None => new(-1, float.MaxValue, Vector3.Zero, Vector3.Zero);

    /// <summary>
    /// 有効な結果かどうか。
    /// </summary>
    public readonly bool IsValid => ShapeIndex >= 0;

    public override readonly string ToString()
        => IsValid
            ? $"Hit(Shape={ShapeIndex}, Dist={Distance:F3}, Point={Point}, Normal={Normal})"
            : "Hit(None)";
}
