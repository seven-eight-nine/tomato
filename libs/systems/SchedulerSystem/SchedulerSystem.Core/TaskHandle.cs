namespace Tomato.SchedulerSystem;

/// <summary>
/// スケジュールされたタスクへのハンドル。
/// </summary>
public readonly struct TaskHandle
{
    private readonly int _taskId;
    private readonly FrameScheduler? _scheduler;

    internal TaskHandle(int taskId, FrameScheduler scheduler)
    {
        _taskId = taskId;
        _scheduler = scheduler;
    }

    /// <summary>タスクID</summary>
    public int TaskId => _taskId;

    /// <summary>タスクをキャンセル</summary>
    public bool Cancel()
    {
        return _scheduler?.Cancel(_taskId) ?? false;
    }

    /// <summary>有効なハンドルかどうか</summary>
    public bool IsValid => _scheduler != null;

    public static TaskHandle Invalid => default;
}
