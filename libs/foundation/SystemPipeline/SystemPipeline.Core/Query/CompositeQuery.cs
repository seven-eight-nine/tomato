using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// 複数のクエリを組み合わせる複合クエリ。
/// 全てのクエリ条件を満たすエンティティのみを返します（AND条件）。
/// </summary>
public sealed class CompositeQuery : IEntityQuery
{
    private readonly IEntityQuery[] _queries;

    /// <summary>
    /// CompositeQueryを作成します。
    /// </summary>
    /// <param name="queries">組み合わせるクエリ</param>
    public CompositeQuery(params IEntityQuery[] queries)
    {
        _queries = queries ?? new IEntityQuery[0];
    }

    /// <inheritdoc/>
    public IEnumerable<AnyHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<AnyHandle> entities)
    {
        IEnumerable<AnyHandle> result = entities;

        foreach (var query in _queries)
        {
            result = query.Filter(registry, result);
        }

        return result;
    }
}
