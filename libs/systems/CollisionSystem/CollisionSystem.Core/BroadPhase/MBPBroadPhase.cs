using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Multi-Box Pruning (MBP) による Broad Phase 実装。
/// 空間をリージョンに分割し、各リージョン内でソートベースの枝刈りを行う。
/// 大規模シミュレーションやバッチ処理に適する。
/// </summary>
public sealed class MBPBroadPhase : IBroadPhase
{
    private const int BruteForceThreshold = 32;
    private const int InvalidIndex = -1;

    // リージョン
    private readonly Region[] _regions;
    private readonly int _regionsX;
    private readonly int _regionsZ;
    private readonly float _regionSizeX;
    private readonly float _regionSizeZ;

    // Shape情報
    private readonly ShapeEntry[] _shapes;
    private int _shapeCount;

    // ワールド境界
    private readonly AABB _worldBounds;

    // ワールド境界外オブジェクト
    private readonly int[] _outOfBoundsShapes;
    private int _outOfBoundsCount;

    // 重複排除用マーカー
    private readonly int[] _queryMarker;
    private int _currentQueryId;

    /// <summary>
    /// MBPBroadPhase を作成する。
    /// </summary>
    /// <param name="worldBounds">ワールド境界</param>
    /// <param name="regionsX">X方向リージョン数</param>
    /// <param name="regionsZ">Z方向リージョン数</param>
    /// <param name="maxShapes">最大Shape数</param>
    public MBPBroadPhase(in AABB worldBounds, int regionsX = 4, int regionsZ = 4, int maxShapes = 1024)
    {
        _worldBounds = worldBounds;
        _regionsX = regionsX;
        _regionsZ = regionsZ;

        var size = worldBounds.Size;
        _regionSizeX = size.X / regionsX;
        _regionSizeZ = size.Z / regionsZ;

        _regions = new Region[regionsX * regionsZ];
        for (int i = 0; i < _regions.Length; i++)
        {
            _regions[i] = new Region
            {
                ShapeIndices = new int[256],
                MinX = new float[256],
                MaxX = new float[256],
                Count = 0,
                IsSorted = true
            };
        }

        _shapes = new ShapeEntry[maxShapes + 1];
        _shapeCount = 0;

        _outOfBoundsShapes = new int[256];
        _outOfBoundsCount = 0;

        _queryMarker = new int[maxShapes + 1];
        _currentQueryId = 0;
    }

    /// <summary>
    /// ワールド境界。
    /// </summary>
    public AABB WorldBounds => _worldBounds;

    /// <summary>
    /// リージョン数（X方向）。
    /// </summary>
    public int RegionsX => _regionsX;

    /// <summary>
    /// リージョン数（Z方向）。
    /// </summary>
    public int RegionsZ => _regionsZ;

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _shapeCount;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        if (shapeIndex >= _shapes.Length)
            return;

        ref var shape = ref _shapes[shapeIndex];
        shape.IsActive = true;
        shape.AABB = aabb;
        shape.RegionIndices = new int[4];
        shape.RegionCount = 0;
        shape.IsOutOfBounds = false;

        // ワールド境界外チェック
        if (!_worldBounds.Intersects(aabb))
        {
            // 完全にワールド境界外の場合は境界外リストに追加
            if (_outOfBoundsCount < _outOfBoundsShapes.Length)
            {
                shape.IsOutOfBounds = true;
                _outOfBoundsShapes[_outOfBoundsCount++] = shapeIndex;
            }
            _shapeCount++;
            return;
        }

        // 所属リージョンを計算
        GetRegionRange(aabb, out int minRX, out int minRZ, out int maxRX, out int maxRZ);

        for (int rz = minRZ; rz <= maxRZ; rz++)
        {
            for (int rx = minRX; rx <= maxRX; rx++)
            {
                int regionIndex = rz * _regionsX + rx;
                AddToRegion(shapeIndex, regionIndex, aabb);

                if (shape.RegionCount < shape.RegionIndices.Length)
                {
                    shape.RegionIndices[shape.RegionCount++] = regionIndex;
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

        // 境界外リストから削除
        if (shape.IsOutOfBounds)
        {
            for (int i = 0; i < _outOfBoundsCount; i++)
            {
                if (_outOfBoundsShapes[i] == shapeIndex)
                {
                    _outOfBoundsShapes[i] = _outOfBoundsShapes[--_outOfBoundsCount];
                    break;
                }
            }
        }
        else
        {
            // 各リージョンから削除
            for (int i = 0; i < shape.RegionCount; i++)
            {
                int regionIndex = shape.RegionIndices[i];
                RemoveFromRegion(shapeIndex, regionIndex);
            }
        }

        shape.IsActive = false;
        shape.IsOutOfBounds = false;
        shape.RegionCount = 0;
        _shapeCount--;

        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        if (shapeIndex >= _shapes.Length || !_shapes[shapeIndex].IsActive)
            return;

        // リージョン範囲が同じかチェック
        GetRegionRange(oldAABB, out int oldMinRX, out int oldMinRZ, out int oldMaxRX, out int oldMaxRZ);
        GetRegionRange(newAABB, out int newMinRX, out int newMinRZ, out int newMaxRX, out int newMaxRZ);

        _shapes[shapeIndex].AABB = newAABB;

        if (oldMinRX == newMinRX && oldMinRZ == newMinRZ &&
            oldMaxRX == newMaxRX && oldMaxRZ == newMaxRZ)
        {
            // 同じリージョンなら座標のみ更新
            ref var shape = ref _shapes[shapeIndex];
            for (int i = 0; i < shape.RegionCount; i++)
            {
                int regionIndex = shape.RegionIndices[i];
                UpdateInRegion(shapeIndex, regionIndex, newAABB);
            }
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
        if (_shapeCount <= BruteForceThreshold)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        GetRegionRange(queryAABB, out int minRX, out int minRZ, out int maxRX, out int maxRZ);

        // クエリIDをインクリメント（重複排除用）
        _currentQueryId++;
        if (_currentQueryId == 0)
        {
            Array.Clear(_queryMarker, 0, _queryMarker.Length);
            _currentQueryId = 1;
        }

        int count = 0;

        float queryMinX = queryAABB.Min.X;
        float queryMaxX = queryAABB.Max.X;

        for (int rz = minRZ; rz <= maxRZ; rz++)
        {
            for (int rx = minRX; rx <= maxRX; rx++)
            {
                int regionIndex = rz * _regionsX + rx;
                ref var region = ref _regions[regionIndex];

                // ソートが必要なら実行
                if (!region.IsSorted)
                {
                    SortRegion(ref region);
                }

                // MinXでソートされているため、MinX > queryMaxX になったら終了可能
                // ただし、MinX < queryMinX でも MaxX >= queryMinX の形状を見逃さないよう
                // 先頭から走査する
                for (int i = 0; i < region.Count && count < candidates.Length; i++)
                {
                    // X軸の最小値がクエリの最大値を超えたら終了
                    if (region.MinX[i] > queryMaxX)
                        break;

                    int shapeIndex = region.ShapeIndices[i];

                    // X軸オーバーラップチェック（MaxXがqueryMinXより小さければスキップ）
                    if (region.MaxX[i] < queryMinX)
                        continue;

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

        // 境界外オブジェクトもチェック
        for (int i = 0; i < _outOfBoundsCount && count < candidates.Length; i++)
        {
            int shapeIndex = _outOfBoundsShapes[i];

            if (_queryMarker[shapeIndex] == _currentQueryId)
                continue;
            _queryMarker[shapeIndex] = _currentQueryId;

            if (allAABBs[shapeIndex].Intersects(queryAABB))
            {
                candidates[count++] = shapeIndex;
            }
        }

        return count;
    }

    /// <summary>
    /// 全データをクリアする。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _regions.Length; i++)
        {
            _regions[i].Count = 0;
            _regions[i].IsSorted = true;
        }

        for (int i = 0; i < _shapes.Length; i++)
        {
            _shapes[i] = default;
        }

        _outOfBoundsCount = 0;
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
    private void GetRegionRange(in AABB aabb, out int minRX, out int minRZ, out int maxRX, out int maxRZ)
    {
        float invSizeX = 1f / _regionSizeX;
        float invSizeZ = 1f / _regionSizeZ;

        minRX = MathUtils.Clamp((int)((aabb.Min.X - _worldBounds.Min.X) * invSizeX), 0, _regionsX - 1);
        minRZ = MathUtils.Clamp((int)((aabb.Min.Z - _worldBounds.Min.Z) * invSizeZ), 0, _regionsZ - 1);
        maxRX = MathUtils.Clamp((int)((aabb.Max.X - _worldBounds.Min.X) * invSizeX), 0, _regionsX - 1);
        maxRZ = MathUtils.Clamp((int)((aabb.Max.Z - _worldBounds.Min.Z) * invSizeZ), 0, _regionsZ - 1);
    }

    private void AddToRegion(int shapeIndex, int regionIndex, in AABB aabb)
    {
        ref var region = ref _regions[regionIndex];
        EnsureRegionCapacity(ref region, region.Count + 1);

        int idx = region.Count++;
        region.ShapeIndices[idx] = shapeIndex;
        region.MinX[idx] = aabb.Min.X;
        region.MaxX[idx] = aabb.Max.X;
        region.IsSorted = false;
    }

    private void RemoveFromRegion(int shapeIndex, int regionIndex)
    {
        ref var region = ref _regions[regionIndex];

        for (int i = 0; i < region.Count; i++)
        {
            if (region.ShapeIndices[i] == shapeIndex)
            {
                // 末尾要素と入れ替え
                int last = --region.Count;
                region.ShapeIndices[i] = region.ShapeIndices[last];
                region.MinX[i] = region.MinX[last];
                region.MaxX[i] = region.MaxX[last];
                region.IsSorted = false;
                return;
            }
        }
    }

    private void UpdateInRegion(int shapeIndex, int regionIndex, in AABB aabb)
    {
        ref var region = ref _regions[regionIndex];

        for (int i = 0; i < region.Count; i++)
        {
            if (region.ShapeIndices[i] == shapeIndex)
            {
                region.MinX[i] = aabb.Min.X;
                region.MaxX[i] = aabb.Max.X;
                region.IsSorted = false;
                return;
            }
        }
    }

    private void SortRegion(ref Region region)
    {
        // 挿入ソート（小規模なので効率的）
        for (int i = 1; i < region.Count; i++)
        {
            int keyShape = region.ShapeIndices[i];
            float keyMinX = region.MinX[i];
            float keyMaxX = region.MaxX[i];

            int j = i - 1;
            while (j >= 0 && region.MinX[j] > keyMinX)
            {
                region.ShapeIndices[j + 1] = region.ShapeIndices[j];
                region.MinX[j + 1] = region.MinX[j];
                region.MaxX[j + 1] = region.MaxX[j];
                j--;
            }

            region.ShapeIndices[j + 1] = keyShape;
            region.MinX[j + 1] = keyMinX;
            region.MaxX[j + 1] = keyMaxX;
        }

        region.IsSorted = true;
    }

    private static void EnsureRegionCapacity(ref Region region, int required)
    {
        if (region.ShapeIndices.Length >= required)
            return;

        int newCapacity = System.Math.Max(region.ShapeIndices.Length * 2, required);
        var newShapes = new int[newCapacity];
        var newMinX = new float[newCapacity];
        var newMaxX = new float[newCapacity];

        Array.Copy(region.ShapeIndices, newShapes, region.Count);
        Array.Copy(region.MinX, newMinX, region.Count);
        Array.Copy(region.MaxX, newMaxX, region.Count);

        region.ShapeIndices = newShapes;
        region.MinX = newMinX;
        region.MaxX = newMaxX;
    }

    private struct Region
    {
        public int[] ShapeIndices;
        public float[] MinX;
        public float[] MaxX;
        public int Count;
        public bool IsSorted;
    }

    private struct ShapeEntry
    {
        public bool IsActive;
        public bool IsOutOfBounds;
        public AABB AABB;
        public int[] RegionIndices;
        public int RegionCount;
    }
}
