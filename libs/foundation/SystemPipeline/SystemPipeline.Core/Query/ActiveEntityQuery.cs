using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// アクティブなエンティティのみをフィルタリングするクエリ。
/// IsValid == true のエンティティのみを返します。
/// </summary>
public sealed class ActiveEntityQuery : IEntityQuery
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly ActiveEntityQuery Instance = new ActiveEntityQuery();

    private ActiveEntityQuery() { }

    /// <inheritdoc/>
    public IEnumerable<AnyHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<AnyHandle> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.IsValid)
            {
                yield return entity;
            }
        }
    }
}
