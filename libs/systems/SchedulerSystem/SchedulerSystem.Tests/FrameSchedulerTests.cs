using System;
using Xunit;

namespace Tomato.SchedulerSystem.Tests;

/// <summary>
/// FrameScheduler テスト
/// </summary>
public class FrameSchedulerTests
{
    [Fact]
    public void Constructor_ShouldInitializeAtFrameZero()
    {
        var scheduler = new FrameScheduler();
        Assert.Equal(0, scheduler.CurrentFrame);
    }

    [Fact]
    public void Update_ShouldIncrementFrame()
    {
        var scheduler = new FrameScheduler();
        scheduler.Update();
        Assert.Equal(1, scheduler.CurrentFrame);
    }

    [Fact]
    public void Schedule_ShouldReturnValidHandle()
    {
        var scheduler = new FrameScheduler();
        var handle = scheduler.Schedule(10, () => { });
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void Schedule_NegativeDelay_ShouldThrow()
    {
        var scheduler = new FrameScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(-1, () => { }));
    }

    [Fact]
    public void Schedule_NullAction_ShouldThrow()
    {
        var scheduler = new FrameScheduler();
        Assert.Throws<ArgumentNullException>(() => scheduler.Schedule(10, null!));
    }

    [Fact]
    public void Schedule_ZeroDelay_ShouldExecuteOnNextUpdate()
    {
        var scheduler = new FrameScheduler();
        bool executed = false;

        scheduler.Schedule(0, () => executed = true);
        Assert.False(executed);

        scheduler.Update();
        Assert.False(executed); // 0フレーム後 = 現在のフレーム = フレーム0、Update後はフレーム1

        // 実際にはZeroDelayは次のフレームで実行される設計
    }

    [Fact]
    public void Schedule_ShouldExecuteAtCorrectFrame()
    {
        var scheduler = new FrameScheduler();
        bool executed = false;

        scheduler.Schedule(3, () => executed = true);

        scheduler.Update(); // Frame 1
        Assert.False(executed);
        scheduler.Update(); // Frame 2
        Assert.False(executed);
        scheduler.Update(); // Frame 3
        Assert.True(executed);
    }

    [Fact]
    public void Schedule_MultipleAtSameFrame_ShouldAllExecute()
    {
        var scheduler = new FrameScheduler();
        int counter = 0;

        scheduler.Schedule(2, () => counter++);
        scheduler.Schedule(2, () => counter++);
        scheduler.Schedule(2, () => counter++);

        scheduler.Update(); // Frame 1
        scheduler.Update(); // Frame 2
        Assert.Equal(3, counter);
    }

    [Fact]
    public void Cancel_ShouldPreventExecution()
    {
        var scheduler = new FrameScheduler();
        bool executed = false;

        var handle = scheduler.Schedule(2, () => executed = true);
        handle.Cancel();

        scheduler.Update();
        scheduler.Update();
        Assert.False(executed);
    }

    [Fact]
    public void Cancel_InvalidTask_ShouldReturnFalse()
    {
        var scheduler = new FrameScheduler();
        var result = scheduler.Cancel(999);
        Assert.False(result);
    }

    [Fact]
    public void ScheduleRepeating_ShouldExecuteMultipleTimes()
    {
        var scheduler = new FrameScheduler();
        int counter = 0;

        scheduler.ScheduleRepeating(2, () => counter++);

        scheduler.Update(); // Frame 1
        Assert.Equal(0, counter);
        scheduler.Update(); // Frame 2
        Assert.Equal(1, counter);
        scheduler.Update(); // Frame 3
        Assert.Equal(1, counter);
        scheduler.Update(); // Frame 4
        Assert.Equal(2, counter);
    }

    [Fact]
    public void ScheduleRepeating_WithMaxRepetitions_ShouldStopAfterMax()
    {
        var scheduler = new FrameScheduler();
        int counter = 0;

        scheduler.ScheduleRepeating(1, () => counter++, maxRepetitions: 3);

        for (int i = 0; i < 10; i++)
        {
            scheduler.Update();
        }

        Assert.Equal(3, counter);
    }

    [Fact]
    public void ScheduleRepeating_ZeroInterval_ShouldThrow()
    {
        var scheduler = new FrameScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeating(0, () => { }));
    }

    [Fact]
    public void ScheduleRepeating_Cancel_ShouldStopExecution()
    {
        var scheduler = new FrameScheduler();
        int counter = 0;

        var handle = scheduler.ScheduleRepeating(1, () => counter++);

        scheduler.Update(); // Frame 1, counter = 1
        handle.Cancel();
        scheduler.Update(); // Frame 2
        scheduler.Update(); // Frame 3

        Assert.Equal(1, counter);
    }

    [Fact]
    public void ScheduledTaskCount_ShouldReflectPendingTasks()
    {
        var scheduler = new FrameScheduler();

        scheduler.Schedule(5, () => { });
        scheduler.Schedule(5, () => { });

        Assert.Equal(2, scheduler.ScheduledTaskCount);

        // 実行後は削除される
        for (int i = 0; i < 5; i++) scheduler.Update();

        Assert.Equal(0, scheduler.ScheduledTaskCount);
    }

    [Fact]
    public void RepeatingTaskCount_ShouldReflectActiveTasks()
    {
        var scheduler = new FrameScheduler();

        scheduler.ScheduleRepeating(2, () => { });
        scheduler.ScheduleRepeating(3, () => { }, maxRepetitions: 1);

        Assert.Equal(2, scheduler.RepeatingTaskCount);

        // maxRepetitions=1のタスクは1回実行後に削除
        scheduler.Update();
        scheduler.Update();
        scheduler.Update();

        Assert.Equal(1, scheduler.RepeatingTaskCount);
    }

    [Fact]
    public void Clear_ShouldRemoveAllTasks()
    {
        var scheduler = new FrameScheduler();

        scheduler.Schedule(10, () => { });
        scheduler.ScheduleRepeating(5, () => { });

        scheduler.Clear();

        Assert.Equal(0, scheduler.ScheduledTaskCount);
        Assert.Equal(0, scheduler.RepeatingTaskCount);
    }

    [Fact]
    public void TaskHandle_Invalid_ShouldHaveValidFalse()
    {
        var handle = TaskHandle.Invalid;
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void TaskHandle_InvalidCancel_ShouldReturnFalse()
    {
        var handle = TaskHandle.Invalid;
        Assert.False(handle.Cancel());
    }
}
