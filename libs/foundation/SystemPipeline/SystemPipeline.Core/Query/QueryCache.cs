using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// クエリ結果をtick内でキャッシュするクラス。
/// 同じtick内で同じクエリが実行された場合、キャッシュから結果を返します。
/// スレッドセーフです。
/// </summary>
public sealed class QueryCache
{
    private readonly ConcurrentDictionary<IEntityQuery, IReadOnlyList<AnyHandle>> _cache;
    private long _lastTick;
    private readonly object _tickLock = new();

    public QueryCache()
    {
        _cache = new ConcurrentDictionary<IEntityQuery, IReadOnlyList<AnyHandle>>();
        _lastTick = -1;
    }

    /// <summary>
    /// クエリを実行し、結果をキャッシュまたはキャッシュから取得します。
    /// </summary>
    /// <param name="query">実行するクエリ</param>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="currentTick">現在のtick</param>
    /// <returns>クエリ結果のエンティティリスト</returns>
    public IReadOnlyList<AnyHandle> GetOrExecute(
        IEntityQuery query,
        IEntityRegistry registry,
        long currentTick)
    {
        // tickが変わったらキャッシュをクリア
        if (currentTick != Interlocked.Read(ref _lastTick))
        {
            lock (_tickLock)
            {
                // ダブルチェック
                if (currentTick != Interlocked.Read(ref _lastTick))
                {
                    _cache.Clear();
                    Interlocked.Exchange(ref _lastTick, currentTick);
                }
            }
        }

        // キャッシュにあれば返す、なければ実行してキャッシュ
        return _cache.GetOrAdd(query, q => ExecuteQuery(q, registry));
    }

    private static IReadOnlyList<AnyHandle> ExecuteQuery(IEntityQuery query, IEntityRegistry registry)
    {
        var allEntities = registry.GetAllEntities();
        var result = new List<AnyHandle>();

        foreach (var handle in query.Filter(registry, allEntities))
        {
            result.Add(handle);
        }

        return result;
    }

    /// <summary>
    /// キャッシュをクリアします。
    /// </summary>
    public void Clear()
    {
        lock (_tickLock)
        {
            _cache.Clear();
            Interlocked.Exchange(ref _lastTick, -1);
        }
    }
}
