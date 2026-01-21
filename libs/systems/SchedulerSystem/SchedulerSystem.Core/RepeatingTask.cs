using System;

namespace Tomato.SchedulerSystem;

/// <summary>
/// 繰り返し実行のスケジュールタスク。
/// </summary>
internal sealed class RepeatingTask
{
    public int Id { get; }
    public Action Action { get; }
    public int IntervalFrames { get; }
    public int NextExecutionFrame { get; set; }
    public int MaxRepetitions { get; }
    public int Repetitions { get; set; }
    public bool IsCancelled { get; set; }

    public RepeatingTask(int id, Action action, int intervalFrames, int firstFrame, int maxRepetitions)
    {
        Id = id;
        Action = action;
        IntervalFrames = intervalFrames;
        NextExecutionFrame = firstFrame;
        MaxRepetitions = maxRepetitions;
        Repetitions = 0;
    }

    public bool IsExpired => MaxRepetitions > 0 && Repetitions >= MaxRepetitions;
}
