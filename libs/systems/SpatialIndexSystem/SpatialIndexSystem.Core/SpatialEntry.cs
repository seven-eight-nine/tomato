using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.SpatialIndexSystem;

/// <summary>
/// 空間インデックス内のエントリ。
/// </summary>
public struct SpatialEntry
{
    /// <summary>EntityのHandle</summary>
    public AnyHandle Handle;

    /// <summary>位置</summary>
    public Vector3 Position;

    /// <summary>半径（バウンディング球）</summary>
    public float Radius;

    public SpatialEntry(AnyHandle handle, Vector3 position, float radius = 0f)
    {
        Handle = handle;
        Position = position;
        Radius = radius;
    }
}
