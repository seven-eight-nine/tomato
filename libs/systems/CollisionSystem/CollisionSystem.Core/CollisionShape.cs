using System;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突形状の種類。
/// </summary>
public enum ShapeType
{
    Sphere,
    Capsule,
    Box,
    Cylinder,
    Mesh
}

/// <summary>
/// カプセルの方向。
/// </summary>
public enum CapsuleDirection
{
    X,
    Y,
    Z
}

/// <summary>
/// 衝突判定の形状を表す基底クラス。
/// </summary>
public abstract class CollisionShape
{
    /// <summary>形状の種類。</summary>
    public abstract ShapeType Type { get; }

    /// <summary>形状のオフセット。</summary>
    public abstract Vector3 Offset { get; }

    /// <summary>形状を包含するAABBを取得する。</summary>
    public abstract AABB GetBounds(Vector3 position);

    /// <summary>他の形状との衝突判定。</summary>
    public abstract bool Intersects(
        Vector3 thisPos,
        CollisionShape other,
        Vector3 otherPos,
        out CollisionContact contact);
}

/// <summary>
/// 球形状。
/// </summary>
public sealed class SphereShape : CollisionShape
{
    public readonly float Radius;
    private readonly Vector3 _offset;

    public override ShapeType Type => ShapeType.Sphere;
    public override Vector3 Offset => _offset;

    public SphereShape(float radius, Vector3 offset = default)
    {
        Radius = radius;
        _offset = offset;
    }

    public override AABB GetBounds(Vector3 position)
    {
        var center = position + _offset;
        var extents = new Vector3(Radius, Radius, Radius);
        return new AABB(center - extents, center + extents);
    }

    public override bool Intersects(
        Vector3 thisPos,
        CollisionShape other,
        Vector3 otherPos,
        out CollisionContact contact)
    {
        var thisCenter = thisPos + _offset;

        return other switch
        {
            SphereShape sphere => IntersectSphereSphere(thisCenter, Radius, otherPos + sphere.Offset, sphere.Radius, out contact),
            CapsuleShape capsule => IntersectSphereCapsule(thisCenter, Radius, capsule, otherPos, out contact),
            BoxShape box => IntersectSphereBox(thisCenter, Radius, box, otherPos, out contact),
            _ => throw new NotSupportedException($"Unsupported shape: {other.Type}")
        };
    }

    private static bool IntersectSphereSphere(
        Vector3 center1, float radius1,
        Vector3 center2, float radius2,
        out CollisionContact contact)
    {
        var diff = center2 - center1;
        var distSq = diff.LengthSquared;
        var radiusSum = radius1 + radius2;

        if (distSq > radiusSum * radiusSum)
        {
            contact = CollisionContact.None;
            return false;
        }

        var dist = MathF.Sqrt(distSq);
        var normal = dist > float.Epsilon ? diff * (1f / dist) : Vector3.UnitY;
        var penetration = radiusSum - dist;
        var point = center1 + normal * (radius1 - penetration * 0.5f);

        contact = new CollisionContact(point, normal, penetration);
        return true;
    }

    private static bool IntersectSphereCapsule(
        Vector3 sphereCenter, float sphereRadius,
        CapsuleShape capsule, Vector3 capsulePos,
        out CollisionContact contact)
    {
        // カプセルの線分端点を計算
        var halfHeight = capsule.Height * 0.5f;
        var direction = capsule.Direction switch
        {
            CapsuleDirection.X => Vector3.UnitX,
            CapsuleDirection.Y => Vector3.UnitY,
            CapsuleDirection.Z => Vector3.UnitZ,
            _ => Vector3.UnitY
        };

        var capsuleCenter = capsulePos + capsule.Offset;
        var point1 = capsuleCenter - direction * halfHeight;
        var point2 = capsuleCenter + direction * halfHeight;

        // 球の中心から線分への最近接点を求める
        var closest = ClosestPointOnLineSegment(sphereCenter, point1, point2);

        // 球同士の衝突判定に帰着
        return IntersectSphereSphere(sphereCenter, sphereRadius, closest, capsule.Radius, out contact);
    }

    private static Vector3 ClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        var line = lineEnd - lineStart;
        var lineLengthSq = line.LengthSquared;

        if (lineLengthSq < float.Epsilon)
            return lineStart;

        var t = MathF.Max(0f, MathF.Min(1f, Vector3.Dot(point - lineStart, line) / lineLengthSq));
        return lineStart + line * t;
    }

    private static bool IntersectSphereBox(
        Vector3 sphereCenter, float sphereRadius,
        BoxShape box, Vector3 boxPos,
        out CollisionContact contact)
    {
        // 簡易実装: AABBとして扱う
        var boxCenter = boxPos + box.Offset;
        var boxMin = boxCenter - box.HalfExtents;
        var boxMax = boxCenter + box.HalfExtents;

        // 球の中心からボックスへの最近接点
        var closest = new Vector3(
            MathF.Max(boxMin.X, MathF.Min(sphereCenter.X, boxMax.X)),
            MathF.Max(boxMin.Y, MathF.Min(sphereCenter.Y, boxMax.Y)),
            MathF.Max(boxMin.Z, MathF.Min(sphereCenter.Z, boxMax.Z)));

        var diff = sphereCenter - closest;
        var distSq = diff.LengthSquared;

        if (distSq > sphereRadius * sphereRadius)
        {
            contact = CollisionContact.None;
            return false;
        }

        var dist = MathF.Sqrt(distSq);
        var normal = dist > float.Epsilon ? diff * (1f / dist) : Vector3.UnitY;
        var penetration = sphereRadius - dist;

        contact = new CollisionContact(closest, normal, penetration);
        return true;
    }
}

/// <summary>
/// カプセル形状。
/// </summary>
public sealed class CapsuleShape : CollisionShape
{
    public readonly float Radius;
    public readonly float Height;
    public readonly CapsuleDirection Direction;
    private readonly Vector3 _offset;

    public override ShapeType Type => ShapeType.Capsule;
    public override Vector3 Offset => _offset;

    public CapsuleShape(float radius, float height, CapsuleDirection direction = CapsuleDirection.Y, Vector3 offset = default)
    {
        Radius = radius;
        Height = height;
        Direction = direction;
        _offset = offset;
    }

    public override AABB GetBounds(Vector3 position)
    {
        var center = position + _offset;
        var halfHeight = Height * 0.5f;

        Vector3 min, max;

        switch (Direction)
        {
            case CapsuleDirection.X:
                min = center - new Vector3(halfHeight + Radius, Radius, Radius);
                max = center + new Vector3(halfHeight + Radius, Radius, Radius);
                break;
            case CapsuleDirection.Z:
                min = center - new Vector3(Radius, Radius, halfHeight + Radius);
                max = center + new Vector3(Radius, Radius, halfHeight + Radius);
                break;
            default: // Y
                min = center - new Vector3(Radius, halfHeight + Radius, Radius);
                max = center + new Vector3(Radius, halfHeight + Radius, Radius);
                break;
        }

        return new AABB(min, max);
    }

    public override bool Intersects(
        Vector3 thisPos,
        CollisionShape other,
        Vector3 otherPos,
        out CollisionContact contact)
    {
        // カプセル同士の衝突は複雑なので、球として簡易判定
        var thisCenter = thisPos + _offset;

        if (other is SphereShape sphere)
        {
            // 球-カプセルは球側から呼び出す
            var result = sphere.Intersects(otherPos, this, thisPos, out contact);
            // 法線を反転
            if (result)
            {
                contact = new CollisionContact(contact.Point, -contact.Normal, contact.Penetration);
            }
            return result;
        }

        contact = CollisionContact.None;
        return false;
    }
}

/// <summary>
/// ボックス形状（AABB）。
/// </summary>
public sealed class BoxShape : CollisionShape
{
    public readonly Vector3 HalfExtents;
    private readonly Vector3 _offset;

    public override ShapeType Type => ShapeType.Box;
    public override Vector3 Offset => _offset;

    public BoxShape(Vector3 halfExtents, Vector3 offset = default)
    {
        HalfExtents = halfExtents;
        _offset = offset;
    }

    public override AABB GetBounds(Vector3 position)
    {
        var center = position + _offset;
        return new AABB(center - HalfExtents, center + HalfExtents);
    }

    public override bool Intersects(
        Vector3 thisPos,
        CollisionShape other,
        Vector3 otherPos,
        out CollisionContact contact)
    {
        if (other is SphereShape sphere)
        {
            // 球-ボックスは球側から呼び出す
            var result = sphere.Intersects(otherPos, this, thisPos, out contact);
            if (result)
            {
                contact = new CollisionContact(contact.Point, -contact.Normal, contact.Penetration);
            }
            return result;
        }

        if (other is BoxShape box)
        {
            return IntersectBoxBox(thisPos + _offset, HalfExtents, otherPos + box.Offset, box.HalfExtents, out contact);
        }

        contact = CollisionContact.None;
        return false;
    }

    private static bool IntersectBoxBox(
        Vector3 center1, Vector3 half1,
        Vector3 center2, Vector3 half2,
        out CollisionContact contact)
    {
        var aabb1 = new AABB(center1 - half1, center1 + half1);
        var aabb2 = new AABB(center2 - half2, center2 + half2);

        if (!aabb1.Intersects(aabb2))
        {
            contact = CollisionContact.None;
            return false;
        }

        // 簡易的な貫通計算
        var diff = center2 - center1;
        var overlap = (half1 + half2) - new Vector3(MathF.Abs(diff.X), MathF.Abs(diff.Y), MathF.Abs(diff.Z));

        // 最小の重なり軸を見つける
        float minOverlap = overlap.X;
        var normal = new Vector3(diff.X < 0 ? -1f : 1f, 0f, 0f);

        if (overlap.Y < minOverlap)
        {
            minOverlap = overlap.Y;
            normal = new Vector3(0f, diff.Y < 0 ? -1f : 1f, 0f);
        }

        if (overlap.Z < minOverlap)
        {
            minOverlap = overlap.Z;
            normal = new Vector3(0f, 0f, diff.Z < 0 ? -1f : 1f);
        }

        var point = (center1 + center2) * 0.5f;
        contact = new CollisionContact(point, normal, minOverlap);
        return true;
    }
}
