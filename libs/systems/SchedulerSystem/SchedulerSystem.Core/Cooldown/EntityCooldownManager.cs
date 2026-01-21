using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SchedulerSystem;

/// <summary>
/// Entity単位のクールダウン管理。
/// </summary>
public sealed class EntityCooldownManager
{
    private readonly Dictionary<AnyHandle, Dictionary<string, int>> _cooldowns = new();
    private int _currentFrame;

    /// <summary>現在のフレーム番号</summary>
    public int CurrentFrame => _currentFrame;

    /// <summary>クールダウンを開始</summary>
    public void StartCooldown(AnyHandle entity, string actionId, int durationFrames)
    {
        if (!_cooldowns.TryGetValue(entity, out var entityCooldowns))
        {
            entityCooldowns = new Dictionary<string, int>();
            _cooldowns[entity] = entityCooldowns;
        }

        entityCooldowns[actionId] = _currentFrame + durationFrames;
    }

    /// <summary>クールダウン中か確認</summary>
    public bool IsOnCooldown(AnyHandle entity, string actionId)
    {
        if (!_cooldowns.TryGetValue(entity, out var entityCooldowns))
        {
            return false;
        }

        return entityCooldowns.TryGetValue(actionId, out var endFrame) &&
               _currentFrame < endFrame;
    }

    /// <summary>残りフレーム数を取得</summary>
    public int GetRemainingFrames(AnyHandle entity, string actionId)
    {
        if (!_cooldowns.TryGetValue(entity, out var entityCooldowns))
        {
            return 0;
        }

        if (entityCooldowns.TryGetValue(actionId, out var endFrame))
        {
            int remaining = endFrame - _currentFrame;
            return remaining > 0 ? remaining : 0;
        }

        return 0;
    }

    /// <summary>指定アクションのクールダウンをリセット</summary>
    public void Reset(AnyHandle entity, string actionId)
    {
        if (_cooldowns.TryGetValue(entity, out var entityCooldowns))
        {
            entityCooldowns.Remove(actionId);
        }
    }

    /// <summary>Entity削除時にクリーンアップ</summary>
    public void OnEntityRemoved(AnyHandle entity)
    {
        _cooldowns.Remove(entity);
    }

    /// <summary>すべてのクールダウンをクリア</summary>
    public void Clear()
    {
        _cooldowns.Clear();
    }

    /// <summary>毎フレーム呼び出し</summary>
    public void Update()
    {
        _currentFrame++;
    }

    /// <summary>無効なEntityのクールダウンを削除</summary>
    public void CleanupInvalidEntities()
    {
        var invalidEntities = new List<AnyHandle>();

        foreach (var entity in _cooldowns.Keys)
        {
            if (!entity.IsValid)
            {
                invalidEntities.Add(entity);
            }
        }

        foreach (var entity in invalidEntities)
        {
            _cooldowns.Remove(entity);
        }
    }
}
