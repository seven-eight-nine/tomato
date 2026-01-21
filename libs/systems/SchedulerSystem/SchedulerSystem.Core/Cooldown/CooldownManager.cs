using System.Collections.Generic;

namespace Tomato.SchedulerSystem;

/// <summary>
/// クールダウンを管理。
/// </summary>
public sealed class CooldownManager
{
    private readonly Dictionary<string, int> _cooldowns = new();
    private int _currentFrame;

    /// <summary>現在のフレーム番号</summary>
    public int CurrentFrame => _currentFrame;

    /// <summary>クールダウンを開始</summary>
    public void StartCooldown(string key, int durationFrames)
    {
        _cooldowns[key] = _currentFrame + durationFrames;
    }

    /// <summary>クールダウン中か確認</summary>
    public bool IsOnCooldown(string key)
    {
        return _cooldowns.TryGetValue(key, out var endFrame) && _currentFrame < endFrame;
    }

    /// <summary>残りフレーム数を取得</summary>
    public int GetRemainingFrames(string key)
    {
        if (_cooldowns.TryGetValue(key, out var endFrame))
        {
            int remaining = endFrame - _currentFrame;
            return remaining > 0 ? remaining : 0;
        }
        return 0;
    }

    /// <summary>クールダウンをリセット</summary>
    public void Reset(string key)
    {
        _cooldowns.Remove(key);
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

    /// <summary>期限切れのクールダウンを削除</summary>
    public void Cleanup()
    {
        var expired = new List<string>();

        foreach (var (key, endFrame) in _cooldowns)
        {
            if (_currentFrame >= endFrame)
            {
                expired.Add(key);
            }
        }

        foreach (var key in expired)
        {
            _cooldowns.Remove(key);
        }
    }
}
