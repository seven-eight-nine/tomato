using System;
using System.Collections.Generic;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.SpatialIndexSystem;

/// <summary>
/// 空間ハッシュグリッドによる空間インデックス実装。
/// </summary>
public sealed class SpatialHashGrid : ISpatialIndex
{
    private readonly float _cellSize;
    private readonly float _invCellSize;
    private readonly Dictionary<long, List<SpatialEntry>> _cells = new();
    private readonly Dictionary<AnyHandle, (long CellKey, int EntryIndex)> _handleToCell = new();

    /// <summary>セルサイズ</summary>
    public float CellSize => _cellSize;

    /// <summary>エントリ数</summary>
    public int Count => _handleToCell.Count;

    /// <summary>使用中のセル数</summary>
    public int CellCount => _cells.Count;

    public SpatialHashGrid(float cellSize = 10f)
    {
        if (cellSize <= 0f)
            throw new ArgumentException("Cell size must be positive", nameof(cellSize));

        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
    }

    /// <summary>Entityの位置を更新（存在しなければ追加）</summary>
    public void Update(AnyHandle handle, Vector3 position, float radius = 0f)
    {
        var newCellKey = GetCellKey(position);

        if (_handleToCell.TryGetValue(handle, out var existing))
        {
            if (existing.CellKey == newCellKey)
            {
                // 同じセル内 - エントリを更新
                var cell = _cells[existing.CellKey];
                var entry = cell[existing.EntryIndex];
                entry.Position = position;
                entry.Radius = radius;
                cell[existing.EntryIndex] = entry;
                return;
            }

            // セルが変わった - 古いセルから削除
            RemoveFromCell(existing.CellKey, existing.EntryIndex, handle);
        }

        // 新しいセルに追加
        AddToCell(newCellKey, new SpatialEntry(handle, position, radius));
    }

    /// <summary>Entityを削除</summary>
    public bool Remove(AnyHandle handle)
    {
        if (!_handleToCell.TryGetValue(handle, out var existing))
            return false;

        RemoveFromCell(existing.CellKey, existing.EntryIndex, handle);
        return true;
    }

    /// <summary>球範囲内のEntityを検索</summary>
    public void QuerySphere(Vector3 center, float radius, List<AnyHandle> results)
    {
        var minCell = GetCellCoords(new Vector3(center.X - radius, center.Y - radius, center.Z - radius));
        var maxCell = GetCellCoords(new Vector3(center.X + radius, center.Y + radius, center.Z + radius));

        var radiusSq = radius * radius;

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    var cellKey = GetCellKey(x, y, z);
                    if (!_cells.TryGetValue(cellKey, out var cell))
                        continue;

                    foreach (var entry in cell)
                    {
                        var dx = entry.Position.X - center.X;
                        var dy = entry.Position.Y - center.Y;
                        var dz = entry.Position.Z - center.Z;
                        var distSq = dx * dx + dy * dy + dz * dz;
                        var totalRadius = radius + entry.Radius;

                        if (distSq <= totalRadius * totalRadius)
                        {
                            results.Add(entry.Handle);
                        }
                    }
                }
            }
        }
    }

    /// <summary>AABB範囲内のEntityを検索</summary>
    public void QueryAABB(AABB bounds, List<AnyHandle> results)
    {
        var minCell = GetCellCoords(bounds.Min);
        var maxCell = GetCellCoords(bounds.Max);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    var cellKey = GetCellKey(x, y, z);
                    if (!_cells.TryGetValue(cellKey, out var cell))
                        continue;

                    foreach (var entry in cell)
                    {
                        // エントリの半径を考慮したAABB判定
                        var entryMin = new Vector3(
                            entry.Position.X - entry.Radius,
                            entry.Position.Y - entry.Radius,
                            entry.Position.Z - entry.Radius);
                        var entryMax = new Vector3(
                            entry.Position.X + entry.Radius,
                            entry.Position.Y + entry.Radius,
                            entry.Position.Z + entry.Radius);

                        if (entryMax.X >= bounds.Min.X && entryMin.X <= bounds.Max.X &&
                            entryMax.Y >= bounds.Min.Y && entryMin.Y <= bounds.Max.Y &&
                            entryMax.Z >= bounds.Min.Z && entryMin.Z <= bounds.Max.Z)
                        {
                            results.Add(entry.Handle);
                        }
                    }
                }
            }
        }
    }

    /// <summary>最も近いEntityを検索</summary>
    public bool QueryNearest(Vector3 point, float maxDistance, out AnyHandle nearest, out float distance)
    {
        nearest = default;
        distance = float.MaxValue;
        bool found = false;

        var results = new List<AnyHandle>();
        QuerySphere(point, maxDistance, results);

        foreach (var handle in results)
        {
            if (!_handleToCell.TryGetValue(handle, out var cellInfo))
                continue;

            var cell = _cells[cellInfo.CellKey];
            var entry = cell[cellInfo.EntryIndex];

            var dx = entry.Position.X - point.X;
            var dy = entry.Position.Y - point.Y;
            var dz = entry.Position.Z - point.Z;
            var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz) - entry.Radius;

            if (dist < distance)
            {
                distance = dist;
                nearest = handle;
                found = true;
            }
        }

        return found;
    }

    /// <summary>全エントリをクリア</summary>
    public void Clear()
    {
        _cells.Clear();
        _handleToCell.Clear();
    }

    private (int x, int y, int z) GetCellCoords(Vector3 position)
    {
        return (
            (int)MathF.Floor(position.X * _invCellSize),
            (int)MathF.Floor(position.Y * _invCellSize),
            (int)MathF.Floor(position.Z * _invCellSize)
        );
    }

    private long GetCellKey(Vector3 position)
    {
        var coords = GetCellCoords(position);
        return GetCellKey(coords.x, coords.y, coords.z);
    }

    private static long GetCellKey(int x, int y, int z)
    {
        // 各座標を21ビットに収める（-1048576 ～ 1048575）
        const long mask = 0x1FFFFF;
        return ((long)(x & mask) << 42) | ((long)(y & mask) << 21) | (long)(z & mask);
    }

    private void AddToCell(long cellKey, SpatialEntry entry)
    {
        if (!_cells.TryGetValue(cellKey, out var cell))
        {
            cell = new List<SpatialEntry>();
            _cells[cellKey] = cell;
        }

        var index = cell.Count;
        cell.Add(entry);
        _handleToCell[entry.Handle] = (cellKey, index);
    }

    private void RemoveFromCell(long cellKey, int index, AnyHandle handle)
    {
        var cell = _cells[cellKey];

        // 最後の要素と入れ替えて削除
        var lastIndex = cell.Count - 1;
        if (index != lastIndex)
        {
            var lastEntry = cell[lastIndex];
            cell[index] = lastEntry;
            _handleToCell[lastEntry.Handle] = (cellKey, index);
        }

        cell.RemoveAt(lastIndex);
        _handleToCell.Remove(handle);

        // セルが空になったら削除
        if (cell.Count == 0)
        {
            _cells.Remove(cellKey);
        }
    }
}
