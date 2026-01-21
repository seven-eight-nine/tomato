using System.Collections.Concurrent;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// クエリ結果をフレーム内でキャッシュするクラス。
/// 同じフレーム内で同じクエリが実行された場合、キャッシュから結果を返します。
/// スレッドセーフです。
/// </summary>
public sealed class QueryCache
{
    private readonly ConcurrentDictionary<IEntityQuery, IReadOnlyList<AnyHandle>> _cache;
    private volatile int _lastFrameCount;
    private readonly object _frameLock = new();

    public QueryCache()
    {
        _cache = new ConcurrentDictionary<IEntityQuery, IReadOnlyList<AnyHandle>>();
        _lastFrameCount = -1;
    }

    /// <summary>
    /// クエリを実行し、結果をキャッシュまたはキャッシュから取得します。
    /// </summary>
    /// <param name="query">実行するクエリ</param>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="frameCount">現在のフレーム番号</param>
    /// <returns>クエリ結果のエンティティリスト</returns>
    public IReadOnlyList<AnyHandle> GetOrExecute(
        IEntityQuery query,
        IEntityRegistry registry,
        int frameCount)
    {
        // フレームが変わったらキャッシュをクリア
        if (frameCount != _lastFrameCount)
        {
            lock (_frameLock)
            {
                // ダブルチェック
                if (frameCount != _lastFrameCount)
                {
                    _cache.Clear();
                    _lastFrameCount = frameCount;
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
        lock (_frameLock)
        {
            _cache.Clear();
            _lastFrameCount = -1;
        }
    }
}
