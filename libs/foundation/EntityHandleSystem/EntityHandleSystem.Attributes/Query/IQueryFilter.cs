namespace Tomato.EntityHandleSystem;

/// <summary>
/// クエリフィルタのインターフェース。
/// </summary>
public interface IQueryFilter
{
    /// <summary>Entityがフィルタ条件に合致するか判定</summary>
    bool Matches(AnyHandle handle, IQueryableArena arena, int index);
}
