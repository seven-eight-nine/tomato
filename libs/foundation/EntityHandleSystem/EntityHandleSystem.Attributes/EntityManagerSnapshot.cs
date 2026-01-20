using System;
using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// EntityManager全体のスナップショット。
/// 登録された全Arenaの状態を保持する。
/// </summary>
public sealed class EntityManagerSnapshot
{
    private readonly Dictionary<Type, object> _arenaSnapshots;

    /// <summary>スナップショット取得時のフレーム番号</summary>
    public int FrameNumber { get; }

    /// <summary>キャプチャ時刻</summary>
    public DateTime CapturedAt { get; }

    /// <summary>含まれるArena数</summary>
    public int ArenaCount => _arenaSnapshots.Count;

    internal EntityManagerSnapshot(int frameNumber, Dictionary<Type, object> arenaSnapshots)
    {
        FrameNumber = frameNumber;
        _arenaSnapshots = arenaSnapshots;
        CapturedAt = DateTime.UtcNow;
    }

    /// <summary>指定Arenaのスナップショットを取得</summary>
    public TSnapshot GetSnapshot<TArena, TSnapshot>()
        where TSnapshot : struct
    {
        if (_arenaSnapshots.TryGetValue(typeof(TArena), out var snapshot))
        {
            return (TSnapshot)snapshot;
        }

        throw new KeyNotFoundException($"Snapshot for arena {typeof(TArena).Name} not found");
    }

    /// <summary>指定Arenaのスナップショットを取得（存在しない場合はnull）</summary>
    public TSnapshot? TryGetSnapshot<TArena, TSnapshot>()
        where TSnapshot : struct
    {
        if (_arenaSnapshots.TryGetValue(typeof(TArena), out var snapshot))
        {
            return (TSnapshot)snapshot;
        }

        return null;
    }

    /// <summary>指定Arenaのスナップショットが存在するか</summary>
    public bool HasSnapshot<TArena>()
    {
        return _arenaSnapshots.ContainsKey(typeof(TArena));
    }

    internal Dictionary<Type, object> GetAllSnapshots() => _arenaSnapshots;
}
