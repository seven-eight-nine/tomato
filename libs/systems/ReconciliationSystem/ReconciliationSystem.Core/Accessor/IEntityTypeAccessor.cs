using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// Entity種別へのアクセスを提供するインターフェース。
/// </summary>
public interface IEntityTypeAccessor
{
    /// <summary>
    /// 指定EntityのEntity種別を取得する。
    /// </summary>
    EntityType GetEntityType(VoidHandle handle);
}
