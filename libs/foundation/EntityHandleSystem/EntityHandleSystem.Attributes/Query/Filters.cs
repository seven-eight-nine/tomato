using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// 有効なEntityのみを通すフィルタ。
/// </summary>
public sealed class AliveFilter : IQueryFilter
{
    public bool Matches(VoidHandle handle, IQueryableArena arena, int index)
    {
        return handle.IsValid;
    }
}

/// <summary>
/// Arena型によるフィルタ。
/// </summary>
public sealed class TypeFilter : IQueryFilter
{
    private readonly Type _arenaType;

    public TypeFilter(Type arenaType)
    {
        _arenaType = arenaType;
    }

    public bool Matches(VoidHandle handle, IQueryableArena arena, int index)
    {
        return arena.ArenaType == _arenaType || _arenaType.IsAssignableFrom(arena.ArenaType);
    }
}

/// <summary>
/// 任意の条件によるフィルタ。
/// </summary>
public sealed class PredicateFilter : IQueryFilter
{
    private readonly Func<VoidHandle, IQueryableArena, int, bool> _predicate;

    public PredicateFilter(Func<VoidHandle, IQueryableArena, int, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool Matches(VoidHandle handle, IQueryableArena arena, int index)
    {
        return _predicate(handle, arena, index);
    }
}
