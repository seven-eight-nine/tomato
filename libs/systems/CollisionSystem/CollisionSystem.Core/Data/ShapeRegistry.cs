using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Shape 管理レジストリ。SoA 形式でデータを保持。
/// </summary>
public sealed class ShapeRegistry
{
    private const int InitialCapacity = 256;
    private const float FatAABBMargin = 0.1f;

    // Shape メタデータ
    private int[] _generations;
    private ShapeType[] _types;
    private int[] _paramIndices;
    private bool[] _isStatic;
    private int[] _userData;

    // AABB（Fat AABB）
    private AABB[] _aabbs;

    // 実際の形状データを保持するインデックス
    // ShapeType ごとに別配列を持つ
    private readonly List<SphereData> _spheres = new();
    private readonly List<CapsuleData> _capsules = new();
    private readonly List<CylinderData> _cylinders = new();
    private readonly List<BoxData> _boxes = new();

    // フリーリスト（削除されたインデックスの再利用）
    private readonly Stack<int> _freeIndices = new();

    private int _capacity;
    private int _count;

    public ShapeRegistry()
    {
        _capacity = InitialCapacity;
        _generations = new int[_capacity];
        _types = new ShapeType[_capacity];
        _paramIndices = new int[_capacity];
        _isStatic = new bool[_capacity];
        _userData = new int[_capacity];
        _aabbs = new AABB[_capacity];

        Array.Fill(_generations, -1);
    }

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 現在のキャパシティ。
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// インデックスが有効なShapeを指しているか確認する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidIndex(int index)
    {
        if (index < 0 || index >= _capacity)
            return false;
        return _generations[index] >= 0;
    }

    #region Registration

    /// <summary>
    /// 球を登録する。
    /// </summary>
    public ShapeHandle AddSphere(in SphereData sphere, bool isStatic = false, int userData = 0)
    {
        int paramIndex = _spheres.Count;
        _spheres.Add(sphere);

        var aabb = ComputeSphereAABB(sphere);
        return AllocateShape(ShapeType.Sphere, paramIndex, aabb, isStatic, userData);
    }

    /// <summary>
    /// カプセルを登録する。
    /// </summary>
    public ShapeHandle AddCapsule(in CapsuleData capsule, bool isStatic = false, int userData = 0)
    {
        int paramIndex = _capsules.Count;
        _capsules.Add(capsule);

        var aabb = ComputeCapsuleAABB(capsule);
        return AllocateShape(ShapeType.Capsule, paramIndex, aabb, isStatic, userData);
    }

    /// <summary>
    /// 円柱を登録する。
    /// </summary>
    public ShapeHandle AddCylinder(in CylinderData cylinder, bool isStatic = false, int userData = 0)
    {
        int paramIndex = _cylinders.Count;
        _cylinders.Add(cylinder);

        var aabb = ComputeCylinderAABB(cylinder);
        return AllocateShape(ShapeType.Cylinder, paramIndex, aabb, isStatic, userData);
    }

    /// <summary>
    /// ボックスを登録する。
    /// </summary>
    public ShapeHandle AddBox(in BoxData box, bool isStatic = false, int userData = 0)
    {
        int paramIndex = _boxes.Count;
        _boxes.Add(box);

        var aabb = ComputeBoxAABB(box);
        return AllocateShape(ShapeType.Box, paramIndex, aabb, isStatic, userData);
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(ShapeHandle handle)
    {
        if (!IsValid(handle))
            return false;

        int index = handle.Index;

        // Generation をインクリメントして無効化
        _generations[index]++;
        _freeIndices.Push(index);
        _count--;

        return true;
    }

    #endregion

    #region Update

    /// <summary>
    /// 球の位置を更新する。
    /// </summary>
    /// <returns>AABB が変更された場合は true</returns>
    public bool UpdateSphere(ShapeHandle handle, in SphereData newData)
    {
        if (!IsValid(handle))
            return false;

        int index = handle.Index;
        if (_types[index] != ShapeType.Sphere)
            return false;

        int paramIndex = _paramIndices[index];
        _spheres[paramIndex] = newData;

        var newAABB = ComputeSphereAABB(newData);
        return UpdateAABBIfNeeded(index, newAABB);
    }

    /// <summary>
    /// カプセルの位置を更新する。
    /// </summary>
    public bool UpdateCapsule(ShapeHandle handle, in CapsuleData newData)
    {
        if (!IsValid(handle))
            return false;

        int index = handle.Index;
        if (_types[index] != ShapeType.Capsule)
            return false;

        int paramIndex = _paramIndices[index];
        _capsules[paramIndex] = newData;

        var newAABB = ComputeCapsuleAABB(newData);
        return UpdateAABBIfNeeded(index, newAABB);
    }

    /// <summary>
    /// 円柱の位置を更新する。
    /// </summary>
    public bool UpdateCylinder(ShapeHandle handle, in CylinderData newData)
    {
        if (!IsValid(handle))
            return false;

        int index = handle.Index;
        if (_types[index] != ShapeType.Cylinder)
            return false;

        int paramIndex = _paramIndices[index];
        _cylinders[paramIndex] = newData;

        var newAABB = ComputeCylinderAABB(newData);
        return UpdateAABBIfNeeded(index, newAABB);
    }

    /// <summary>
    /// ボックスの位置を更新する。
    /// </summary>
    public bool UpdateBox(ShapeHandle handle, in BoxData newData)
    {
        if (!IsValid(handle))
            return false;

        int index = handle.Index;
        if (_types[index] != ShapeType.Box)
            return false;

        int paramIndex = _paramIndices[index];
        _boxes[paramIndex] = newData;

        var newAABB = ComputeBoxAABB(newData);
        return UpdateAABBIfNeeded(index, newAABB);
    }

    #endregion

    #region Accessors

    /// <summary>
    /// ハンドルが有効か確認する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(ShapeHandle handle)
    {
        if (handle.Index < 0 || handle.Index >= _capacity)
            return false;
        return _generations[handle.Index] == handle.Generation;
    }

    /// <summary>
    /// インデックスから現在のハンドルを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeHandle GetHandle(int index)
    {
        if (index < 0 || index >= _capacity || _generations[index] < 0)
            return ShapeHandle.Invalid;
        return new ShapeHandle(index, _generations[index]);
    }

    /// <summary>
    /// Shape の種別を取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeType GetShapeType(int index) => _types[index];

    /// <summary>
    /// Shape が静的かどうか。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStatic(int index) => _isStatic[index];

    /// <summary>
    /// ユーザーデータを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUserData(int index) => _userData[index];

    /// <summary>
    /// AABB を取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AABB GetAABB(int index) => _aabbs[index];

    /// <summary>
    /// 球データを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly SphereData GetSphere(int index)
    {
        int paramIndex = _paramIndices[index];
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_spheres)[paramIndex];
    }

    /// <summary>
    /// カプセルデータを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly CapsuleData GetCapsule(int index)
    {
        int paramIndex = _paramIndices[index];
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_capsules)[paramIndex];
    }

    /// <summary>
    /// 円柱データを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly CylinderData GetCylinder(int index)
    {
        int paramIndex = _paramIndices[index];
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_cylinders)[paramIndex];
    }

    /// <summary>
    /// ボックスデータを取得する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BoxData GetBox(int index)
    {
        int paramIndex = _paramIndices[index];
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_boxes)[paramIndex];
    }

    /// <summary>
    /// 全 AABB の Span を取得する。
    /// </summary>
    public ReadOnlySpan<AABB> AABBs => _aabbs.AsSpan(0, _capacity);

    /// <summary>
    /// アクティブなインデックスを列挙する。
    /// </summary>
    public IEnumerable<int> GetActiveIndices()
    {
        for (int i = 0; i < _capacity; i++)
        {
            if (_generations[i] >= 0)
                yield return i;
        }
    }

    #endregion

    #region Private Methods

    private ShapeHandle AllocateShape(ShapeType type, int paramIndex, AABB aabb, bool isStatic, int userData)
    {
        int index;
        if (_freeIndices.Count > 0)
        {
            index = _freeIndices.Pop();
        }
        else
        {
            index = _count;
            EnsureCapacity(_count + 1);
        }

        int generation = _generations[index] < 0 ? 0 : _generations[index];
        _generations[index] = generation;
        _types[index] = type;
        _paramIndices[index] = paramIndex;
        _isStatic[index] = isStatic;
        _userData[index] = userData;
        _aabbs[index] = aabb.Expand(FatAABBMargin);

        _count++;
        return new ShapeHandle(index, generation);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _capacity)
            return;

        int newCapacity = System.Math.Max(_capacity * 2, required);
        Array.Resize(ref _generations, newCapacity);
        Array.Resize(ref _types, newCapacity);
        Array.Resize(ref _paramIndices, newCapacity);
        Array.Resize(ref _isStatic, newCapacity);
        Array.Resize(ref _userData, newCapacity);
        Array.Resize(ref _aabbs, newCapacity);

        for (int i = _capacity; i < newCapacity; i++)
        {
            _generations[i] = -1;
        }

        _capacity = newCapacity;
    }

    private bool UpdateAABBIfNeeded(int index, AABB exactAABB)
    {
        var currentFatAABB = _aabbs[index];

        // 実際の AABB が Fat AABB 内に収まっているか確認
        if (currentFatAABB.Contains(exactAABB.Min) && currentFatAABB.Contains(exactAABB.Max))
        {
            return false;
        }

        // Fat AABB を再計算
        _aabbs[index] = exactAABB.Expand(FatAABBMargin);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AABB ComputeSphereAABB(in SphereData sphere)
    {
        var r = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
        return new AABB(sphere.Center - r, sphere.Center + r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AABB ComputeCapsuleAABB(in CapsuleData capsule)
    {
        var min = Vector3.Min(capsule.Point1, capsule.Point2);
        var max = Vector3.Max(capsule.Point1, capsule.Point2);
        var r = new Vector3(capsule.Radius, capsule.Radius, capsule.Radius);
        return new AABB(min - r, max + r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AABB ComputeCylinderAABB(in CylinderData cylinder)
    {
        var r = new Vector3(cylinder.Radius, 0, cylinder.Radius);
        var min = cylinder.BaseCenter - r;
        var max = cylinder.BaseCenter + new Vector3(cylinder.Radius, cylinder.Height, cylinder.Radius);
        return new AABB(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AABB ComputeBoxAABB(in BoxData box)
    {
        // Y軸回転を考慮したAABB計算
        if (MathF.Abs(box.Yaw) < 1e-6f)
        {
            // 回転なし
            return new AABB(box.Center - box.HalfExtents, box.Center + box.HalfExtents);
        }

        var cos = MathF.Cos(box.Yaw);
        var sin = MathF.Sin(box.Yaw);
        var absCos = MathF.Abs(cos);
        var absSin = MathF.Abs(sin);

        // 回転後のAABB半サイズ（XZ平面で回転）
        var newHalfX = box.HalfExtents.X * absCos + box.HalfExtents.Z * absSin;
        var newHalfZ = box.HalfExtents.X * absSin + box.HalfExtents.Z * absCos;
        var rotatedHalf = new Vector3(newHalfX, box.HalfExtents.Y, newHalfZ);

        return new AABB(box.Center - rotatedHalf, box.Center + rotatedHalf);
    }

    #endregion
}
