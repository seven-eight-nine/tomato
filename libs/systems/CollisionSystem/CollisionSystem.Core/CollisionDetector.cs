using System;
using System.Collections.Generic;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突検出を行うコアコンポーネント。
/// 空間分割を使用して効率的に衝突を検出する。
/// </summary>
public sealed class CollisionDetector
{
    private readonly ISpatialPartition _spatialPartition;
    private readonly List<(CollisionVolume volume, Vector3 position)> _volumes;
    private readonly List<(CollisionVolume, CollisionVolume)> _potentialPairs;

    public CollisionDetector() : this(new UniformGrid(10f))
    {
    }

    public CollisionDetector(ISpatialPartition spatialPartition)
    {
        _spatialPartition = spatialPartition;
        _volumes = new List<(CollisionVolume, Vector3)>();
        _potentialPairs = new List<(CollisionVolume, CollisionVolume)>();
    }

    /// <summary>
    /// 現在登録されているボリューム数。
    /// </summary>
    public int VolumeCount => _volumes.Count;

    /// <summary>
    /// ボリュームを追加する。
    /// </summary>
    public void AddVolume(CollisionVolume volume, Vector3 position)
    {
        _volumes.Add((volume, position));
        _spatialPartition.Insert(volume, position);
    }

    /// <summary>
    /// 特定のボリュームを削除する。
    /// </summary>
    public void RemoveVolume(CollisionVolume volume)
    {
        for (int i = _volumes.Count - 1; i >= 0; i--)
        {
            if (_volumes[i].volume == volume)
            {
                _volumes.RemoveAt(i);
                break;
            }
        }
        _spatialPartition.Remove(volume);
    }

    /// <summary>
    /// 全てのボリュームをクリアする。
    /// </summary>
    public void Clear()
    {
        _volumes.Clear();
        _spatialPartition.Clear();
    }

    /// <summary>
    /// 1フレーム経過させ、期限切れボリュームを削除する。
    /// </summary>
    public void Tick()
    {
        for (int i = _volumes.Count - 1; i >= 0; i--)
        {
            var (volume, _) = _volumes[i];
            volume.Tick();

            if (volume.IsExpired)
            {
                _volumes.RemoveAt(i);
                _spatialPartition.Remove(volume);
            }
        }
    }

    /// <summary>
    /// 衝突を検出する。
    /// </summary>
    public void DetectCollisions(List<CollisionResult> results)
    {
        results.Clear();
        _potentialPairs.Clear();

        // 空間分割から潜在的な衝突ペアを取得
        _spatialPartition.QueryAllPairs(_potentialPairs);

        // 各ペアについて詳細な衝突判定
        foreach (var (v1, v2) in _potentialPairs)
        {
            // 同じオーナーは衝突しない
            if (v1.Owner == v2.Owner)
                continue;

            // フィルタチェック
            if (!v1.Filter.CanCollideWith(v2.Filter))
                continue;

            // 位置を取得
            var pos1 = GetVolumePosition(v1);
            var pos2 = GetVolumePosition(v2);

            // 詳細な衝突判定
            if (v1.Shape.Intersects(pos1, v2.Shape, pos2, out var contact))
            {
                results.Add(new CollisionResult(v1, v2, contact));
            }
        }
    }

    /// <summary>
    /// 指定ボリュームに衝突するボリュームを検索する。
    /// </summary>
    public void QueryCollisions(CollisionVolume queryVolume, Vector3 queryPosition, List<CollisionResult> results)
    {
        results.Clear();

        var queryBounds = queryVolume.GetBounds(queryPosition);
        var candidates = new List<CollisionVolume>();
        _spatialPartition.Query(queryBounds, candidates);

        foreach (var candidate in candidates)
        {
            if (candidate == queryVolume)
                continue;

            if (candidate.Owner == queryVolume.Owner)
                continue;

            if (!queryVolume.Filter.CanCollideWith(candidate.Filter))
                continue;

            var candidatePos = GetVolumePosition(candidate);

            if (queryVolume.Shape.Intersects(queryPosition, candidate.Shape, candidatePos, out var contact))
            {
                results.Add(new CollisionResult(queryVolume, candidate, contact));
            }
        }
    }

    private Vector3 GetVolumePosition(CollisionVolume volume)
    {
        foreach (var (v, pos) in _volumes)
        {
            if (v == volume)
                return pos;
        }
        return Vector3.Zero;
    }
}
