using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// クエリ結果をフレーム内でキャッシュするクラス。
/// 同じフレーム内で同じクエリが実行された場合、キャッシュから結果を返します。
/// </summary>
public sealed class QueryCache
{
    private readonly Dictionary<IEntityQuery, IReadOnlyList<VoidHandle>> _cache;
    private int _lastFrameCount;

    public QueryCache()
    {
        _cache = new Dictionary<IEntityQuery, IReadOnlyList<VoidHandle>>();
        _lastFrameCount = -1;
    }

    /// <summary>
    /// クエリを実行し、結果をキャッシュまたはキャッシュから取得します。
    /// </summary>
    /// <param name="query">実行するクエリ</param>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="frameCount">現在のフレーム番号</param>
    /// <returns>クエリ結果のエンティティリスト</returns>
    public IReadOnlyList<VoidHandle> GetOrExecute(
        IEntityQuery query,
        IEntityRegistry registry,
        int frameCount)
    {
        // フレームが変わったらキャッシュをクリア
        if (frameCount != _lastFrameCount)
        {
            _cache.Clear();
            _lastFrameCount = frameCount;
        }

        // キャッシュにあれば返す
        if (_cache.TryGetValue(query, out var cached))
        {
            return cached;
        }

        // クエリを実行してキャッシュ
        var allEntities = registry.GetAllEntities();
        var result = new List<VoidHandle>();

        foreach (var handle in query.Filter(registry, allEntities))
        {
            result.Add(handle);
        }

        _cache[query] = result;
        return result;
    }

    /// <summary>
    /// キャッシュをクリアします。
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _lastFrameCount = -1;
    }
}
