using System;
using System.Collections.Generic;

namespace Tomato.CollisionSystem;

/// <summary>
/// 空間分割のインターフェース。
/// </summary>
public interface ISpatialPartition
{
    /// <summary>ボリュームを登録する。</summary>
    void Insert(CollisionVolume volume, Vector3 position);

    /// <summary>ボリュームを削除する。</summary>
    void Remove(CollisionVolume volume);

    /// <summary>全ボリュームをクリアする。</summary>
    void Clear();

    /// <summary>指定AABBと交差する可能性のあるボリュームを取得する。</summary>
    void Query(AABB bounds, List<CollisionVolume> results);

    /// <summary>全ての潜在的な衝突ペアを取得する。</summary>
    void QueryAllPairs(List<(CollisionVolume, CollisionVolume)> results);
}

/// <summary>
/// グリッドベースの空間分割。
/// 一様なセルサイズで3D空間を分割し、効率的な衝突候補の絞り込みを行う。
/// </summary>
public sealed class UniformGrid : ISpatialPartition
{
    private readonly float _cellSize;
    private readonly float _inverseCellSize;
    private readonly Dictionary<(int x, int y, int z), List<CollisionVolume>> _cells;
    private readonly Dictionary<CollisionVolume, List<(int x, int y, int z)>> _volumeCells;

    public UniformGrid(float cellSize = 10f)
    {
        if (cellSize <= 0)
            throw new ArgumentException("Cell size must be positive", nameof(cellSize));

        _cellSize = cellSize;
        _inverseCellSize = 1f / cellSize;
        _cells = new Dictionary<(int, int, int), List<CollisionVolume>>();
        _volumeCells = new Dictionary<CollisionVolume, List<(int, int, int)>>();
    }

    public void Insert(CollisionVolume volume, Vector3 position)
    {
        var bounds = volume.GetBounds(position);
        var minCell = WorldToCell(bounds.Min);
        var maxCell = WorldToCell(bounds.Max);

        var cells = new List<(int, int, int)>();

        for (int x = minCell.x; x <= maxCell.x; x++)
        for (int y = minCell.y; y <= maxCell.y; y++)
        for (int z = minCell.z; z <= maxCell.z; z++)
        {
            var cellKey = (x, y, z);
            if (!_cells.TryGetValue(cellKey, out var list))
            {
                list = new List<CollisionVolume>();
                _cells[cellKey] = list;
            }
            list.Add(volume);
            cells.Add(cellKey);
        }

        _volumeCells[volume] = cells;
    }

    public void Remove(CollisionVolume volume)
    {
        if (!_volumeCells.TryGetValue(volume, out var cells))
            return;

        foreach (var cellKey in cells)
        {
            if (_cells.TryGetValue(cellKey, out var list))
            {
                list.Remove(volume);
                if (list.Count == 0)
                {
                    _cells.Remove(cellKey);
                }
            }
        }

        _volumeCells.Remove(volume);
    }

    public void Clear()
    {
        _cells.Clear();
        _volumeCells.Clear();
    }

    public void Query(AABB bounds, List<CollisionVolume> results)
    {
        var minCell = WorldToCell(bounds.Min);
        var maxCell = WorldToCell(bounds.Max);

        var seen = new HashSet<CollisionVolume>();

        for (int x = minCell.x; x <= maxCell.x; x++)
        for (int y = minCell.y; y <= maxCell.y; y++)
        for (int z = minCell.z; z <= maxCell.z; z++)
        {
            if (_cells.TryGetValue((x, y, z), out var list))
            {
                foreach (var volume in list)
                {
                    if (seen.Add(volume))
                    {
                        results.Add(volume);
                    }
                }
            }
        }
    }

    public void QueryAllPairs(List<(CollisionVolume, CollisionVolume)> results)
    {
        var seenPairs = new HashSet<(CollisionVolume, CollisionVolume)>();

        foreach (var (_, volumes) in _cells)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                for (int j = i + 1; j < volumes.Count; j++)
                {
                    var v1 = volumes[i];
                    var v2 = volumes[j];

                    // 順序を正規化してペアの重複を防ぐ
                    var pair = v1.GetHashCode() < v2.GetHashCode()
                        ? (v1, v2)
                        : (v2, v1);

                    if (seenPairs.Add(pair))
                    {
                        results.Add(pair);
                    }
                }
            }
        }
    }

    private (int x, int y, int z) WorldToCell(Vector3 worldPos)
    {
        return (
            (int)MathF.Floor(worldPos.X * _inverseCellSize),
            (int)MathF.Floor(worldPos.Y * _inverseCellSize),
            (int)MathF.Floor(worldPos.Z * _inverseCellSize));
    }
}
