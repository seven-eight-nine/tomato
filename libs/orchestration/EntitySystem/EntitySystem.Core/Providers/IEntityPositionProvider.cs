using Tomato.EntityHandleSystem;
using Tomato.CollisionSystem;

namespace Tomato.EntitySystem.Providers;

/// <summary>
/// Entityの位置を取得するインターフェース。
/// </summary>
public interface IEntityPositionProvider
{
    /// <summary>
    /// Entityの現在位置を取得する。
    /// </summary>
    /// <param name="handle">EntityのAnyHandle</param>
    /// <returns>位置</returns>
    Vector3 GetPosition(AnyHandle handle);
}
