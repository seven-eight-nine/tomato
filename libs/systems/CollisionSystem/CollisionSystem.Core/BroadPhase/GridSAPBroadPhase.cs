using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Grid + SAP (Sweep and Prune) による Broad Phase 実装。
/// 空間を固定サイズのゾーンに分割し、各ゾーン内で SAP を適用する。
/// </summary>
public sealed class GridSAPBroadPhase : IBroadPhase
{
    private readonly float _gridSize;
    private readonly float _invGridSize;
    private readonly SAPAxisMode _axisMode;

    // ゾーン座標 → Zone のマッピング
    private readonly Dictionary<long, Zone> _zones;

    // ShapeIndex → 所属ゾーン座標リスト
    private readonly Dictionary<int, List<long>> _shapeToZones;

    // 愚直検索のしきい値
    private const int BruteForceThreshold = 32;

    // ゾーン数制限（大きいオブジェクトがグリッドを溢れないようにする）
    private const int MaxZonesPerShape = 64;

    // クエリ時のゾーン走査上限（これを超えると愚直検索にフォールバック）
    private const int MaxZonesToQuery = 2048;

    // 重複排除用マーカー
    private int[] _queryMarker;
    private int _currentQueryId;
    private int _maxShapeIndex;

    // 大オブジェクトリスト（ゾーン制限を超えるオブジェクト）
    private readonly List<int> _largeObjects = new List<int>();

    public GridSAPBroadPhase(float gridSize, SAPAxisMode axisMode = SAPAxisMode.X)
    {
        if (gridSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(gridSize));

        _gridSize = gridSize;
        _invGridSize = 1f / gridSize;
        _axisMode = axisMode;
        _zones = new Dictionary<long, Zone>();
        _shapeToZones = new Dictionary<int, List<long>>();

        _queryMarker = new int[1024];
        _currentQueryId = 0;
        _maxShapeIndex = 0;
    }

    /// <summary>
    /// グリッドサイズ（1セルの辺の長さ）。
    /// </summary>
    public float GridSize => _gridSize;

    /// <summary>
    /// アクティブなゾーン数。
    /// </summary>
    public int ZoneCount => _zones.Count;

    /// <summary>
    /// 軸モード。
    /// </summary>
    public SAPAxisMode AxisMode => _axisMode;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        // マーカー配列を必要に応じて拡張
        EnsureMarkerCapacity(shapeIndex);

        if (!_shapeToZones.TryGetValue(shapeIndex, out var zoneList))
        {
            zoneList = new List<long>(4);
            _shapeToZones[shapeIndex] = zoneList;
        }
        else
        {
            zoneList.Clear();
        }

        GetAxisValues(aabb, out float minPrimary, out float maxPrimary, out float minSecondary, out float maxSecondary);

        // ゾーン範囲を計算
        int minZx = (int)MathF.Floor(aabb.Min.X * _invGridSize);
        int minZy = (int)MathF.Floor(aabb.Min.Y * _invGridSize);
        int minZz = (int)MathF.Floor(aabb.Min.Z * _invGridSize);
        int maxZx = (int)MathF.Floor(aabb.Max.X * _invGridSize);
        int maxZy = (int)MathF.Floor(aabb.Max.Y * _invGridSize);
        int maxZz = (int)MathF.Floor(aabb.Max.Z * _invGridSize);

        int zoneCount = (maxZx - minZx + 1) * (maxZy - minZy + 1) * (maxZz - minZz + 1);

        // ゾーン数が多すぎる場合は大オブジェクトリストに追加
        if (zoneCount > MaxZonesPerShape)
        {
            _largeObjects.Add(shapeIndex);
            return;
        }

        for (int zx = minZx; zx <= maxZx; zx++)
        {
            for (int zy = minZy; zy <= maxZy; zy++)
            {
                for (int zz = minZz; zz <= maxZz; zz++)
                {
                    long coord = PackZoneCoord(zx, zy, zz);
                    var zone = GetOrCreateZone(coord);
                    zone.Add(shapeIndex, minPrimary, maxPrimary, minSecondary, maxSecondary);
                    zoneList.Add(coord);
                }
            }
        }
    }

    private void EnsureMarkerCapacity(int shapeIndex)
    {
        if (shapeIndex >= _queryMarker.Length)
        {
            int newSize = System.Math.Max(_queryMarker.Length * 2, shapeIndex + 1);
            Array.Resize(ref _queryMarker, newSize);
        }
        if (shapeIndex > _maxShapeIndex)
            _maxShapeIndex = shapeIndex;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        // 大オブジェクトリストからの削除を試行
        _largeObjects.Remove(shapeIndex);

        if (!_shapeToZones.TryGetValue(shapeIndex, out var zoneList))
            return _largeObjects.Contains(shapeIndex) == false;

        foreach (var coord in zoneList)
        {
            if (_zones.TryGetValue(coord, out var zone))
            {
                zone.Remove(shapeIndex);

                // 空になったゾーンを削除
                if (zone.Count == 0)
                {
                    _zones.Remove(coord);
                }
            }
        }

        _shapeToZones.Remove(shapeIndex);
        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        var oldCoords = GetZoneCoordinates(oldAABB);
        var newCoords = GetZoneCoordinates(newAABB);

        // 同じゾーン座標なら更新のみ
        if (SameCoordinates(oldCoords, newCoords))
        {
            GetAxisValues(newAABB, out float minPrimary, out float maxPrimary, out float minSecondary, out float maxSecondary);

            foreach (var coord in newCoords)
            {
                if (_zones.TryGetValue(coord, out var zone))
                {
                    zone.Update(shapeIndex, minPrimary, maxPrimary, minSecondary, maxSecondary);
                }
            }
            return;
        }

        // ゾーンが変わった場合は再登録
        Remove(shapeIndex);
        Add(shapeIndex, newAABB);
    }

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    /// <returns>候補数</returns>
    public int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        // 総数が少ない場合は愚直検索
        int totalShapes = CountTotalShapes();
        if (totalShapes <= BruteForceThreshold)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        // クエリIDをインクリメント（重複排除用）
        _currentQueryId++;
        if (_currentQueryId == 0)
        {
            Array.Clear(_queryMarker, 0, _queryMarker.Length);
            _currentQueryId = 1;
        }

        GetAxisValues(queryAABB, out float minPrimary, out float maxPrimary, out float minSecondary, out float maxSecondary);

        // ゾーン範囲を計算
        int minZx = (int)MathF.Floor(queryAABB.Min.X * _invGridSize);
        int minZy = (int)MathF.Floor(queryAABB.Min.Y * _invGridSize);
        int minZz = (int)MathF.Floor(queryAABB.Min.Z * _invGridSize);
        int maxZx = (int)MathF.Floor(queryAABB.Max.X * _invGridSize);
        int maxZy = (int)MathF.Floor(queryAABB.Max.Y * _invGridSize);
        int maxZz = (int)MathF.Floor(queryAABB.Max.Z * _invGridSize);

        // ゾーン数が多すぎる場合は愚直検索にフォールバック
        int zoneCount = (maxZx - minZx + 1) * (maxZy - minZy + 1) * (maxZz - minZz + 1);
        if (zoneCount > MaxZonesToQuery)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        Span<int> tempBuffer = stackalloc int[256];
        int count = 0;

        for (int zx = minZx; zx <= maxZx; zx++)
        {
            for (int zy = minZy; zy <= maxZy; zy++)
            {
                for (int zz = minZz; zz <= maxZz; zz++)
                {
                    long coord = PackZoneCoord(zx, zy, zz);
                    if (!_zones.TryGetValue(coord, out var zone))
                        continue;

                    int zoneResultCount = zone.QueryOverlap(minPrimary, maxPrimary, minSecondary, maxSecondary, tempBuffer);

                    for (int i = 0; i < zoneResultCount && count < candidates.Length; i++)
                    {
                        int shapeIndex = tempBuffer[i];

                        // O(1)重複チェック
                        if (shapeIndex < _queryMarker.Length && _queryMarker[shapeIndex] == _currentQueryId)
                            continue;
                        if (shapeIndex < _queryMarker.Length)
                            _queryMarker[shapeIndex] = _currentQueryId;

                        // 3軸 AABB オーバーラップ確認
                        if (allAABBs[shapeIndex].Intersects(queryAABB))
                        {
                            candidates[count++] = shapeIndex;
                        }
                    }
                }
            }
        }

        // 大オブジェクトリストもチェック
        foreach (int shapeIndex in _largeObjects)
        {
            if (count >= candidates.Length)
                break;

            if (shapeIndex < _queryMarker.Length && _queryMarker[shapeIndex] == _currentQueryId)
                continue;
            if (shapeIndex < _queryMarker.Length)
                _queryMarker[shapeIndex] = _currentQueryId;

            if (allAABBs[shapeIndex].Intersects(queryAABB))
            {
                candidates[count++] = shapeIndex;
            }
        }

        return count;
    }

    /// <summary>
    /// 愚直検索（Shape数が少ない場合）。
    /// </summary>
    private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        int count = 0;

        foreach (var shapeIndex in _shapeToZones.Keys)
        {
            if (count >= candidates.Length)
                break;

            if (allAABBs[shapeIndex].Intersects(queryAABB))
            {
                candidates[count++] = shapeIndex;
            }
        }

        // 大オブジェクトもチェック
        foreach (int shapeIndex in _largeObjects)
        {
            if (count >= candidates.Length)
                break;

            if (allAABBs[shapeIndex].Intersects(queryAABB))
            {
                candidates[count++] = shapeIndex;
            }
        }

        return count;
    }

    /// <summary>
    /// 全ゾーンを列挙する。
    /// </summary>
    public IEnumerable<Zone> GetAllZones() => _zones.Values;

    /// <summary>
    /// クリアする。
    /// </summary>
    public void Clear()
    {
        _zones.Clear();
        _shapeToZones.Clear();
        _largeObjects.Clear();
    }

    private int CountTotalShapes()
    {
        return _shapeToZones.Count + _largeObjects.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GetAxisValues(in AABB aabb, out float minPrimary, out float maxPrimary, out float minSecondary, out float maxSecondary)
    {
        switch (_axisMode)
        {
            case SAPAxisMode.Z:
                minPrimary = aabb.Min.Z;
                maxPrimary = aabb.Max.Z;
                minSecondary = 0f;
                maxSecondary = 0f;
                break;
            case SAPAxisMode.XZ:
                minPrimary = aabb.Min.X;
                maxPrimary = aabb.Max.X;
                minSecondary = aabb.Min.Z;
                maxSecondary = aabb.Max.Z;
                break;
            default: // SAPAxisMode.X
                minPrimary = aabb.Min.X;
                maxPrimary = aabb.Max.X;
                minSecondary = 0f;
                maxSecondary = 0f;
                break;
        }
    }

    private List<long> GetZoneCoordinates(in AABB aabb)
    {
        int minZx = (int)MathF.Floor(aabb.Min.X * _invGridSize);
        int minZy = (int)MathF.Floor(aabb.Min.Y * _invGridSize);
        int minZz = (int)MathF.Floor(aabb.Min.Z * _invGridSize);
        int maxZx = (int)MathF.Floor(aabb.Max.X * _invGridSize);
        int maxZy = (int)MathF.Floor(aabb.Max.Y * _invGridSize);
        int maxZz = (int)MathF.Floor(aabb.Max.Z * _invGridSize);

        var coords = new List<long>((maxZx - minZx + 1) * (maxZy - minZy + 1) * (maxZz - minZz + 1));

        for (int zx = minZx; zx <= maxZx; zx++)
        {
            for (int zy = minZy; zy <= maxZy; zy++)
            {
                for (int zz = minZz; zz <= maxZz; zz++)
                {
                    coords.Add(PackZoneCoord(zx, zy, zz));
                }
            }
        }

        return coords;
    }

    private Zone GetOrCreateZone(long coord)
    {
        if (_zones.TryGetValue(coord, out var zone))
            return zone;

        UnpackZoneCoord(coord, out int zx, out int zy, out int zz);
        var bounds = new AABB(
            new Vector3(zx * _gridSize, zy * _gridSize, zz * _gridSize),
            new Vector3((zx + 1) * _gridSize, (zy + 1) * _gridSize, (zz + 1) * _gridSize)
        );

        zone = new Zone(bounds, _axisMode);
        _zones[coord] = zone;
        return zone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackZoneCoord(int x, int y, int z)
    {
        // 各座標を21ビットにパック
        return ((long)(x + 0x100000) << 42) | ((long)(y + 0x100000) << 21) | (long)(z + 0x100000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UnpackZoneCoord(long coord, out int x, out int y, out int z)
    {
        z = (int)(coord & 0x1FFFFF) - 0x100000;
        y = (int)((coord >> 21) & 0x1FFFFF) - 0x100000;
        x = (int)((coord >> 42) & 0x1FFFFF) - 0x100000;
    }

    private static bool SameCoordinates(List<long> a, List<long> b)
    {
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
