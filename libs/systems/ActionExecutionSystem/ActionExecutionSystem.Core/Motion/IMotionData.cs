namespace Tomato.ActionExecutionSystem;

/// <summary>
/// アクションに紐づくモーションデータ。
/// </summary>
public interface IMotionData
{
    /// <summary>総時間（秒）。</summary>
    float Duration { get; }

    /// <summary>指定時刻のモーションを評価する。</summary>
    MotionFrame Evaluate(float time);
}
