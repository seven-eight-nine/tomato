using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// クエリを実行するエグゼキュータ。
/// </summary>
public sealed class QueryExecutor
{
    private readonly List<IQueryableArena> _arenas = new List<IQueryableArena>();

    /// <summary>登録されたArena数</summary>
    public int ArenaCount => _arenas.Count;

    /// <summary>Arena を登録</summary>
    public void Register(IQueryableArena arena)
    {
        _arenas.Add(arena);
    }

    /// <summary>Arena の登録を解除</summary>
    public bool Unregister(IQueryableArena arena)
    {
        return _arenas.Remove(arena);
    }

    /// <summary>全Arenaの登録を解除</summary>
    public void Clear()
    {
        _arenas.Clear();
    }

    /// <summary>クエリビルダーを開始</summary>
    public EntityQuery Query()
    {
        return new EntityQuery(this);
    }

    /// <summary>クエリを実行</summary>
    internal QueryResult Execute(IReadOnlyList<IQueryFilter> filters)
    {
        var handles = new List<AnyHandle>();

        foreach (var arena in _arenas)
        {
            foreach (var (handle, index) in arena.EnumerateActive())
            {
                bool matches = true;

                foreach (var filter in filters)
                {
                    if (!filter.Matches(handle, arena, index))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    handles.Add(handle);
                }
            }
        }

        return new QueryResult(handles);
    }
}
