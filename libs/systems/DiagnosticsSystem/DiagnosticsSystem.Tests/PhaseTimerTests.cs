using System;
using System.Threading;
using Xunit;

namespace Tomato.DiagnosticsSystem.Tests;

/// <summary>
/// PhaseTimer テスト
/// </summary>
public class PhaseTimerTests
{
    [Fact]
    public void Constructor_ShouldSetPhaseName()
    {
        var timer = new PhaseTimer("TestPhase");
        Assert.Equal("TestPhase", timer.PhaseName);
    }

    [Fact]
    public void Start_ShouldReturnDisposable()
    {
        var timer = new PhaseTimer("TestPhase");
        using var scope = timer.Start();
        Assert.NotNull(scope);
    }

    [Fact]
    public void GetAndReset_ShouldReturnAccumulatedTime()
    {
        var timer = new PhaseTimer("TestPhase");

        using (timer.Start())
        {
            Thread.Sleep(10);
        }

        var timing = timer.GetAndReset();
        Assert.Equal("TestPhase", timing.PhaseName);
        Assert.True(timing.ElapsedMs >= 5, $"Expected >= 5ms but got {timing.ElapsedMs}ms");
    }

    [Fact]
    public void GetAndReset_ShouldResetAccumulated()
    {
        var timer = new PhaseTimer("TestPhase");

        using (timer.Start())
        {
            Thread.Sleep(10);
        }

        timer.GetAndReset();
        var timing = timer.GetAndReset();

        Assert.Equal(0, timing.ElapsedMs);
    }

    [Fact]
    public void MultipleStarts_ShouldAccumulate()
    {
        var timer = new PhaseTimer("TestPhase");

        using (timer.Start())
        {
            Thread.Sleep(5);
        }

        using (timer.Start())
        {
            Thread.Sleep(5);
        }

        var timing = timer.GetAndReset();
        Assert.True(timing.ElapsedMs >= 5, $"Expected >= 5ms but got {timing.ElapsedMs}ms");
    }

    [Fact]
    public void AccumulatedMs_ShouldReturnCurrentValue()
    {
        var timer = new PhaseTimer("TestPhase");

        using (timer.Start())
        {
            Thread.Sleep(10);
        }

        Assert.True(timer.AccumulatedMs >= 5);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var timer = new PhaseTimer("TestPhase");
        var scope = timer.Start();

        Thread.Sleep(5);

        scope.Dispose();
        var after1 = timer.AccumulatedMs;

        scope.Dispose(); // 2回目のDispose
        var after2 = timer.AccumulatedMs;

        Assert.Equal(after1, after2);
    }
}
