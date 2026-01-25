using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 空間ハッシュ法による Broad Phase 実装。
/// 固定サイズのグリッドセルにオブジェクトを登録し、O(1)で追加/削除。
/// 均一分布や中規模のシーンに適する。
/// </summary>
public sealed class SpatialHashBroadPhase : IBroadPhase
{
    private const int BruteForceThreshold = 32;
    private const int MaxCellsToQuery = 512; // セル走査数の上限

    // セル配列（オープンアドレス法のハッシュテーブル）
    private readonly Cell[] _cells;
    private readonly int _cellMask;

    // Shape情報
    private readonly ShapeEntry[] _shapes;
    private int _shapeCount;

    // セルサイズ
    private readonly float _cellSize;
    private readonly float _invCellSize;

    // 重複排除用マーカー（クエリごとにインクリメント）
    private readonly int[] _queryMarker;
    private int _currentQueryId;

    /// <summary>
    /// SpatialHashBroadPhase を作成する。
    /// </summary>
    /// <param name="cellSize">セルサイズ</param>
    /// <param name="maxShapes">最大Shape数</param>
    /// <param name="cellCapacity">ハッシュテーブルサイズ（2のべき乗）</param>
    public SpatialHashBroadPhase(float cellSize = 8f, int maxShapes = 1024, int cellCapacity = 4096)
    {
        if (cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize));

        // 2のべき乗に丸める
        cellCapacity = NextPowerOfTwo(cellCapacity);

        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
        _cells = new Cell[cellCapacity];
        _cellMask = cellCapacity - 1;

        // セル初期化
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = new Cell { ShapeIndices = new int[16], Count = 0 };
        }

        // Shape配列初期化（0番は使用しない）
        _shapes = new ShapeEntry[maxShapes + 1];
        _shapeCount = 0;

        // 重複排除用マーカー初期化
        _queryMarker = new int[maxShapes + 1];
        _currentQueryId = 0;
    }

    /// <summary>
    /// セルサイズ。
    /// </summary>
    public float CellSize => _cellSize;

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _shapeCount;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        // 必要に応じて配列を拡張（ここでは固定サイズで対応）
        if (shapeIndex >= _shapes.Length)
            return;

        ComputeCellRange(aabb, out int minX, out int minY, out int minZ, out int maxX, out int maxY, out int maxZ);

        // 登録セル数を計算
        int cellCount = (maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1);

        ref var shape = ref _shapes[shapeIndex];
        shape.IsActive = true;
        shape.CellKeys = new long[cellCount];
        shape.CellCount = 0;

        // 各セルに登録
        for (int cx = minX; cx <= maxX; cx++)
        {
            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long key = PackCellKey(cx, cy, cz);
                    int cellIndex = GetCellIndex(key);

                    ref var cell = ref _cells[cellIndex];
                    EnsureCellCapacity(ref cell, cell.Count + 1);
                    cell.ShapeIndices[cell.Count++] = shapeIndex;

                    if (shape.CellCount < shape.CellKeys.Length)
                    {
                        shape.CellKeys[shape.CellCount++] = key;
                    }
                }
            }
        }

        _shapeCount++;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        if (shapeIndex >= _shapes.Length || !_shapes[shapeIndex].IsActive)
            return false;

        ref var shape = ref _shapes[shapeIndex];

        // 各セルから削除
        for (int i = 0; i < shape.CellCount; i++)
        {
            long key = shape.CellKeys[i];
            int cellIndex = GetCellIndex(key);

            ref var cell = ref _cells[cellIndex];
            RemoveFromCell(ref cell, shapeIndex);
        }

        shape.IsActive = false;
        shape.CellCount = 0;
        _shapeCount--;

        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        // 同じセル範囲なら何もしない
        ComputeCellRange(oldAABB, out int oldMinX, out int oldMinY, out int oldMinZ, out int oldMaxX, out int oldMaxY, out int oldMaxZ);
        ComputeCellRange(newAABB, out int newMinX, out int newMinY, out int newMinZ, out int newMaxX, out int newMaxY, out int newMaxZ);

        if (oldMinX == newMinX && oldMinY == newMinY && oldMinZ == newMinZ &&
            oldMaxX == newMaxX && oldMaxY == newMaxY && oldMaxZ == newMaxZ)
        {
            return;
        }

        // 再登録
        Remove(shapeIndex);
        Add(shapeIndex, newAABB);
    }

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    public int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        // 少数の場合は愚直検索
        if (_shapeCount <= BruteForceThreshold)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        ComputeCellRange(queryAABB, out int minX, out int minY, out int minZ, out int maxX, out int maxY, out int maxZ);

        // セル数が多すぎる場合は愚直検索にフォールバック
        int cellCount = (maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1);
        if (cellCount > MaxCellsToQuery)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        // クエリIDをインクリメント（重複排除用）
        _currentQueryId++;
        if (_currentQueryId == 0)
        {
            // オーバーフロー時はマーカーをリセット
            Array.Clear(_queryMarker, 0, _queryMarker.Length);
            _currentQueryId = 1;
        }

        int count = 0;

        for (int cx = minX; cx <= maxX; cx++)
        {
            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long key = PackCellKey(cx, cy, cz);
                    int cellIndex = GetCellIndex(key);

                    ref readonly var cell = ref _cells[cellIndex];

                    for (int i = 0; i < cell.Count && count < candidates.Length; i++)
                    {
                        int shapeIndex = cell.ShapeIndices[i];

                        // O(1)重複チェック
                        if (_queryMarker[shapeIndex] == _currentQueryId)
                            continue;
                        _queryMarker[shapeIndex] = _currentQueryId;

                        // 3軸AABBオーバーラップ確認
                        if (allAABBs[shapeIndex].Intersects(queryAABB))
                        {
                            candidates[count++] = shapeIndex;
                        }
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 全データをクリアする。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i].Count = 0;
        }

        for (int i = 0; i < _shapes.Length; i++)
        {
            _shapes[i].IsActive = false;
            _shapes[i].CellCount = 0;
        }

        _shapeCount = 0;
    }

    private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        int count = 0;

        for (int i = 0; i < _shapes.Length && count < candidates.Length; i++)
        {
            if (!_shapes[i].IsActive)
                continue;

            if (allAABBs[i].Intersects(queryAABB))
            {
                candidates[count++] = i;
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeCellRange(in AABB aabb, out int minX, out int minY, out int minZ, out int maxX, out int maxY, out int maxZ)
    {
        minX = (int)MathF.Floor(aabb.Min.X * _invCellSize);
        minY = (int)MathF.Floor(aabb.Min.Y * _invCellSize);
        minZ = (int)MathF.Floor(aabb.Min.Z * _invCellSize);
        maxX = (int)MathF.Floor(aabb.Max.X * _invCellSize);
        maxY = (int)MathF.Floor(aabb.Max.Y * _invCellSize);
        maxZ = (int)MathF.Floor(aabb.Max.Z * _invCellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackCellKey(int x, int y, int z)
    {
        return ((long)(x + 0x100000) << 42) | ((long)(y + 0x100000) << 21) | (long)(z + 0x100000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetCellIndex(long key)
    {
        // 簡易ハッシュ
        ulong hash = (ulong)key;
        hash ^= hash >> 33;
        hash *= 0xff51afd7ed558ccdUL;
        hash ^= hash >> 33;
        return (int)(hash & (ulong)_cellMask);
    }

    private static void EnsureCellCapacity(ref Cell cell, int required)
    {
        if (cell.ShapeIndices.Length >= required)
            return;

        int newCapacity = System.Math.Max(cell.ShapeIndices.Length * 2, required);
        var newArray = new int[newCapacity];
        Array.Copy(cell.ShapeIndices, newArray, cell.Count);
        cell.ShapeIndices = newArray;
    }

    private static void RemoveFromCell(ref Cell cell, int shapeIndex)
    {
        for (int i = 0; i < cell.Count; i++)
        {
            if (cell.ShapeIndices[i] == shapeIndex)
            {
                cell.ShapeIndices[i] = cell.ShapeIndices[--cell.Count];
                return;
            }
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    private struct Cell
    {
        public int[] ShapeIndices;
        public int Count;
    }

    private struct ShapeEntry
    {
        public bool IsActive;
        public long[] CellKeys;
        public int CellCount;
    }
}
