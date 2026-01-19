using System.Collections.Generic;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.SpatialIndexSystem;

/// <summary>
/// 空間インデックスのインターフェース。
/// </summary>
public interface ISpatialIndex
{
    /// <summary>エントリ数</summary>
    int Count { get; }

    /// <summary>Entityの位置を更新（存在しなければ追加）</summary>
    void Update(VoidHandle handle, Vector3 position, float radius = 0f);

    /// <summary>Entityを削除</summary>
    bool Remove(VoidHandle handle);

    /// <summary>球範囲内のEntityを検索</summary>
    void QuerySphere(Vector3 center, float radius, List<VoidHandle> results);

    /// <summary>AABB範囲内のEntityを検索</summary>
    void QueryAABB(AABB bounds, List<VoidHandle> results);

    /// <summary>最も近いEntityを検索</summary>
    bool QueryNearest(Vector3 point, float maxDistance, out VoidHandle nearest, out float distance);

    /// <summary>全エントリをクリア</summary>
    void Clear();
}
