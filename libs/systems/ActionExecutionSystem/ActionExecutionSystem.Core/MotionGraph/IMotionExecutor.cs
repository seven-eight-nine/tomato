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
    /// モーション更新時に呼ばれる。
    /// </summary>
    void OnMotionUpdate(string motionId, int elapsedFrames, float deltaTime);

    /// <summary>
    /// モーション終了時に呼ばれる。
    /// </summary>
    void OnMotionEnd(string motionId);
}
