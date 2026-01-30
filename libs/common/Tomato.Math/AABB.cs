using System;
using System.Runtime.CompilerServices;

namespace Tomato.Math;

/// <summary>
/// 軸平行境界ボックス (Axis-Aligned Bounding Box)。
/// 空間分割と広域衝突判定に使用される。
/// </summary>
public readonly struct AABB : IEquatable<AABB>
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public Vector3 Center
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Min + Max) * 0.5f;
    }

    public Vector3 Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Max - Min;
    }

    public Vector3 Extents
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Size * 0.5f;
    }

    /// <summary>
    /// 他のAABBと交差しているか判定する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in AABB other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X
            && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
            && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>
    /// 点がAABB内に含まれるか判定する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vector3 point)
    {
        return point.X >= Min.X && point.X <= Max.X
            && point.Y >= Min.Y && point.Y <= Max.Y
            && point.Z >= Min.Z && point.Z <= Max.Z;
    }

    /// <summary>
    /// AABBを指定量だけ拡張する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AABB Expand(float amount)
    {
        var expansion = new Vector3(amount, amount, amount);
        return new AABB(Min - expansion, Max + expansion);
    }

    /// <summary>
    /// 2つのAABBを包含する最小のAABBを返す。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AABB Merge(in AABB a, in AABB b)
    {
        return new AABB(
            Vector3.Min(a.Min, b.Min),
            Vector3.Max(a.Max, b.Max));
    }

    /// <summary>
    /// 中心とサイズからAABBを作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AABB FromCenterSize(Vector3 center, Vector3 size)
    {
        var extents = size * 0.5f;
        return new AABB(center - extents, center + extents);
    }

    /// <summary>
    /// 中心と半径からAABBを作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AABB FromCenterRadius(Vector3 center, float radius)
    {
        var extents = new Vector3(radius, radius, radius);
        return new AABB(center - extents, center + extents);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AABB other)
        => Min == other.Min && Max == other.Max;

    public override bool Equals(object? obj)
        => obj is AABB other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        unchecked
        {
            return Min.GetHashCode() * 31 + Max.GetHashCode();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(AABB left, AABB right)
        => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(AABB left, AABB right)
        => !left.Equals(right);

    public override string ToString()
        => $"AABB({Min} - {Max})";
}
