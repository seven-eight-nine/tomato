using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// レイクエリ。
/// </summary>
public readonly struct RayQuery
{
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;
    public readonly float MaxDistance;
    public readonly uint IncludeMask;
    public readonly uint ExcludeMask;

    public RayQuery(Vector3 origin, Vector3 direction, float maxDistance,
                    uint includeMask = 0xFFFFFFFF, uint excludeMask = 0)
    {
        Origin = origin;
        Direction = direction.Normalized;
        MaxDistance = maxDistance;
        IncludeMask = includeMask;
        ExcludeMask = excludeMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetPoint(float t) => Origin + Direction * t;

    public Vector3 End => GetPoint(MaxDistance);

    public AABB GetAABB()
    {
        var end = End;
        return new AABB(
            Vector3.Min(Origin, end),
            Vector3.Max(Origin, end));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PassesMask(uint shapeMask)
    {
        return (shapeMask & IncludeMask) != 0 && (shapeMask & ExcludeMask) == 0;
    }
}

/// <summary>
/// 球オーバーラップクエリ。
/// </summary>
public readonly struct SphereOverlapQuery
{
    public readonly Vector3 Center;
    public readonly float Radius;
    public readonly uint IncludeMask;
    public readonly uint ExcludeMask;

    public SphereOverlapQuery(Vector3 center, float radius,
                               uint includeMask = 0xFFFFFFFF, uint excludeMask = 0)
    {
        Center = center;
        Radius = radius;
        IncludeMask = includeMask;
        ExcludeMask = excludeMask;
    }

    public AABB GetAABB()
    {
        var r = new Vector3(Radius, Radius, Radius);
        return new AABB(Center - r, Center + r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PassesMask(uint shapeMask)
    {
        return (shapeMask & IncludeMask) != 0 && (shapeMask & ExcludeMask) == 0;
    }
}

/// <summary>
/// カプセルスイープクエリ（移動判定用）。
/// </summary>
public readonly struct CapsuleSweepQuery
{
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public readonly float Radius;
    public readonly uint IncludeMask;
    public readonly uint ExcludeMask;

    public CapsuleSweepQuery(Vector3 start, Vector3 end, float radius,
                              uint includeMask = 0xFFFFFFFF, uint excludeMask = 0)
    {
        Start = start;
        End = end;
        Radius = radius;
        IncludeMask = includeMask;
        ExcludeMask = excludeMask;
    }

    public AABB GetAABB()
    {
        var r = new Vector3(Radius, Radius, Radius);
        var min = Vector3.Min(Start, End) - r;
        var max = Vector3.Max(Start, End) + r;
        return new AABB(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PassesMask(uint shapeMask)
    {
        return (shapeMask & IncludeMask) != 0 && (shapeMask & ExcludeMask) == 0;
    }
}

/// <summary>
/// 斬撃線クエリ。
/// </summary>
public readonly struct SlashQuery
{
    /// <summary>
    /// 開始時の剣の根元。
    /// </summary>
    public readonly Vector3 StartBase;

    /// <summary>
    /// 開始時の剣の先端。
    /// </summary>
    public readonly Vector3 StartTip;

    /// <summary>
    /// 終了時の剣の根元。
    /// </summary>
    public readonly Vector3 EndBase;

    /// <summary>
    /// 終了時の剣の先端。
    /// </summary>
    public readonly Vector3 EndTip;

    public readonly uint IncludeMask;
    public readonly uint ExcludeMask;

    public SlashQuery(Vector3 startBase, Vector3 startTip, Vector3 endBase, Vector3 endTip,
                      uint includeMask = 0xFFFFFFFF, uint excludeMask = 0)
    {
        StartBase = startBase;
        StartTip = startTip;
        EndBase = endBase;
        EndTip = endTip;
        IncludeMask = includeMask;
        ExcludeMask = excludeMask;
    }

    public AABB GetAABB()
    {
        var min = Vector3.Min(Vector3.Min(StartBase, StartTip), Vector3.Min(EndBase, EndTip));
        var max = Vector3.Max(Vector3.Max(StartBase, StartTip), Vector3.Max(EndBase, EndTip));
        return new AABB(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PassesMask(uint shapeMask)
    {
        return (shapeMask & IncludeMask) != 0 && (shapeMask & ExcludeMask) == 0;
    }
}
