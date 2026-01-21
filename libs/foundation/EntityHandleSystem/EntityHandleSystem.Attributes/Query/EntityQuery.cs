using System;
using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// Entity検索のためのクエリビルダー。
/// </summary>
public sealed class EntityQuery
{
    private readonly QueryExecutor _executor;
    private readonly List<IQueryFilter> _filters = new List<IQueryFilter>();

    internal EntityQuery(QueryExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>指定Arena型のEntityのみ</summary>
    public EntityQuery OfType<TArena>() where TArena : class
    {
        _filters.Add(new TypeFilter(typeof(TArena)));
        return this;
    }

    /// <summary>指定Arena型のEntityのみ（Type指定）</summary>
    public EntityQuery OfType(Type arenaType)
    {
        _filters.Add(new TypeFilter(arenaType));
        return this;
    }

    /// <summary>有効なEntityのみ</summary>
    public EntityQuery WhereAlive()
    {
        _filters.Add(new AliveFilter());
        return this;
    }

    /// <summary>任意の条件でフィルタ</summary>
    public EntityQuery Where(Func<AnyHandle, IQueryableArena, int, bool> predicate)
    {
        _filters.Add(new PredicateFilter(predicate));
        return this;
    }

    /// <summary>カスタムフィルタを追加</summary>
    public EntityQuery WithFilter(IQueryFilter filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>クエリを実行</summary>
    public QueryResult Execute()
    {
        return _executor.Execute(_filters);
    }

    /// <summary>クエリを実行し、列挙</summary>
    public IEnumerable<AnyHandle> Enumerate()
    {
        return Execute().Handles;
    }

    /// <summary>最初の1件を取得</summary>
    public AnyHandle? First()
    {
        return Execute().FirstOrNull();
    }

    /// <summary>件数を取得</summary>
    public int Count()
    {
        return Execute().Count;
    }

    /// <summary>条件に合うEntityが存在するか</summary>
    public bool Any()
    {
        return Execute().Count > 0;
    }
}
