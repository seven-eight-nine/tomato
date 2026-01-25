using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 空間分割ゾーン。SAP 配列を保持する。
/// </summary>
public sealed class Zone
{
    private const int InitialCapacity = 64;

    // SAP エントリ配列（主軸の MinPrimary でソート済み）
    private SAPEntry[] _entries;
    private int _count;

    // ShapeIndex → エントリインデックス のマッピング（高速削除用）
    private readonly Dictionary<int, int> _shapeToEntry;

    // ゾーンの境界
    public readonly AABB Bounds;

    // 軸モード
    private readonly SAPAxisMode _axisMode;

    public Zone(AABB bounds, SAPAxisMode axisMode = SAPAxisMode.X)
    {
        Bounds = bounds;
        _axisMode = axisMode;
        _entries = new SAPEntry[InitialCapacity];
        _count = 0;
        _shapeToEntry = new Dictionary<int, int>();
    }

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// SAP エントリの読み取り専用ビュー。
    /// </summary>
    public ReadOnlySpan<SAPEntry> Entries => _entries.AsSpan(0, _count);

    /// <summary>
    /// Shape を追加する。
    /// </summary>
    public void Add(int shapeIndex, float minPrimary, float maxPrimary)
    {
        Add(shapeIndex, minPrimary, maxPrimary, 0f, 0f);
    }

    /// <summary>
    /// Shape を追加する（副軸あり）。
    /// </summary>
    public void Add(int shapeIndex, float minPrimary, float maxPrimary, float minSecondary, float maxSecondary)
    {
        if (_shapeToEntry.ContainsKey(shapeIndex))
            return;

        EnsureCapacity(_count + 1);

        // 挿入位置を二分探索で見つける
        int insertIndex = FindInsertPosition(minPrimary);

        // 要素をシフト
        if (insertIndex < _count)
        {
            Array.Copy(_entries, insertIndex, _entries, insertIndex + 1, _count - insertIndex);

            // マッピングを更新
            for (int i = insertIndex + 1; i <= _count; i++)
            {
                _shapeToEntry[_entries[i].ShapeIndex] = i;
            }
        }

        _entries[insertIndex] = new SAPEntry(minPrimary, maxPrimary, minSecondary, maxSecondary, shapeIndex);
        _shapeToEntry[shapeIndex] = insertIndex;
        _count++;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        if (!_shapeToEntry.TryGetValue(shapeIndex, out int entryIndex))
            return false;

        // 要素をシフト
        if (entryIndex < _count - 1)
        {
            Array.Copy(_entries, entryIndex + 1, _entries, entryIndex, _count - entryIndex - 1);

            // マッピングを更新
            for (int i = entryIndex; i < _count - 1; i++)
            {
                _shapeToEntry[_entries[i].ShapeIndex] = i;
            }
        }

        _shapeToEntry.Remove(shapeIndex);
        _count--;

        return true;
    }

    /// <summary>
    /// Shape の範囲を更新する。
    /// </summary>
    public void Update(int shapeIndex, float newMinPrimary, float newMaxPrimary)
    {
        Update(shapeIndex, newMinPrimary, newMaxPrimary, 0f, 0f);
    }

    /// <summary>
    /// Shape の範囲を更新する（副軸あり）。
    /// </summary>
    public void Update(int shapeIndex, float newMinPrimary, float newMaxPrimary, float newMinSecondary, float newMaxSecondary)
    {
        if (!_shapeToEntry.TryGetValue(shapeIndex, out int entryIndex))
            return;

        ref var entry = ref _entries[entryIndex];
        float oldMinPrimary = entry.MinPrimary;
        entry.MinPrimary = newMinPrimary;
        entry.MaxPrimary = newMaxPrimary;
        entry.MinSecondary = newMinSecondary;
        entry.MaxSecondary = newMaxSecondary;

        // 差分ソート（Insertion Sort）
        if (newMinPrimary < oldMinPrimary)
        {
            // 左に移動
            while (entryIndex > 0 && _entries[entryIndex - 1].MinPrimary > newMinPrimary)
            {
                // スワップ
                var temp = _entries[entryIndex - 1];
                _entries[entryIndex - 1] = _entries[entryIndex];
                _entries[entryIndex] = temp;

                _shapeToEntry[_entries[entryIndex].ShapeIndex] = entryIndex;
                _shapeToEntry[_entries[entryIndex - 1].ShapeIndex] = entryIndex - 1;

                entryIndex--;
            }
        }
        else if (newMinPrimary > oldMinPrimary)
        {
            // 右に移動
            while (entryIndex < _count - 1 && _entries[entryIndex + 1].MinPrimary < newMinPrimary)
            {
                var temp = _entries[entryIndex + 1];
                _entries[entryIndex + 1] = _entries[entryIndex];
                _entries[entryIndex] = temp;

                _shapeToEntry[_entries[entryIndex].ShapeIndex] = entryIndex;
                _shapeToEntry[_entries[entryIndex + 1].ShapeIndex] = entryIndex + 1;

                entryIndex++;
            }
        }
    }

    /// <summary>
    /// 指定した範囲と重なる候補を列挙する。
    /// </summary>
    public int QueryOverlap(float queryMinPrimary, float queryMaxPrimary, Span<int> candidates)
    {
        return QueryOverlap(queryMinPrimary, queryMaxPrimary, 0f, 0f, candidates);
    }

    /// <summary>
    /// 指定した範囲と重なる候補を列挙する（副軸あり）。
    /// </summary>
    public int QueryOverlap(float queryMinPrimary, float queryMaxPrimary, float queryMinSecondary, float queryMaxSecondary, Span<int> candidates)
    {
        int count = 0;

        // queryMinPrimary 以下の最初のエントリを二分探索
        int startIndex = FindStartIndex(queryMinPrimary);

        bool useSecondary = _axisMode == SAPAxisMode.XZ;

        for (int i = startIndex; i < _count && count < candidates.Length; i++)
        {
            ref readonly var entry = ref _entries[i];

            // MinPrimary が queryMaxPrimary を超えたら終了
            if (entry.MinPrimary > queryMaxPrimary)
                break;

            // 主軸範囲が重なるか確認
            if (entry.MaxPrimary >= queryMinPrimary)
            {
                // XZモードの場合は副軸も確認
                if (useSecondary)
                {
                    if (entry.MaxSecondary >= queryMinSecondary && entry.MinSecondary <= queryMaxSecondary)
                    {
                        candidates[count++] = entry.ShapeIndex;
                    }
                }
                else
                {
                    candidates[count++] = entry.ShapeIndex;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Shape が含まれているか確認する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int shapeIndex)
    {
        return _shapeToEntry.ContainsKey(shapeIndex);
    }

    /// <summary>
    /// 全エントリをクリアする。
    /// </summary>
    public void Clear()
    {
        _count = 0;
        _shapeToEntry.Clear();
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _entries.Length)
            return;

        int newCapacity = System.Math.Max(_entries.Length * 2, required);
        Array.Resize(ref _entries, newCapacity);
    }

    private int FindInsertPosition(float minPrimary)
    {
        int lo = 0;
        int hi = _count;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_entries[mid].MinPrimary < minPrimary)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private int FindStartIndex(float queryMinPrimary)
    {
        int lo = 0;
        int hi = _count;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            // MaxPrimary が queryMinPrimary より小さいエントリは完全にスキップ可能
            if (_entries[mid].MaxPrimary < queryMinPrimary)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}
