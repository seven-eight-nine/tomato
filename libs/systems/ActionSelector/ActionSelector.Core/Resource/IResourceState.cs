using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// リソース状態へのアクセスインターフェース。
///
/// HP、MP、スタミナ、ゲージなどの汎用リソースと、
/// スキルのクールダウンを管理する。
/// </summary>
/// <remarks>
/// 設計意図:
/// - 文字列IDによる柔軟なリソース定義
/// - 読み取り専用インターフェース（変更はゲーム側で行う）
/// </remarks>
public interface IResourceState
{
    /// <summary>
    /// リソースの現在値を取得する。
    /// </summary>
    /// <param name="resourceId">リソースID</param>
    /// <returns>現在値。リソースが存在しない場合は 0</returns>
    float GetValue(string resourceId);

    /// <summary>
    /// リソースの最大値を取得する。
    /// </summary>
    /// <param name="resourceId">リソースID</param>
    /// <returns>最大値。リソースが存在しない場合は 0</returns>
    float GetMaxValue(string resourceId);

    /// <summary>
    /// リソースの割合を取得する（0.0〜1.0）。
    /// </summary>
    /// <param name="resourceId">リソースID</param>
    /// <returns>現在値 / 最大値。最大値が0の場合は 0</returns>
    float GetRatio(string resourceId);

    /// <summary>
    /// 指定量のリソースがあるか判定する。
    /// </summary>
    /// <param name="resourceId">リソースID</param>
    /// <param name="amount">必要量</param>
    /// <returns>現在値 >= amount の場合 true</returns>
    bool HasAmount(string resourceId, float amount);

    /// <summary>
    /// クールダウンの残り時間を取得する（秒）。
    /// </summary>
    /// <param name="cooldownId">クールダウンID</param>
    /// <returns>残り時間。クールダウンが存在しない or 完了済みの場合は 0</returns>
    float GetCooldown(string cooldownId);

    /// <summary>
    /// クールダウンが完了しているか判定する。
    /// </summary>
    /// <param name="cooldownId">クールダウンID</param>
    /// <returns>残り時間 <= 0 の場合 true</returns>
    bool IsCooldownReady(string cooldownId);
}

/// <summary>
/// リソース状態の標準実装。
/// </summary>
/// <remarks>
/// テスト用およびシンプルなゲーム向け。
/// 本番では各ゲームのリソースシステムに合わせて実装する。
/// </remarks>
public sealed class SimpleResourceState : IResourceState
{
    // ===========================================
    // 内部構造
    // ===========================================

    private readonly struct ResourceValue
    {
        public readonly float Current;
        public readonly float Max;

        public ResourceValue(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    // ===========================================
    // フィールド
    // ===========================================

    private readonly Dictionary<string, ResourceValue> _resources = new();
    private readonly Dictionary<string, float> _cooldowns = new();

    // ===========================================
    // リソース操作
    // ===========================================

    /// <summary>
    /// リソースを設定する。
    /// </summary>
    public void SetResource(string resourceId, float current, float max)
    {
        _resources[resourceId] = new ResourceValue(current, max);
    }

    /// <summary>
    /// リソースの現在値を設定する。
    /// </summary>
    public void SetValue(string resourceId, float current)
    {
        if (_resources.TryGetValue(resourceId, out var existing))
        {
            _resources[resourceId] = new ResourceValue(current, existing.Max);
        }
        else
        {
            _resources[resourceId] = new ResourceValue(current, current);
        }
    }

    // ===========================================
    // クールダウン操作
    // ===========================================

    /// <summary>
    /// クールダウンを開始する。
    /// </summary>
    public void StartCooldown(string cooldownId, float duration)
    {
        _cooldowns[cooldownId] = duration;
    }

    /// <summary>
    /// クールダウンを更新する（毎フレーム呼ぶ）。
    /// </summary>
    public void Update(float deltaTime)
    {
        var keysToRemove = new List<string>();
        var keys = new List<string>(_cooldowns.Keys);

        foreach (var key in keys)
        {
            _cooldowns[key] -= deltaTime;
            if (_cooldowns[key] <= 0)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cooldowns.Remove(key);
        }
    }

    // ===========================================
    // IResourceState 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetValue(string resourceId)
    {
        return _resources.TryGetValue(resourceId, out var value) ? value.Current : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetMaxValue(string resourceId)
    {
        return _resources.TryGetValue(resourceId, out var value) ? value.Max : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetRatio(string resourceId)
    {
        if (!_resources.TryGetValue(resourceId, out var value))
            return 0f;
        return value.Max > 0 ? value.Current / value.Max : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAmount(string resourceId, float amount)
    {
        return _resources.TryGetValue(resourceId, out var value) && value.Current >= amount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetCooldown(string cooldownId)
    {
        return _cooldowns.TryGetValue(cooldownId, out var remaining) ? MathF.Max(0, remaining) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCooldownReady(string cooldownId)
    {
        return !_cooldowns.TryGetValue(cooldownId, out var remaining) || remaining <= 0;
    }
}

/// <summary>
/// 空のリソース状態。リソースがないゲーム用。
/// </summary>
public sealed class EmptyResourceState : IResourceState
{
    public static readonly EmptyResourceState Instance = new();
    private EmptyResourceState() { }

    public float GetValue(string resourceId) => 0f;
    public float GetMaxValue(string resourceId) => 0f;
    public float GetRatio(string resourceId) => 0f;
    public bool HasAmount(string resourceId, float amount) => false;
    public float GetCooldown(string cooldownId) => 0f;
    public bool IsCooldownReady(string cooldownId) => true;
}
