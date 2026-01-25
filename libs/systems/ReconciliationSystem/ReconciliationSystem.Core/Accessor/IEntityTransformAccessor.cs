using Tomato.Math;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// Entity位置情報へのアクセスを提供するインターフェース。
/// </summary>
public interface IEntityTransformAccessor
{
    /// <summary>
    /// 指定Entityの位置を取得する。
    /// </summary>
    Vector3 GetPosition(AnyHandle handle);

    /// <summary>
    /// 指定Entityの位置を設定する。
    /// </summary>
    void SetPosition(AnyHandle handle, Vector3 position);
}
