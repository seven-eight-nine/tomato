using System;
using System.Collections.Generic;

namespace Tomato.SchedulerSystem;

/// <summary>
/// フレームベースのタスクスケジューラ。
/// </summary>
public sealed class FrameScheduler
{
    private readonly SortedDictionary<int, List<ScheduledTask>> _scheduled = new();
    private readonly List<RepeatingTask> _repeating = new();
    private int _currentFrame;
    private int _nextTaskId;

    /// <summary>現在のフレーム番号</summary>
    public int CurrentFrame => _currentFrame;

    /// <summary>スケジュールされたタスク数（一回実行）</summary>
    public int ScheduledTaskCount
    {
        get
        {
            int count = 0;
            foreach (var (_, list) in _scheduled)
            {
                foreach (var task in list)
                {
                    if (!task.IsCancelled) count++;
                }
            }
            return count;
        }
    }

    /// <summary>繰り返しタスク数</summary>
    public int RepeatingTaskCount
    {
        get
        {
            int count = 0;
            foreach (var task in _repeating)
            {
                if (!task.IsCancelled) count++;
            }
            return count;
        }
    }

    /// <summary>指定フレーム後に実行</summary>
    public TaskHandle Schedule(int delayFrames, Action action)
    {
        if (delayFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(delayFrames), "Delay must be non-negative");
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        int targetFrame = _currentFrame + delayFrames;
        int taskId = _nextTaskId++;
        var task = new ScheduledTask(taskId, action, targetFrame);

        if (!_scheduled.TryGetValue(targetFrame, out var list))
        {
            list = new List<ScheduledTask>();
            _scheduled[targetFrame] = list;
        }

        list.Add(task);
        return new TaskHandle(taskId, this);
    }

    /// <summary>指定フレーム間隔で繰り返し実行</summary>
    public TaskHandle ScheduleRepeating(int intervalFrames, Action action, int maxRepetitions = -1)
    {
        if (intervalFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalFrames), "Interval must be positive");
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        int taskId = _nextTaskId++;
        var task = new RepeatingTask(
            taskId,
            action,
            intervalFrames,
            _currentFrame + intervalFrames,
            maxRepetitions);

        _repeating.Add(task);
        return new TaskHandle(taskId, this);
    }

    /// <summary>タスクをキャンセル</summary>
    public bool Cancel(int taskId)
    {
        // 一回実行タスクから検索
        foreach (var (_, list) in _scheduled)
        {
            foreach (var task in list)
            {
                if (task.Id == taskId && !task.IsCancelled)
                {
                    task.IsCancelled = true;
                    return true;
                }
            }
        }

        // 繰り返しタスクから検索
        foreach (var task in _repeating)
        {
            if (task.Id == taskId && !task.IsCancelled)
            {
                task.IsCancelled = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>毎フレーム呼び出し</summary>
    public void Update()
    {
        _currentFrame++;

        // 一回実行タスク
        if (_scheduled.TryGetValue(_currentFrame, out var tasks))
        {
            foreach (var task in tasks)
            {
                if (!task.IsCancelled)
                {
                    task.Action();
                }
            }
            _scheduled.Remove(_currentFrame);
        }

        // 繰り返しタスク
        for (int i = _repeating.Count - 1; i >= 0; i--)
        {
            var task = _repeating[i];

            if (task.IsCancelled || task.IsExpired)
            {
                _repeating.RemoveAt(i);
                continue;
            }

            if (_currentFrame >= task.NextExecutionFrame)
            {
                task.Action();
                task.Repetitions++;

                if (task.IsExpired)
                {
                    _repeating.RemoveAt(i);
                }
                else
                {
                    task.NextExecutionFrame = _currentFrame + task.IntervalFrames;
                }
            }
        }
    }

    /// <summary>すべてのタスクをクリア</summary>
    public void Clear()
    {
        _scheduled.Clear();
        _repeating.Clear();
    }
}
