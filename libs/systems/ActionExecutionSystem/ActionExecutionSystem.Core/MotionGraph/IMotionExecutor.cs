namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション実行時のコールバックインターフェース。
/// </summary>
public interface IMotionExecutor
{
    /// <summary>
    /// モーション開始時に呼ばれる。
    /// </summary>
    void OnMotionStart(string motionId);

    /// <summary>
    /// モーションtick時に呼ばれる。
    /// </summary>
    void OnMotionTick(string motionId, int elapsedTicks, int deltaTicks);

    /// <summary>
    /// モーション終了時に呼ばれる。
    /// </summary>
    void OnMotionEnd(string motionId);
}
