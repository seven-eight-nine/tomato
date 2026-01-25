using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Shape 同士の衝突判定。全てインライン静的メソッド。
/// </summary>
public static class ShapeIntersection
{
    #region Point vs Shape

    /// <summary>
    /// Point vs Sphere。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointSphere(in Vector3 point, in SphereData sphere)
    {
        return Vector3.DistanceSquared(point, sphere.Center) <= sphere.Radius * sphere.Radius;
    }

    /// <summary>
    /// Point vs Capsule。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointCapsule(in Vector3 point, in CapsuleData capsule)
    {
        var closest = MathUtils.ClosestPointOnSegment(point, capsule.Point1, capsule.Point2);
        return Vector3.DistanceSquared(point, closest) <= capsule.Radius * capsule.Radius;
    }

    /// <summary>
    /// Point vs Cylinder。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointCylinder(in Vector3 point, in CylinderData cylinder)
    {
        float localY = point.Y - cylinder.BaseCenter.Y;
        if (localY < 0 || localY > cylinder.Height)
            return false;

        float dx = point.X - cylinder.BaseCenter.X;
        float dz = point.Z - cylinder.BaseCenter.Z;
        return dx * dx + dz * dz <= cylinder.Radius * cylinder.Radius;
    }

    /// <summary>
    /// Point vs Box（Y軸回転対応）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointBox(in Vector3 point, in BoxData box)
    {
        // ボックスのローカル座標系に変換
        var local = TransformToBoxLocal(point, box);
        return MathF.Abs(local.X) <= box.HalfExtents.X &&
               MathF.Abs(local.Y) <= box.HalfExtents.Y &&
               MathF.Abs(local.Z) <= box.HalfExtents.Z;
    }

    #endregion

    #region Ray vs Shape

    /// <summary>
    /// Ray vs Sphere。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RaySphere(
        in Vector3 origin, in Vector3 direction, float maxDistance,
        in SphereData sphere,
        out float t, out Vector3 point, out Vector3 normal)
    {
        var oc = origin - sphere.Center;
        var radiusSq = sphere.Radius * sphere.Radius;
        var ocLenSq = oc.LengthSquared;

        // 内部からのヒット
        if (ocLenSq < radiusSq)
        {
            t = 0;
            point = origin;
            normal = -direction;
            return true;
        }

        var b = Vector3.Dot(oc, direction);
        var c = ocLenSq - radiusSq;
        var discriminant = b * b - c;

        if (discriminant < 0)
        {
            t = 0;
            point = normal = default;
            return false;
        }

        t = -b - MathF.Sqrt(discriminant);
        if (t < 0 || t > maxDistance)
        {
            t = 0;
            point = normal = default;
            return false;
        }

        point = origin + direction * t;
        normal = (point - sphere.Center) * (1f / sphere.Radius);
        return true;
    }

    /// <summary>
    /// Ray vs Capsule。
    /// </summary>
    public static bool RayCapsule(
        in Vector3 origin, in Vector3 direction, float maxDistance,
        in CapsuleData capsule,
        out float t, out Vector3 point, out Vector3 normal)
    {
        t = float.MaxValue;
        point = normal = default;

        var axis = capsule.Point2 - capsule.Point1;
        var axisLenSq = axis.LengthSquared;

        // カプセルが点に縮退
        if (axisLenSq < float.Epsilon)
        {
            var sphere = new SphereData(capsule.Point1, capsule.Radius);
            return RaySphere(origin, direction, maxDistance, sphere, out t, out point, out normal);
        }

        // 無限円柱との交差
        var m = origin - capsule.Point1;
        var md = Vector3.Dot(m, axis);
        var nd = Vector3.Dot(direction, axis);
        var dd = axisLenSq;

        var mn = Vector3.Dot(m, direction);
        var a = dd - nd * nd;
        var k = m.LengthSquared - capsule.Radius * capsule.Radius;
        var c = dd * k - md * md;

        bool hit = false;

        if (MathF.Abs(a) > float.Epsilon)
        {
            var b = dd * mn - nd * md;
            var discriminant = b * b - a * c;

            if (discriminant >= 0)
            {
                var sqrtDisc = MathF.Sqrt(discriminant);
                var invA = 1f / a;

                var t0 = (-b - sqrtDisc) * invA;
                if (t0 >= 0 && t0 <= maxDistance && t0 < t)
                {
                    var s = md + t0 * nd;
                    if (s >= 0 && s <= dd)
                    {
                        t = t0;
                        point = origin + direction * t;
                        var axisPoint = capsule.Point1 + axis * (s / dd);
                        normal = (point - axisPoint).Normalized;
                        hit = true;
                    }
                }
            }
        }

        // 半球キャップ
        var sphere1 = new SphereData(capsule.Point1, capsule.Radius);
        if (RaySphere(origin, direction, maxDistance, sphere1, out var t1, out var p1, out var n1) && t1 < t)
        {
            var toHit = p1 - capsule.Point1;
            if (Vector3.Dot(toHit, axis) <= 0)
            {
                t = t1;
                point = p1;
                normal = n1;
                hit = true;
            }
        }

        var sphere2 = new SphereData(capsule.Point2, capsule.Radius);
        if (RaySphere(origin, direction, maxDistance, sphere2, out var t2, out var p2, out var n2) && t2 < t)
        {
            var toHit = p2 - capsule.Point2;
            if (Vector3.Dot(toHit, axis) >= 0)
            {
                t = t2;
                point = p2;
                normal = n2;
                hit = true;
            }
        }

        return hit && t <= maxDistance;
    }

    /// <summary>
    /// Ray vs Box（Y軸回転対応）。
    /// </summary>
    public static bool RayBox(
        in Vector3 origin, in Vector3 direction, float maxDistance,
        in BoxData box,
        out float t, out Vector3 point, out Vector3 normal)
    {
        // ボックスのローカル座標系に変換
        var localOrigin = TransformToBoxLocal(origin, box);
        var localDir = TransformDirToBoxLocal(direction, box);

        // AABBとのレイ交差判定（スラブ法）
        var invDir = new Vector3(
            MathF.Abs(localDir.X) > 1e-6f ? 1f / localDir.X : float.MaxValue,
            MathF.Abs(localDir.Y) > 1e-6f ? 1f / localDir.Y : float.MaxValue,
            MathF.Abs(localDir.Z) > 1e-6f ? 1f / localDir.Z : float.MaxValue);

        var t1 = (-box.HalfExtents.X - localOrigin.X) * invDir.X;
        var t2 = (box.HalfExtents.X - localOrigin.X) * invDir.X;
        var t3 = (-box.HalfExtents.Y - localOrigin.Y) * invDir.Y;
        var t4 = (box.HalfExtents.Y - localOrigin.Y) * invDir.Y;
        var t5 = (-box.HalfExtents.Z - localOrigin.Z) * invDir.Z;
        var t6 = (box.HalfExtents.Z - localOrigin.Z) * invDir.Z;

        var tMin = MathF.Max(MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4)), MathF.Min(t5, t6));
        var tMax = MathF.Min(MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4)), MathF.Max(t5, t6));

        if (tMax < 0 || tMin > tMax || tMin > maxDistance)
        {
            t = 0;
            point = normal = default;
            return false;
        }

        // 内部からのヒット
        if (tMin < 0)
        {
            t = 0;
            point = origin;
            normal = -direction;
            return true;
        }

        t = tMin;
        var localHitPoint = localOrigin + localDir * t;
        point = origin + direction * t;

        // ローカル法線を計算し、ワールド座標系に変換
        var localNormal = ComputeBoxLocalNormal(localHitPoint, box.HalfExtents);
        normal = TransformDirFromBoxLocal(localNormal, box);

        return true;
    }

    /// <summary>
    /// Ray vs Cylinder。
    /// </summary>
    public static bool RayCylinder(
        in Vector3 origin, in Vector3 direction, float maxDistance,
        in CylinderData cylinder,
        out float t, out Vector3 point, out Vector3 normal)
    {
        t = float.MaxValue;
        point = normal = default;

        var m = origin - cylinder.BaseCenter;
        var dy = direction.Y;

        bool hit = false;

        // 側面との交差
        var dx = direction.X;
        var dz = direction.Z;
        var mx = m.X;
        var mz = m.Z;

        var a = dx * dx + dz * dz;
        var b = mx * dx + mz * dz;
        var c = mx * mx + mz * mz - cylinder.Radius * cylinder.Radius;

        if (a > float.Epsilon)
        {
            var discriminant = b * b - a * c;
            if (discriminant >= 0)
            {
                var sqrtDisc = MathF.Sqrt(discriminant);
                var invA = 1f / a;

                var t0 = (-b - sqrtDisc) * invA;
                if (t0 >= 0 && t0 <= maxDistance && t0 < t)
                {
                    var y = m.Y + t0 * dy;
                    if (y >= 0 && y <= cylinder.Height)
                    {
                        t = t0;
                        point = origin + direction * t;
                        normal = new Vector3(point.X - cylinder.BaseCenter.X, 0, point.Z - cylinder.BaseCenter.Z).Normalized;
                        hit = true;
                    }
                }
            }
        }

        // 底面キャップ
        if (MathF.Abs(dy) > float.Epsilon)
        {
            var tBottom = -m.Y / dy;
            if (tBottom >= 0 && tBottom <= maxDistance && tBottom < t)
            {
                var hitPoint = origin + direction * tBottom;
                var dx2 = hitPoint.X - cylinder.BaseCenter.X;
                var dz2 = hitPoint.Z - cylinder.BaseCenter.Z;
                if (dx2 * dx2 + dz2 * dz2 <= cylinder.Radius * cylinder.Radius)
                {
                    t = tBottom;
                    point = hitPoint;
                    normal = new Vector3(0, -1, 0);
                    hit = true;
                }
            }

            // 上面キャップ
            var tTop = (cylinder.Height - m.Y) / dy;
            if (tTop >= 0 && tTop <= maxDistance && tTop < t)
            {
                var hitPoint = origin + direction * tTop;
                var dx2 = hitPoint.X - cylinder.BaseCenter.X;
                var dz2 = hitPoint.Z - cylinder.BaseCenter.Z;
                if (dx2 * dx2 + dz2 * dz2 <= cylinder.Radius * cylinder.Radius)
                {
                    t = tTop;
                    point = hitPoint;
                    normal = new Vector3(0, 1, 0);
                    hit = true;
                }
            }
        }

        // 内部からの判定
        if (!hit)
        {
            // 内部にいるかチェック
            if (PointCylinder(origin, cylinder))
            {
                t = 0;
                point = origin;
                normal = -direction;
                return true;
            }
        }

        return hit;
    }

    #endregion

    #region Sphere vs Shape

    /// <summary>
    /// Sphere vs Sphere。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SphereSphere(
        in SphereData a, in SphereData b,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        var diff = a.Center - b.Center;
        var distSq = diff.LengthSquared;
        var totalRadius = a.Radius + b.Radius;

        if (distSq > totalRadius * totalRadius)
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        var dist = MathF.Sqrt(distSq);
        normal = dist > 1e-6f ? diff * (1f / dist) : Vector3.UnitY;
        distance = totalRadius - dist;
        point = b.Center + normal * b.Radius;
        return true;
    }

    /// <summary>
    /// Sphere vs Capsule。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SphereCapsule(
        in SphereData sphere, in CapsuleData capsule,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        var closest = MathUtils.ClosestPointOnSegment(sphere.Center, capsule.Point1, capsule.Point2);
        var closestSphere = new SphereData(closest, capsule.Radius);
        return SphereSphere(sphere, closestSphere, out point, out normal, out distance);
    }

    /// <summary>
    /// Sphere vs Box（Y軸回転対応）。
    /// </summary>
    public static bool SphereBox(
        in SphereData sphere, in BoxData box,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        // 球の中心をボックスのローカル座標系に変換
        var localCenter = TransformToBoxLocal(sphere.Center, box);

        // ローカル座標系でAABBへの最近接点を計算
        var closest = new Vector3(
            MathUtils.Clamp(localCenter.X, -box.HalfExtents.X, box.HalfExtents.X),
            MathUtils.Clamp(localCenter.Y, -box.HalfExtents.Y, box.HalfExtents.Y),
            MathUtils.Clamp(localCenter.Z, -box.HalfExtents.Z, box.HalfExtents.Z));

        var diff = localCenter - closest;
        var distSq = diff.LengthSquared;

        if (distSq > sphere.Radius * sphere.Radius)
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        var dist = MathF.Sqrt(distSq);

        // ローカル法線をワールド座標系に変換
        Vector3 localNormal;
        if (dist > 1e-6f)
        {
            localNormal = diff * (1f / dist);
        }
        else
        {
            // 球の中心がボックス内部にある場合、最も近い面への法線を使用
            localNormal = ComputeBoxLocalNormal(localCenter, box.HalfExtents);
        }

        normal = TransformDirFromBoxLocal(localNormal, box);
        point = TransformFromBoxLocal(closest, box);
        distance = sphere.Radius - dist;
        return true;
    }

    /// <summary>
    /// Sphere vs Cylinder。
    /// </summary>
    public static bool SphereCylinder(
        in SphereData sphere, in CylinderData cylinder,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        var localY = sphere.Center.Y - cylinder.BaseCenter.Y;
        var clampedY = MathUtils.Clamp(localY, 0f, cylinder.Height);

        var dx = sphere.Center.X - cylinder.BaseCenter.X;
        var dz = sphere.Center.Z - cylinder.BaseCenter.Z;
        var distXZ = MathF.Sqrt(dx * dx + dz * dz);

        Vector3 closest;
        if (distXZ < float.Epsilon)
        {
            closest = new Vector3(cylinder.BaseCenter.X, cylinder.BaseCenter.Y + clampedY, cylinder.BaseCenter.Z);
        }
        else
        {
            var clampedDist = MathF.Min(distXZ, cylinder.Radius);
            var scale = clampedDist / distXZ;
            closest = new Vector3(
                cylinder.BaseCenter.X + dx * scale,
                cylinder.BaseCenter.Y + clampedY,
                cylinder.BaseCenter.Z + dz * scale);
        }

        var toSphere = sphere.Center - closest;
        var distSq = toSphere.LengthSquared;

        if (distSq > sphere.Radius * sphere.Radius)
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        var dist = MathF.Sqrt(distSq);
        normal = dist > 1e-6f ? toSphere * (1f / dist) : Vector3.UnitY;
        distance = sphere.Radius - dist;
        point = closest;
        return true;
    }

    #endregion

    #region Capsule vs Shape

    /// <summary>
    /// Capsule vs Capsule。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CapsuleCapsule(
        in CapsuleData a, in CapsuleData b,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        MathUtils.ClosestPointsBetweenSegments(a.Point1, a.Point2, b.Point1, b.Point2,
            out Vector3 closestA, out Vector3 closestB);

        var sphereA = new SphereData(closestA, a.Radius);
        var sphereB = new SphereData(closestB, b.Radius);

        return SphereSphere(sphereA, sphereB, out point, out normal, out distance);
    }

    #endregion

    #region Capsule Sweep vs Shape

    /// <summary>
    /// Capsule Sweep vs Sphere（TOI 計算）。
    /// </summary>
    public static bool CapsuleSweepSphere(
        in Vector3 sweepStart, in Vector3 sweepEnd, float sweepRadius,
        in SphereData target,
        out float toi, out Vector3 point, out Vector3 normal)
    {
        // 球を拡張してレイキャスト
        var expandedSphere = new SphereData(target.Center, target.Radius + sweepRadius);
        var direction = sweepEnd - sweepStart;
        var length = direction.Length;

        if (length < float.Epsilon)
        {
            // 静止状態
            var checkSphere = new SphereData(sweepStart, sweepRadius);
            if (SphereSphere(checkSphere, target, out point, out normal, out var dist))
            {
                toi = 0;
                return true;
            }
            toi = 0;
            return false;
        }

        var dir = direction * (1f / length);
        if (RaySphere(sweepStart, dir, length, expandedSphere, out var t, out var hitPoint, out normal))
        {
            toi = t / length;
            point = hitPoint + normal * sweepRadius;
            return true;
        }

        toi = 0;
        point = default;
        return false;
    }

    /// <summary>
    /// Capsule Sweep vs Capsule（TOI 計算）。
    /// </summary>
    public static bool CapsuleSweepCapsule(
        in Vector3 sweepStart, in Vector3 sweepEnd, float sweepRadius,
        in CapsuleData target,
        out float toi, out Vector3 point, out Vector3 normal)
    {
        // 簡略化：カプセルを拡張してレイキャスト
        var expandedCapsule = new CapsuleData(target.Point1, target.Point2, target.Radius + sweepRadius);
        var direction = sweepEnd - sweepStart;
        var length = direction.Length;

        if (length < float.Epsilon)
        {
            var checkSphere = new SphereData(sweepStart, sweepRadius);
            if (SphereCapsule(checkSphere, target, out point, out normal, out _))
            {
                toi = 0;
                return true;
            }
            toi = 0;
            point = default;
            return false;
        }

        var dir = direction * (1f / length);
        if (RayCapsule(sweepStart, dir, length, expandedCapsule, out var t, out var hitPoint, out normal))
        {
            toi = t / length;
            point = hitPoint + normal * sweepRadius;
            return true;
        }

        toi = 0;
        point = default;
        return false;
    }

    /// <summary>
    /// Capsule Sweep vs Cylinder（TOI 計算）。
    /// </summary>
    public static bool CapsuleSweepCylinder(
        in Vector3 sweepStart, in Vector3 sweepEnd, float sweepRadius,
        in CylinderData target,
        out float toi, out Vector3 point, out Vector3 normal)
    {
        // 円柱を拡張してレイキャスト
        var expandedCylinder = new CylinderData(
            target.BaseCenter - new Vector3(sweepRadius, sweepRadius, sweepRadius),
            target.Height + sweepRadius * 2,
            target.Radius + sweepRadius);

        var direction = sweepEnd - sweepStart;
        var length = direction.Length;

        if (length < float.Epsilon)
        {
            var checkSphere = new SphereData(sweepStart, sweepRadius);
            if (SphereCylinder(checkSphere, target, out point, out normal, out _))
            {
                toi = 0;
                return true;
            }
            toi = 0;
            point = default;
            return false;
        }

        var dir = direction * (1f / length);
        if (RayCylinder(sweepStart, dir, length, expandedCylinder, out var t, out var hitPoint, out normal))
        {
            toi = t / length;
            point = hitPoint + normal * sweepRadius;
            return true;
        }

        toi = 0;
        point = default;
        return false;
    }

    /// <summary>
    /// Capsule Sweep vs Box（TOI 計算）。
    /// </summary>
    public static bool CapsuleSweepBox(
        in Vector3 sweepStart, in Vector3 sweepEnd, float sweepRadius,
        in BoxData target,
        out float toi, out Vector3 point, out Vector3 normal)
    {
        // ボックスを拡張してレイキャスト
        var expandedBox = new BoxData(
            target.Center,
            target.HalfExtents + new Vector3(sweepRadius, sweepRadius, sweepRadius),
            target.Yaw);

        var direction = sweepEnd - sweepStart;
        var length = direction.Length;

        if (length < float.Epsilon)
        {
            var checkSphere = new SphereData(sweepStart, sweepRadius);
            if (SphereBox(checkSphere, target, out point, out normal, out _))
            {
                toi = 0;
                return true;
            }
            toi = 0;
            point = default;
            return false;
        }

        var dir = direction * (1f / length);
        if (RayBox(sweepStart, dir, length, expandedBox, out var t, out var hitPoint, out normal))
        {
            toi = t / length;
            point = hitPoint + normal * sweepRadius;
            return true;
        }

        toi = 0;
        point = default;
        return false;
    }

    #endregion

    #region Slash vs Shape

    /// <summary>
    /// Slash（四辺形面）vs Sphere。
    /// </summary>
    public static bool SlashSphere(
        in Vector3 a0, in Vector3 a1, in Vector3 b0, in Vector3 b1,
        in SphereData sphere,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        // 四辺形の法線を計算
        var diagonal1 = b1 - a0;
        var diagonal2 = b0 - a1;
        var planeNormal = Vector3.Cross(diagonal1, diagonal2).Normalized;
        var planeD = -Vector3.Dot(planeNormal, a0);

        // 球中心から平面への距離
        var signedDist = Vector3.Dot(planeNormal, sphere.Center) + planeD;
        var absDist = MathF.Abs(signedDist);

        if (absDist > sphere.Radius)
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        // 投影点
        var projected = sphere.Center - planeNormal * signedDist;

        // 四辺形内にあるか判定
        if (PointInQuad(projected, a0, a1, b1, b0, planeNormal))
        {
            point = projected;
            normal = signedDist >= 0 ? planeNormal : -planeNormal;
            distance = sphere.Radius - absDist;
            return true;
        }

        // エッジへの最近接点
        var bestDistSq = float.MaxValue;
        Vector3 bestPoint = default;

        CheckEdgeDistance(sphere.Center, a0, a1, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(sphere.Center, a1, b1, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(sphere.Center, b1, b0, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(sphere.Center, b0, a0, ref bestDistSq, ref bestPoint);

        if (bestDistSq <= sphere.Radius * sphere.Radius)
        {
            var dist = MathF.Sqrt(bestDistSq);
            point = bestPoint;
            var toCenter = sphere.Center - bestPoint;
            normal = dist > 1e-6f ? toCenter * (1f / dist) : planeNormal;
            distance = sphere.Radius - dist;
            return true;
        }

        point = normal = default;
        distance = 0;
        return false;
    }

    /// <summary>
    /// Slash vs Capsule。
    /// </summary>
    public static bool SlashCapsule(
        in Vector3 a0, in Vector3 a1, in Vector3 b0, in Vector3 b1,
        in CapsuleData capsule,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        // カプセル軸上の複数点でサンプリング
        const int samples = 5;
        var bestDist = float.MinValue;
        point = normal = default;
        distance = 0;

        for (int i = 0; i < samples; i++)
        {
            var t = i / (float)(samples - 1);
            var samplePoint = Vector3.Lerp(capsule.Point1, capsule.Point2, t);
            var sampleSphere = new SphereData(samplePoint, capsule.Radius);

            if (SlashSphere(a0, a1, b0, b1, sampleSphere, out var p, out var n, out var d))
            {
                if (d > bestDist)
                {
                    bestDist = d;
                    point = p;
                    normal = n;
                    distance = d;
                }
            }
        }

        return distance > 0;
    }

    /// <summary>
    /// Slash vs Cylinder（Capsule近似）。
    /// </summary>
    public static bool SlashCylinder(
        in Vector3 a0, in Vector3 a1, in Vector3 b0, in Vector3 b1,
        in CylinderData cylinder,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        // Cylinder を Capsule として近似
        var capsule = new CapsuleData(
            cylinder.BaseCenter,
            cylinder.BaseCenter + new Vector3(0, cylinder.Height, 0),
            cylinder.Radius);

        return SlashCapsule(a0, a1, b0, b1, capsule, out point, out normal, out distance);
    }

    /// <summary>
    /// Slash vs Box（Sphere近似による判定）。
    /// </summary>
    public static bool SlashBox(
        in Vector3 a0, in Vector3 a1, in Vector3 b0, in Vector3 b1,
        in BoxData box,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        // ボックスを包含球で近似
        var boundingRadius = box.HalfExtents.Length;
        var boundingSphere = new SphereData(box.Center, boundingRadius);

        // まず包含球でテスト（高速棄却）
        if (!SlashSphere(a0, a1, b0, b1, boundingSphere, out _, out _, out _))
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        // より正確な判定：ボックスの8頂点から最も近い点を見つける
        // 斬撃面との交差判定
        var diagonal1 = b1 - a0;
        var diagonal2 = b0 - a1;
        var planeNormal = Vector3.Cross(diagonal1, diagonal2);
        var planeNormalLen = planeNormal.Length;
        if (planeNormalLen < 1e-6f)
        {
            point = normal = default;
            distance = 0;
            return false;
        }
        planeNormal = planeNormal * (1f / planeNormalLen);
        var planeD = -Vector3.Dot(planeNormal, a0);

        // ボックス中心から平面への符号付き距離
        var signedDist = Vector3.Dot(planeNormal, box.Center) + planeD;

        // ボックスの「有効半径」を平面法線方向で計算
        var cos = MathF.Cos(box.Yaw);
        var sin = MathF.Sin(box.Yaw);

        // ローカル軸をワールド座標系に変換
        var axisX = new Vector3(cos, 0, -sin);
        var axisY = new Vector3(0, 1, 0);
        var axisZ = new Vector3(sin, 0, cos);

        var effectiveRadius =
            box.HalfExtents.X * MathF.Abs(Vector3.Dot(planeNormal, axisX)) +
            box.HalfExtents.Y * MathF.Abs(Vector3.Dot(planeNormal, axisY)) +
            box.HalfExtents.Z * MathF.Abs(Vector3.Dot(planeNormal, axisZ));

        if (MathF.Abs(signedDist) > effectiveRadius)
        {
            point = normal = default;
            distance = 0;
            return false;
        }

        // 投影点を計算
        var projected = box.Center - planeNormal * signedDist;

        // 四辺形内にあるかチェック
        if (PointInQuad(projected, a0, a1, b1, b0, planeNormal))
        {
            point = projected;
            normal = signedDist >= 0 ? planeNormal : -planeNormal;
            distance = effectiveRadius - MathF.Abs(signedDist);
            return true;
        }

        // エッジへの最近接点で判定（包含球使用）
        var bestDistSq = float.MaxValue;
        Vector3 bestPoint = default;

        CheckEdgeDistance(box.Center, a0, a1, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(box.Center, a1, b1, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(box.Center, b1, b0, ref bestDistSq, ref bestPoint);
        CheckEdgeDistance(box.Center, b0, a0, ref bestDistSq, ref bestPoint);

        if (bestDistSq <= boundingRadius * boundingRadius)
        {
            var dist = MathF.Sqrt(bestDistSq);
            point = bestPoint;
            var toCenter = box.Center - bestPoint;
            normal = dist > 1e-6f ? toCenter * (1f / dist) : planeNormal;
            distance = boundingRadius - dist;
            return true;
        }

        point = normal = default;
        distance = 0;
        return false;
    }

    #endregion

    #region Helper Methods

    #region Box Transform Helpers

    /// <summary>
    /// ワールド座標をボックスのローカル座標系に変換する（Y軸回転）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 TransformToBoxLocal(in Vector3 point, in BoxData box)
    {
        var d = point - box.Center;
        if (MathF.Abs(box.Yaw) < 1e-6f)
            return d;

        var cos = MathF.Cos(-box.Yaw);
        var sin = MathF.Sin(-box.Yaw);
        return new Vector3(
            d.X * cos - d.Z * sin,
            d.Y,
            d.X * sin + d.Z * cos);
    }

    /// <summary>
    /// ワールド方向ベクトルをボックスのローカル座標系に変換する（Y軸回転）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 TransformDirToBoxLocal(in Vector3 dir, in BoxData box)
    {
        if (MathF.Abs(box.Yaw) < 1e-6f)
            return dir;

        var cos = MathF.Cos(-box.Yaw);
        var sin = MathF.Sin(-box.Yaw);
        return new Vector3(
            dir.X * cos - dir.Z * sin,
            dir.Y,
            dir.X * sin + dir.Z * cos);
    }

    /// <summary>
    /// ボックスのローカル座標をワールド座標系に変換する（Y軸回転）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 TransformFromBoxLocal(in Vector3 local, in BoxData box)
    {
        if (MathF.Abs(box.Yaw) < 1e-6f)
            return local + box.Center;

        var cos = MathF.Cos(box.Yaw);
        var sin = MathF.Sin(box.Yaw);
        return new Vector3(
            local.X * cos - local.Z * sin,
            local.Y,
            local.X * sin + local.Z * cos) + box.Center;
    }

    /// <summary>
    /// ボックスのローカル方向ベクトルをワールド座標系に変換する（Y軸回転）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 TransformDirFromBoxLocal(in Vector3 localDir, in BoxData box)
    {
        if (MathF.Abs(box.Yaw) < 1e-6f)
            return localDir;

        var cos = MathF.Cos(box.Yaw);
        var sin = MathF.Sin(box.Yaw);
        return new Vector3(
            localDir.X * cos - localDir.Z * sin,
            localDir.Y,
            localDir.X * sin + localDir.Z * cos);
    }

    /// <summary>
    /// ボックスローカル座標系での点に対する法線を計算する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ComputeBoxLocalNormal(in Vector3 localPoint, in Vector3 halfExtents)
    {
        // 各面への距離を計算し、最も近い面の法線を返す
        var dx = halfExtents.X - MathF.Abs(localPoint.X);
        var dy = halfExtents.Y - MathF.Abs(localPoint.Y);
        var dz = halfExtents.Z - MathF.Abs(localPoint.Z);

        if (dx <= dy && dx <= dz)
            return new Vector3(localPoint.X >= 0 ? 1 : -1, 0, 0);
        if (dy <= dz)
            return new Vector3(0, localPoint.Y >= 0 ? 1 : -1, 0);
        return new Vector3(0, 0, localPoint.Z >= 0 ? 1 : -1);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PointInQuad(in Vector3 point, in Vector3 v0, in Vector3 v1,
        in Vector3 v2, in Vector3 v3, in Vector3 normal)
    {
        var cross0 = Vector3.Cross(v1 - v0, point - v0);
        var cross1 = Vector3.Cross(v2 - v1, point - v1);
        var cross2 = Vector3.Cross(v3 - v2, point - v2);
        var cross3 = Vector3.Cross(v0 - v3, point - v3);

        var d0 = Vector3.Dot(cross0, normal);
        var d1 = Vector3.Dot(cross1, normal);
        var d2 = Vector3.Dot(cross2, normal);
        var d3 = Vector3.Dot(cross3, normal);

        return (d0 >= 0 && d1 >= 0 && d2 >= 0 && d3 >= 0) ||
               (d0 <= 0 && d1 <= 0 && d2 <= 0 && d3 <= 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckEdgeDistance(in Vector3 point, in Vector3 a, in Vector3 b,
        ref float bestDistSq, ref Vector3 bestPoint)
    {
        var closest = MathUtils.ClosestPointOnSegment(point, a, b);
        var distSq = Vector3.DistanceSquared(point, closest);
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestPoint = closest;
        }
    }

    #endregion
}
