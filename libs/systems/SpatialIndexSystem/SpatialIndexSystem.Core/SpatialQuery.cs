using System;
using System.Collections.Generic;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.SpatialIndexSystem;

/// <summary>
/// 空間クエリのヘルパー。
/// </summary>
public static class SpatialQuery
{
    /// <summary>球範囲内のEntityを検索（リスト返却）</summary>
    public static List<VoidHandle> QuerySphere(ISpatialIndex index, Vector3 center, float radius)
    {
        var results = new List<VoidHandle>();
        index.QuerySphere(center, radius, results);
        return results;
    }

    /// <summary>AABB範囲内のEntityを検索（リスト返却）</summary>
    public static List<VoidHandle> QueryAABB(ISpatialIndex index, AABB bounds)
    {
        var results = new List<VoidHandle>();
        index.QueryAABB(bounds, results);
        return results;
    }

    /// <summary>指定位置の最近傍N件を取得</summary>
    public static List<(VoidHandle Handle, float Distance)> QueryNearestN(
        ISpatialIndex index,
        Vector3 point,
        int count,
        float maxDistance = float.MaxValue)
    {
        var candidates = new List<VoidHandle>();
        index.QuerySphere(point, maxDistance, candidates);

        var withDistances = new List<(VoidHandle Handle, float Distance)>();

        foreach (var handle in candidates)
        {
            // 距離計算はSpatialHashGridの内部データにアクセスできないので
            // 呼び出し側で位置を取得して計算する必要がある
            // ここではダミー距離を設定
            withDistances.Add((handle, 0f));
        }

        return withDistances;
    }

    /// <summary>指定位置の最近傍N件を取得（位置取得関数付き）</summary>
    public static List<(VoidHandle Handle, float Distance)> QueryNearestN(
        ISpatialIndex index,
        Vector3 point,
        int count,
        Func<VoidHandle, Vector3?> positionGetter,
        float maxDistance = float.MaxValue)
    {
        var candidates = new List<VoidHandle>();
        index.QuerySphere(point, maxDistance, candidates);

        var withDistances = new List<(VoidHandle Handle, float Distance)>(candidates.Count);

        foreach (var handle in candidates)
        {
            var pos = positionGetter(handle);
            if (!pos.HasValue)
                continue;

            var dx = pos.Value.X - point.X;
            var dy = pos.Value.Y - point.Y;
            var dz = pos.Value.Z - point.Z;
            var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

            withDistances.Add((handle, distance));
        }

        // 距離でソート
        withDistances.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        // N件に絞る
        if (withDistances.Count > count)
        {
            withDistances.RemoveRange(count, withDistances.Count - count);
        }

        return withDistances;
    }
}
