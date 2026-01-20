using System;

namespace Tomato.SchedulerSystem;

/// <summary>
/// 一回実行のスケジュールタスク。
/// </summary>
internal sealed class ScheduledTask
{
    public int Id { get; }
    public Action Action { get; }
    public int TargetFrame { get; }
    public bool IsCancelled { get; set; }

    public ScheduledTask(int id, Action action, int targetFrame)
    {
        Id = id;
        Action = action;
        TargetFrame = targetFrame;
    }
}
