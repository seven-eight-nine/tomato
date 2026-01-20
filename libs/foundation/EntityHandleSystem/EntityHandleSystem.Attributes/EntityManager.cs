using System;
using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// 複数のArenaを管理するマネージャー。
/// スナップショット/復元を統一的に行う。
///
/// <para>使用例:</para>
/// <code>
/// var manager = new EntityManager();
/// manager.Register&lt;EnemyArena, EnemyArenaSnapshot&gt;(enemyArena);
/// manager.Register&lt;BulletArena, BulletArenaSnapshot&gt;(bulletArena);
///
/// // スナップショット取得
/// var snapshot = manager.CaptureSnapshot(frameNumber);
///
/// // ゲーム進行...
///
/// // 状態を復元
/// manager.RestoreSnapshot(snapshot);
/// </code>
///
/// <remarks>
/// 複数のEntityManagerインスタンスを作成できます。
/// 例えば、メインゲームとミニゲームで別々のEntityManagerを使用できます。
/// </remarks>
/// </summary>
public sealed class EntityManager
{
    private readonly List<IManagedArena> _arenas = new List<IManagedArena>();
    private readonly Dictionary<Type, int> _arenaIndexMap = new Dictionary<Type, int>();
    private readonly object _lock = new object();

    /// <summary>登録されたArena数</summary>
    public int ArenaCount
    {
        get { lock (_lock) { return _arenas.Count; } }
    }

    /// <summary>
    /// Arenaを登録。
    /// </summary>
    /// <typeparam name="TArena">Arena型</typeparam>
    /// <typeparam name="TSnapshot">スナップショット型</typeparam>
    /// <param name="arena">登録するArena</param>
    /// <exception cref="ArgumentNullException">arenaがnull</exception>
    /// <exception cref="InvalidOperationException">同じArena型が既に登録済み</exception>
    public void Register<TArena, TSnapshot>(TArena arena)
        where TArena : ISnapshotableArena<TSnapshot>
        where TSnapshot : struct
    {
        if (arena == null)
            throw new ArgumentNullException(nameof(arena));

        lock (_lock)
        {
            var type = typeof(TArena);
            if (_arenaIndexMap.ContainsKey(type))
            {
                throw new InvalidOperationException($"Arena {type.Name} is already registered");
            }

            var wrapper = new ManagedArenaWrapper<TArena, TSnapshot>(arena);
            _arenaIndexMap[type] = _arenas.Count;
            _arenas.Add(wrapper);
        }
    }

    /// <summary>
    /// Arenaの登録を解除。
    /// </summary>
    /// <typeparam name="TArena">Arena型</typeparam>
    /// <returns>解除に成功した場合true</returns>
    public bool Unregister<TArena>()
    {
        lock (_lock)
        {
            var type = typeof(TArena);
            if (!_arenaIndexMap.TryGetValue(type, out var index))
            {
                return false;
            }

            // 最後の要素と入れ替えて削除
            var lastIndex = _arenas.Count - 1;
            if (index != lastIndex)
            {
                var lastWrapper = _arenas[lastIndex];
                _arenas[index] = lastWrapper;
                _arenaIndexMap[lastWrapper.ArenaType] = index;
            }

            _arenas.RemoveAt(lastIndex);
            _arenaIndexMap.Remove(type);

            return true;
        }
    }

    /// <summary>
    /// 指定Arena型が登録されているか確認。
    /// </summary>
    public bool IsRegistered<TArena>()
    {
        lock (_lock)
        {
            return _arenaIndexMap.ContainsKey(typeof(TArena));
        }
    }

    /// <summary>
    /// 全Arenaの登録を解除。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _arenas.Clear();
            _arenaIndexMap.Clear();
        }
    }

    /// <summary>
    /// 全Arenaの状態をスナップショットとしてキャプチャ。
    /// </summary>
    /// <param name="frameNumber">現在のフレーム番号</param>
    /// <returns>全Arenaのスナップショット</returns>
    public EntityManagerSnapshot CaptureSnapshot(int frameNumber)
    {
        lock (_lock)
        {
            var snapshots = new Dictionary<Type, object>(_arenas.Count);

            foreach (var wrapper in _arenas)
            {
                var snapshot = wrapper.CaptureSnapshot();
                snapshots[wrapper.ArenaType] = snapshot;
            }

            return new EntityManagerSnapshot(frameNumber, snapshots);
        }
    }

    /// <summary>
    /// スナップショットから全Arenaの状態を復元。
    /// </summary>
    /// <param name="snapshot">復元するスナップショット</param>
    /// <exception cref="ArgumentNullException">snapshotがnull</exception>
    public void RestoreSnapshot(EntityManagerSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        lock (_lock)
        {
            var snapshotData = snapshot.GetAllSnapshots();

            foreach (var wrapper in _arenas)
            {
                if (snapshotData.TryGetValue(wrapper.ArenaType, out var arenaSnapshot))
                {
                    wrapper.RestoreSnapshot(arenaSnapshot);
                }
            }
        }
    }

    /// <summary>
    /// 指定Arenaのみスナップショットをキャプチャ。
    /// </summary>
    public TSnapshot CaptureArena<TArena, TSnapshot>()
        where TArena : ISnapshotableArena<TSnapshot>
        where TSnapshot : struct
    {
        lock (_lock)
        {
            var type = typeof(TArena);
            if (!_arenaIndexMap.TryGetValue(type, out var index))
            {
                throw new KeyNotFoundException($"Arena {type.Name} is not registered");
            }

            var wrapper = (ManagedArenaWrapper<TArena, TSnapshot>)_arenas[index];
            return wrapper.CaptureTyped();
        }
    }

    /// <summary>
    /// 指定Arenaのみスナップショットから復元。
    /// </summary>
    public void RestoreArena<TArena, TSnapshot>(in TSnapshot snapshot)
        where TArena : ISnapshotableArena<TSnapshot>
        where TSnapshot : struct
    {
        lock (_lock)
        {
            var type = typeof(TArena);
            if (!_arenaIndexMap.TryGetValue(type, out var index))
            {
                throw new KeyNotFoundException($"Arena {type.Name} is not registered");
            }

            var wrapper = (ManagedArenaWrapper<TArena, TSnapshot>)_arenas[index];
            wrapper.RestoreTyped(snapshot);
        }
    }

    /// <summary>
    /// 登録されているArena型の一覧を取得。
    /// </summary>
    public IReadOnlyList<Type> GetRegisteredArenaTypes()
    {
        lock (_lock)
        {
            var types = new List<Type>(_arenas.Count);
            foreach (var wrapper in _arenas)
            {
                types.Add(wrapper.ArenaType);
            }
            return types;
        }
    }
}
