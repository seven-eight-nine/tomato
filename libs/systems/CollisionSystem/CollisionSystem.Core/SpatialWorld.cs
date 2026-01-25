using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 空間クエリワールド。Broad Phase + Narrow Phase を統合。
/// </summary>
public sealed class SpatialWorld
{
    private readonly ShapeRegistry _registry;
    private readonly IBroadPhase _broadPhase;

    /// <summary>
    /// SpatialWorld を作成する。
    /// </summary>
    /// <param name="broadPhase">Broad Phase 戦略</param>
    public SpatialWorld(IBroadPhase broadPhase)
    {
        _broadPhase = broadPhase ?? throw new ArgumentNullException(nameof(broadPhase));
        _registry = new ShapeRegistry();
    }

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _registry.Count;

    #region Registration

    /// <summary>
    /// 球を追加する。
    /// </summary>
    public ShapeHandle AddSphere(in Vector3 center, float radius, bool isStatic = false, int userData = 0, uint layerMask = 0xFFFFFFFF)
    {
        var data = new SphereData(center, radius);
        var handle = _registry.AddSphere(data, isStatic, userData, layerMask);
        _broadPhase.Add(handle.Index, _registry.GetAABB(handle.Index));
        return handle;
    }

    /// <summary>
    /// カプセルを追加する。
    /// </summary>
    public ShapeHandle AddCapsule(in Vector3 p1, in Vector3 p2, float radius, bool isStatic = false, int userData = 0, uint layerMask = 0xFFFFFFFF)
    {
        var data = new CapsuleData(p1, p2, radius);
        var handle = _registry.AddCapsule(data, isStatic, userData, layerMask);
        _broadPhase.Add(handle.Index, _registry.GetAABB(handle.Index));
        return handle;
    }

    /// <summary>
    /// 円柱を追加する。
    /// </summary>
    public ShapeHandle AddCylinder(in Vector3 baseCenter, float height, float radius, bool isStatic = false, int userData = 0, uint layerMask = 0xFFFFFFFF)
    {
        var data = new CylinderData(baseCenter, height, radius);
        var handle = _registry.AddCylinder(data, isStatic, userData, layerMask);
        _broadPhase.Add(handle.Index, _registry.GetAABB(handle.Index));
        return handle;
    }

    /// <summary>
    /// ボックスを追加する。
    /// </summary>
    /// <param name="center">中心座標</param>
    /// <param name="halfExtents">半サイズ</param>
    /// <param name="yaw">Y軸回転（ラジアン）</param>
    /// <param name="isStatic">静的フラグ</param>
    /// <param name="userData">ユーザーデータ</param>
    /// <param name="layerMask">レイヤーマスク</param>
    public ShapeHandle AddBox(in Vector3 center, in Vector3 halfExtents, float yaw = 0f, bool isStatic = false, int userData = 0, uint layerMask = 0xFFFFFFFF)
    {
        var data = new BoxData(center, halfExtents, yaw);
        var handle = _registry.AddBox(data, isStatic, userData, layerMask);
        _broadPhase.Add(handle.Index, _registry.GetAABB(handle.Index));
        return handle;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(ShapeHandle handle)
    {
        if (!_registry.IsValid(handle))
            return false;

        _broadPhase.Remove(handle.Index);
        return _registry.Remove(handle);
    }

    #endregion

    #region Update

    /// <summary>
    /// 球の位置を更新する。
    /// </summary>
    public void UpdateSphere(ShapeHandle handle, in Vector3 newCenter, float newRadius)
    {
        if (!_registry.IsValid(handle))
            return;

        var oldAABB = _registry.GetAABB(handle.Index);
        var newData = new SphereData(newCenter, newRadius);

        if (_registry.UpdateSphere(handle, newData))
        {
            var newAABB = _registry.GetAABB(handle.Index);
            _broadPhase.Update(handle.Index, oldAABB, newAABB);
        }
    }

    /// <summary>
    /// カプセルの位置を更新する。
    /// </summary>
    public void UpdateCapsule(ShapeHandle handle, in Vector3 newP1, in Vector3 newP2, float newRadius)
    {
        if (!_registry.IsValid(handle))
            return;

        var oldAABB = _registry.GetAABB(handle.Index);
        var newData = new CapsuleData(newP1, newP2, newRadius);

        if (_registry.UpdateCapsule(handle, newData))
        {
            var newAABB = _registry.GetAABB(handle.Index);
            _broadPhase.Update(handle.Index, oldAABB, newAABB);
        }
    }

    /// <summary>
    /// 円柱の位置を更新する。
    /// </summary>
    public void UpdateCylinder(ShapeHandle handle, in Vector3 newBaseCenter, float newHeight, float newRadius)
    {
        if (!_registry.IsValid(handle))
            return;

        var oldAABB = _registry.GetAABB(handle.Index);
        var newData = new CylinderData(newBaseCenter, newHeight, newRadius);

        if (_registry.UpdateCylinder(handle, newData))
        {
            var newAABB = _registry.GetAABB(handle.Index);
            _broadPhase.Update(handle.Index, oldAABB, newAABB);
        }
    }

    /// <summary>
    /// ボックスの位置を更新する。
    /// </summary>
    /// <param name="handle">ハンドル</param>
    /// <param name="newCenter">新しい中心座標</param>
    /// <param name="newHalfExtents">新しい半サイズ</param>
    /// <param name="newYaw">新しいY軸回転（ラジアン）</param>
    public void UpdateBox(ShapeHandle handle, in Vector3 newCenter, in Vector3 newHalfExtents, float newYaw = 0f)
    {
        if (!_registry.IsValid(handle))
            return;

        var oldAABB = _registry.GetAABB(handle.Index);
        var newData = new BoxData(newCenter, newHalfExtents, newYaw);

        if (_registry.UpdateBox(handle, newData))
        {
            var newAABB = _registry.GetAABB(handle.Index);
            _broadPhase.Update(handle.Index, oldAABB, newAABB);
        }
    }

    #endregion

    #region Queries

    /// <summary>
    /// 点クエリ。
    /// </summary>
    public int QueryPoint(in Vector3 point, Span<HitResult> results,
                          uint includeMask = 0xFFFFFFFF, uint excludeMask = 0)
    {
        if (results.IsEmpty)
            return 0;

        // 微小AABBを作成
        var epsilon = new Vector3(0.001f, 0.001f, 0.001f);
        var queryAABB = new AABB(point - epsilon, point + epsilon);

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        int hitCount = 0;
        for (int i = 0; i < candidateCount && hitCount < results.Length; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            uint shapeMask = _registry.GetLayerMask(shapeIndex);
            if ((shapeMask & includeMask) == 0 || (shapeMask & excludeMask) != 0)
                continue;

            if (TestPointShape(point, shapeIndex, out var hit))
            {
                results[hitCount++] = hit;
            }
        }

        return hitCount;
    }

    /// <summary>
    /// レイキャスト（最近ヒットのみ）。
    /// </summary>
    public bool Raycast(in RayQuery query, out HitResult hit)
    {
        hit = HitResult.None;
        var queryAABB = query.GetAABB();

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        float bestT = query.MaxDistance;

        for (int i = 0; i < candidateCount; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            if (!query.PassesMask(_registry.GetLayerMask(shapeIndex)))
                continue;

            if (TestRayShape(query.Origin, query.Direction, bestT, shapeIndex,
                out var t, out var point, out var normal))
            {
                if (t < bestT)
                {
                    bestT = t;
                    hit = new HitResult(shapeIndex, t, point, normal);
                }
            }
        }

        return hit.IsValid;
    }

    /// <summary>
    /// レイキャスト（全ヒット、距離順）。
    /// </summary>
    public int RaycastAll(in RayQuery query, Span<HitResult> results)
    {
        if (results.IsEmpty)
            return 0;

        var queryAABB = query.GetAABB();

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        int hitCount = 0;

        for (int i = 0; i < candidateCount && hitCount < results.Length; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            if (!query.PassesMask(_registry.GetLayerMask(shapeIndex)))
                continue;

            if (TestRayShape(query.Origin, query.Direction, query.MaxDistance, shapeIndex,
                out var t, out var point, out var normal))
            {
                results[hitCount++] = new HitResult(shapeIndex, t, point, normal);
            }
        }

        // 距離でソート
        SortByDistance(results[..hitCount]);

        return hitCount;
    }

    /// <summary>
    /// 球オーバーラップクエリ。
    /// </summary>
    public int QuerySphereOverlap(in SphereOverlapQuery query, Span<HitResult> results)
    {
        if (results.IsEmpty)
            return 0;

        var queryAABB = query.GetAABB();

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        int hitCount = 0;
        var querySphere = new SphereData(query.Center, query.Radius);

        for (int i = 0; i < candidateCount && hitCount < results.Length; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            if (!query.PassesMask(_registry.GetLayerMask(shapeIndex)))
                continue;

            if (TestSphereShape(querySphere, shapeIndex, out var point, out var normal, out var distance))
            {
                results[hitCount++] = new HitResult(shapeIndex, distance, point, normal);
            }
        }

        return hitCount;
    }

    /// <summary>
    /// カプセルスイープクエリ（移動判定）。
    /// </summary>
    public bool CapsuleSweep(in CapsuleSweepQuery query, out HitResult hit)
    {
        hit = HitResult.None;
        var queryAABB = query.GetAABB();

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        float bestTOI = 1f;

        for (int i = 0; i < candidateCount; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            if (!query.PassesMask(_registry.GetLayerMask(shapeIndex)))
                continue;

            if (TestCapsuleSweepShape(query.Start, query.End, query.Radius, shapeIndex,
                out var toi, out var point, out var normal))
            {
                if (toi < bestTOI)
                {
                    bestTOI = toi;
                    hit = new HitResult(shapeIndex, toi, point, normal);
                }
            }
        }

        return hit.IsValid;
    }

    /// <summary>
    /// 斬撃線クエリ。
    /// </summary>
    public int QuerySlash(in SlashQuery query, Span<HitResult> results)
    {
        if (results.IsEmpty)
            return 0;

        var queryAABB = query.GetAABB();

        Span<int> candidates = stackalloc int[256];
        int candidateCount = _broadPhase.Query(queryAABB, candidates, _registry.AABBs);

        int hitCount = 0;

        for (int i = 0; i < candidateCount && hitCount < results.Length; i++)
        {
            int shapeIndex = candidates[i];

            // マスクフィルタリング
            if (!query.PassesMask(_registry.GetLayerMask(shapeIndex)))
                continue;

            if (TestSlashShape(query.StartBase, query.StartTip, query.EndBase, query.EndTip, shapeIndex,
                out var point, out var normal, out var distance))
            {
                results[hitCount++] = new HitResult(shapeIndex, distance, point, normal);
            }
        }

        return hitCount;
    }

    #endregion

    #region Data Access

    /// <summary>
    /// ハンドルが有効か確認する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(ShapeHandle handle) => _registry.IsValid(handle);

    /// <summary>
    /// インデックスから現在のハンドルを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeHandle GetHandle(int index) => _registry.GetHandle(index);

    /// <summary>
    /// ユーザーデータを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUserData(int shapeIndex) => _registry.GetUserData(shapeIndex);

    /// <summary>
    /// ユーザーデータを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUserData(ShapeHandle handle)
    {
        if (!_registry.IsValid(handle))
            return -1;
        return _registry.GetUserData(handle.Index);
    }

    /// <summary>
    /// レイヤーマスクを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetLayerMask(int shapeIndex) => _registry.GetLayerMask(shapeIndex);

    /// <summary>
    /// レイヤーマスクを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetLayerMask(ShapeHandle handle)
    {
        if (!_registry.IsValid(handle))
            return 0;
        return _registry.GetLayerMask(handle.Index);
    }

    /// <summary>
    /// レイヤーマスクを設定する。
    /// </summary>
    public void SetLayerMask(ShapeHandle handle, uint layerMask)
    {
        if (!_registry.IsValid(handle))
            return;
        _registry.SetLayerMask(handle.Index, layerMask);
    }

    #endregion

    #region Narrow Phase Tests

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TestPointShape(in Vector3 point, int shapeIndex, out HitResult hit)
    {
        var type = _registry.GetShapeType(shapeIndex);

        switch (type)
        {
            case ShapeType.Sphere:
            {
                ref readonly var sphere = ref _registry.GetSphere(shapeIndex);
                if (ShapeIntersection.PointSphere(point, sphere))
                {
                    var dir = (point - sphere.Center);
                    var dist = dir.Length;
                    var normal = dist > 1e-6f ? dir * (1f / dist) : Vector3.UnitY;
                    hit = new HitResult(shapeIndex, dist, point, normal);
                    return true;
                }
                break;
            }
            case ShapeType.Capsule:
            {
                ref readonly var capsule = ref _registry.GetCapsule(shapeIndex);
                if (ShapeIntersection.PointCapsule(point, capsule))
                {
                    var closest = MathUtils.ClosestPointOnSegment(point, capsule.Point1, capsule.Point2);
                    var dir = point - closest;
                    var dist = dir.Length;
                    var normal = dist > 1e-6f ? dir * (1f / dist) : Vector3.UnitY;
                    hit = new HitResult(shapeIndex, dist, point, normal);
                    return true;
                }
                break;
            }
            case ShapeType.Cylinder:
            {
                ref readonly var cylinder = ref _registry.GetCylinder(shapeIndex);
                if (ShapeIntersection.PointCylinder(point, cylinder))
                {
                    var dx = point.X - cylinder.BaseCenter.X;
                    var dz = point.Z - cylinder.BaseCenter.Z;
                    var distXZ = MathF.Sqrt(dx * dx + dz * dz);
                    var normal = distXZ > 1e-6f
                        ? new Vector3(dx / distXZ, 0, dz / distXZ)
                        : Vector3.UnitX;
                    hit = new HitResult(shapeIndex, distXZ, point, normal);
                    return true;
                }
                break;
            }
            case ShapeType.Box:
            {
                ref readonly var box = ref _registry.GetBox(shapeIndex);
                if (ShapeIntersection.PointBox(point, box))
                {
                    var dir = point - box.Center;
                    var dist = dir.Length;
                    var normal = dist > 1e-6f ? dir * (1f / dist) : Vector3.UnitY;
                    hit = new HitResult(shapeIndex, dist, point, normal);
                    return true;
                }
                break;
            }
        }

        hit = HitResult.None;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TestRayShape(in Vector3 origin, in Vector3 direction, float maxDistance, int shapeIndex,
        out float t, out Vector3 point, out Vector3 normal)
    {
        var type = _registry.GetShapeType(shapeIndex);

        switch (type)
        {
            case ShapeType.Sphere:
            {
                ref readonly var sphere = ref _registry.GetSphere(shapeIndex);
                return ShapeIntersection.RaySphere(origin, direction, maxDistance, sphere, out t, out point, out normal);
            }
            case ShapeType.Capsule:
            {
                ref readonly var capsule = ref _registry.GetCapsule(shapeIndex);
                return ShapeIntersection.RayCapsule(origin, direction, maxDistance, capsule, out t, out point, out normal);
            }
            case ShapeType.Cylinder:
            {
                ref readonly var cylinder = ref _registry.GetCylinder(shapeIndex);
                return ShapeIntersection.RayCylinder(origin, direction, maxDistance, cylinder, out t, out point, out normal);
            }
            case ShapeType.Box:
            {
                ref readonly var box = ref _registry.GetBox(shapeIndex);
                return ShapeIntersection.RayBox(origin, direction, maxDistance, box, out t, out point, out normal);
            }
        }

        t = 0;
        point = normal = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TestSphereShape(in SphereData querySphere, int shapeIndex,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        var type = _registry.GetShapeType(shapeIndex);

        switch (type)
        {
            case ShapeType.Sphere:
            {
                ref readonly var sphere = ref _registry.GetSphere(shapeIndex);
                return ShapeIntersection.SphereSphere(querySphere, sphere, out point, out normal, out distance);
            }
            case ShapeType.Capsule:
            {
                ref readonly var capsule = ref _registry.GetCapsule(shapeIndex);
                return ShapeIntersection.SphereCapsule(querySphere, capsule, out point, out normal, out distance);
            }
            case ShapeType.Cylinder:
            {
                ref readonly var cylinder = ref _registry.GetCylinder(shapeIndex);
                return ShapeIntersection.SphereCylinder(querySphere, cylinder, out point, out normal, out distance);
            }
            case ShapeType.Box:
            {
                ref readonly var box = ref _registry.GetBox(shapeIndex);
                return ShapeIntersection.SphereBox(querySphere, box, out point, out normal, out distance);
            }
        }

        point = normal = default;
        distance = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TestCapsuleSweepShape(in Vector3 start, in Vector3 end, float radius, int shapeIndex,
        out float toi, out Vector3 point, out Vector3 normal)
    {
        var type = _registry.GetShapeType(shapeIndex);

        switch (type)
        {
            case ShapeType.Sphere:
            {
                ref readonly var sphere = ref _registry.GetSphere(shapeIndex);
                return ShapeIntersection.CapsuleSweepSphere(start, end, radius, sphere, out toi, out point, out normal);
            }
            case ShapeType.Capsule:
            {
                ref readonly var capsule = ref _registry.GetCapsule(shapeIndex);
                return ShapeIntersection.CapsuleSweepCapsule(start, end, radius, capsule, out toi, out point, out normal);
            }
            case ShapeType.Cylinder:
            {
                ref readonly var cylinder = ref _registry.GetCylinder(shapeIndex);
                return ShapeIntersection.CapsuleSweepCylinder(start, end, radius, cylinder, out toi, out point, out normal);
            }
            case ShapeType.Box:
            {
                ref readonly var box = ref _registry.GetBox(shapeIndex);
                return ShapeIntersection.CapsuleSweepBox(start, end, radius, box, out toi, out point, out normal);
            }
        }

        toi = 0;
        point = normal = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TestSlashShape(in Vector3 a0, in Vector3 a1, in Vector3 b0, in Vector3 b1, int shapeIndex,
        out Vector3 point, out Vector3 normal, out float distance)
    {
        var type = _registry.GetShapeType(shapeIndex);

        switch (type)
        {
            case ShapeType.Sphere:
            {
                ref readonly var sphere = ref _registry.GetSphere(shapeIndex);
                return ShapeIntersection.SlashSphere(a0, a1, b0, b1, sphere, out point, out normal, out distance);
            }
            case ShapeType.Capsule:
            {
                ref readonly var capsule = ref _registry.GetCapsule(shapeIndex);
                return ShapeIntersection.SlashCapsule(a0, a1, b0, b1, capsule, out point, out normal, out distance);
            }
            case ShapeType.Cylinder:
            {
                ref readonly var cylinder = ref _registry.GetCylinder(shapeIndex);
                return ShapeIntersection.SlashCylinder(a0, a1, b0, b1, cylinder, out point, out normal, out distance);
            }
            case ShapeType.Box:
            {
                ref readonly var box = ref _registry.GetBox(shapeIndex);
                return ShapeIntersection.SlashBox(a0, a1, b0, b1, box, out point, out normal, out distance);
            }
        }

        point = normal = default;
        distance = 0;
        return false;
    }

    private static void SortByDistance(Span<HitResult> results)
    {
        for (int i = 1; i < results.Length; i++)
        {
            var key = results[i];
            int j = i - 1;
            while (j >= 0 && results[j].Distance > key.Distance)
            {
                results[j + 1] = results[j];
                j--;
            }
            results[j + 1] = key;
        }
    }

    #endregion
}
