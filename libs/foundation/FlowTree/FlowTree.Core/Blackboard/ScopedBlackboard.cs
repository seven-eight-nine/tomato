using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.FlowTree;

/// <summary>
/// スコープ付きBlackboard（サブツリー用）。
/// ローカル変更は親Blackboardに影響しない。
/// </summary>
public sealed class ScopedBlackboard
{
    private readonly Blackboard _parent;

    // ローカルオーバーライド
    private readonly Dictionary<int, int>? _localIntValues;
    private readonly Dictionary<int, float>? _localFloatValues;
    private readonly Dictionary<int, bool>? _localBoolValues;

    /// <summary>
    /// 親Blackboard。
    /// </summary>
    public Blackboard Parent => _parent;

    /// <summary>
    /// ScopedBlackboardを作成する。
    /// </summary>
    /// <param name="parent">親Blackboard</param>
    /// <param name="enableLocalStorage">ローカルストレージを有効にする</param>
    public ScopedBlackboard(Blackboard parent, bool enableLocalStorage = true)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));

        if (enableLocalStorage)
        {
            _localIntValues = new Dictionary<int, int>(8);
            _localFloatValues = new Dictionary<int, float>(8);
            _localBoolValues = new Dictionary<int, bool>(8);
        }
    }

    // =====================================================
    // Int
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetInt(BlackboardKey<int> key, out int value)
    {
        // ローカル優先
        if (_localIntValues?.TryGetValue(key.Id, out value) == true)
            return true;
        return _parent.TryGetInt(key, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt(BlackboardKey<int> key, int defaultValue = 0)
    {
        if (_localIntValues?.TryGetValue(key.Id, out var value) == true)
            return value;
        return _parent.GetInt(key, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIntLocal(BlackboardKey<int> key, int value)
    {
        if (_localIntValues != null)
            _localIntValues[key.Id] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIntGlobal(BlackboardKey<int> key, int value)
    {
        _parent.SetInt(key, value);
    }

    // =====================================================
    // Float
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetFloat(BlackboardKey<float> key, out float value)
    {
        if (_localFloatValues?.TryGetValue(key.Id, out value) == true)
            return true;
        return _parent.TryGetFloat(key, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(BlackboardKey<float> key, float defaultValue = 0f)
    {
        if (_localFloatValues?.TryGetValue(key.Id, out var value) == true)
            return value;
        return _parent.GetFloat(key, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloatLocal(BlackboardKey<float> key, float value)
    {
        if (_localFloatValues != null)
            _localFloatValues[key.Id] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloatGlobal(BlackboardKey<float> key, float value)
    {
        _parent.SetFloat(key, value);
    }

    // =====================================================
    // Bool
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBool(BlackboardKey<bool> key, out bool value)
    {
        if (_localBoolValues?.TryGetValue(key.Id, out value) == true)
            return true;
        return _parent.TryGetBool(key, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBool(BlackboardKey<bool> key, bool defaultValue = false)
    {
        if (_localBoolValues?.TryGetValue(key.Id, out var value) == true)
            return value;
        return _parent.GetBool(key, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBoolLocal(BlackboardKey<bool> key, bool value)
    {
        if (_localBoolValues != null)
            _localBoolValues[key.Id] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBoolGlobal(BlackboardKey<bool> key, bool value)
    {
        _parent.SetBool(key, value);
    }

    // =====================================================
    // Utility
    // =====================================================

    /// <summary>
    /// ローカルストレージをクリアする。
    /// </summary>
    public void ClearLocal()
    {
        _localIntValues?.Clear();
        _localFloatValues?.Clear();
        _localBoolValues?.Clear();
    }
}
