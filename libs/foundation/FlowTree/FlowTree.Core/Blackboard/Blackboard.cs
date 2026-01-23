using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.FlowTree;

/// <summary>
/// 型別ストレージによるBlackboard（ゼロGC）。
/// 事前確保により実行中のアロケーションを排除。
/// </summary>
public sealed class Blackboard
{
    private readonly Dictionary<int, int> _intValues;
    private readonly Dictionary<int, float> _floatValues;
    private readonly Dictionary<int, bool> _boolValues;
    private readonly Dictionary<int, double> _doubleValues;
    private readonly Dictionary<int, string> _stringValues;
    private readonly Dictionary<int, object> _objectValues;

    /// <summary>
    /// Blackboardを作成する。
    /// </summary>
    /// <param name="capacity">各型ごとの初期容量</param>
    public Blackboard(int capacity = 16)
    {
        _intValues = new Dictionary<int, int>(capacity);
        _floatValues = new Dictionary<int, float>(capacity);
        _boolValues = new Dictionary<int, bool>(capacity);
        _doubleValues = new Dictionary<int, double>(capacity);
        _stringValues = new Dictionary<int, string>(capacity);
        _objectValues = new Dictionary<int, object>(capacity);
    }

    // =====================================================
    // Int
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetInt(BlackboardKey<int> key, out int value)
        => _intValues.TryGetValue(key.Id, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt(BlackboardKey<int> key, int defaultValue = 0)
        => _intValues.TryGetValue(key.Id, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetInt(BlackboardKey<int> key, int value)
        => _intValues[key.Id] = value;

    // =====================================================
    // Float
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFloat(BlackboardKey<float> key, out float value)
        => _floatValues.TryGetValue(key.Id, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(BlackboardKey<float> key, float defaultValue = 0f)
        => _floatValues.TryGetValue(key.Id, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloat(BlackboardKey<float> key, float value)
        => _floatValues[key.Id] = value;

    // =====================================================
    // Bool
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBool(BlackboardKey<bool> key, out bool value)
        => _boolValues.TryGetValue(key.Id, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBool(BlackboardKey<bool> key, bool defaultValue = false)
        => _boolValues.TryGetValue(key.Id, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBool(BlackboardKey<bool> key, bool value)
        => _boolValues[key.Id] = value;

    // =====================================================
    // Double
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetDouble(BlackboardKey<double> key, out double value)
        => _doubleValues.TryGetValue(key.Id, out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble(BlackboardKey<double> key, double defaultValue = 0.0)
        => _doubleValues.TryGetValue(key.Id, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDouble(BlackboardKey<double> key, double value)
        => _doubleValues[key.Id] = value;

    // =====================================================
    // String
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetString(BlackboardKey<string> key, out string? value)
        => _stringValues.TryGetValue(key.Id, out value!);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetString(BlackboardKey<string> key, string? defaultValue = null)
        => _stringValues.TryGetValue(key.Id, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetString(BlackboardKey<string> key, string value)
        => _stringValues[key.Id] = value;

    // =====================================================
    // Object (汎用)
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetObject<T>(BlackboardKey<T> key, out T? value) where T : class
    {
        if (_objectValues.TryGetValue(key.Id, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetObject<T>(BlackboardKey<T> key, T? defaultValue = null) where T : class
        => TryGetObject(key, out var value) ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetObject<T>(BlackboardKey<T> key, T value) where T : class
        => _objectValues[key.Id] = value;

    // =====================================================
    // Utility
    // =====================================================

    /// <summary>
    /// 指定したキーが存在するかを確認する。
    /// </summary>
    public bool Contains(IBlackboardKey key)
    {
        return _intValues.ContainsKey(key.Id) ||
               _floatValues.ContainsKey(key.Id) ||
               _boolValues.ContainsKey(key.Id) ||
               _doubleValues.ContainsKey(key.Id) ||
               _stringValues.ContainsKey(key.Id) ||
               _objectValues.ContainsKey(key.Id);
    }

    /// <summary>
    /// 指定したキーを削除する。
    /// </summary>
    public bool Remove(IBlackboardKey key)
    {
        return _intValues.Remove(key.Id) ||
               _floatValues.Remove(key.Id) ||
               _boolValues.Remove(key.Id) ||
               _doubleValues.Remove(key.Id) ||
               _stringValues.Remove(key.Id) ||
               _objectValues.Remove(key.Id);
    }

    /// <summary>
    /// 全ての値をクリアする。
    /// </summary>
    public void Clear()
    {
        _intValues.Clear();
        _floatValues.Clear();
        _boolValues.Clear();
        _doubleValues.Clear();
        _stringValues.Clear();
        _objectValues.Clear();
    }
}
