using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション状態管理用のコンテキスト。
/// HierarchicalStateMachineのTContextとして使用される。
/// </summary>
public sealed class MotionContext
{
    /// <summary>
    /// 現在のモーション状態。
    /// </summary>
    public MotionState? CurrentMotionState { get; set; }

    /// <summary>
    /// 現在のモーションの経過tick数。
    /// </summary>
    public int ElapsedTicks { get; set; }

    /// <summary>
    /// TimelineSystemのクエリ用コンテキスト。
    /// </summary>
    public QueryContext QueryContext { get; }

    /// <summary>
    /// モーション実行時のコールバック。
    /// </summary>
    public IMotionExecutor? Executor { get; set; }

    public MotionContext()
    {
        QueryContext = new QueryContext();
    }

    /// <summary>
    /// tickをリセットする。
    /// </summary>
    public void ResetTicks()
    {
        ElapsedTicks = 0;
    }

    /// <summary>
    /// tickを進める。
    /// </summary>
    public void AdvanceTicks(int deltaTicks)
    {
        ElapsedTicks += deltaTicks;
    }
}
