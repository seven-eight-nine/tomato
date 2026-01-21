using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// EntityManagerで管理されるArenaの内部インターフェース。
/// </summary>
internal interface IManagedArena
{
    /// <summary>Arena の型</summary>
    Type ArenaType { get; }

    /// <summary>スナップショットをキャプチャしてボックス化</summary>
    object CaptureSnapshot();

    /// <summary>ボックス化されたスナップショットを復元</summary>
    void RestoreSnapshot(object snapshot);
}

/// <summary>
/// ISnapshotableArena の型安全なラッパー。
/// </summary>
internal sealed class ManagedArenaWrapper<TArena, TSnapshot> : IManagedArena
    where TArena : ISnapshotableArena<TSnapshot>
    where TSnapshot : struct
{
    private readonly TArena _arena;

    public Type ArenaType => typeof(TArena);

    public ManagedArenaWrapper(TArena arena)
    {
        _arena = arena;
    }

    public object CaptureSnapshot()
    {
        return _arena.CaptureSnapshot();
    }

    public void RestoreSnapshot(object snapshot)
    {
        if (snapshot is TSnapshot typedSnapshot)
        {
            _arena.RestoreSnapshot(typedSnapshot);
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid snapshot type. Expected {typeof(TSnapshot).Name}, got {snapshot?.GetType().Name ?? "null"}");
        }
    }

    /// <summary>型安全にスナップショットをキャプチャ</summary>
    public TSnapshot CaptureTyped() => _arena.CaptureSnapshot();

    /// <summary>型安全にスナップショットを復元</summary>
    public void RestoreTyped(in TSnapshot snapshot) => _arena.RestoreSnapshot(snapshot);
}
