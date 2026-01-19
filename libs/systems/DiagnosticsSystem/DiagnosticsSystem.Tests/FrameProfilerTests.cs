using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Tomato.DiagnosticsSystem.Tests;

/// <summary>
/// FrameProfiler テスト
/// </summary>
public class FrameProfilerTests
{
    [Fact]
    public void Constructor_ShouldSetHistorySize()
    {
        var profiler = new FrameProfiler(100);
        Assert.Equal(100, profiler.HistorySize);
    }

    [Fact]
    public void Constructor_DefaultHistorySize_ShouldBe300()
    {
        var profiler = new FrameProfiler();
        Assert.Equal(300, profiler.HistorySize);
    }

    [Fact]
    public void Measure_ShouldReturnDisposable()
    {
        var profiler = new FrameProfiler();
        using var scope = profiler.Measure("TestPhase");
        Assert.NotNull(scope);
    }

    [Fact]
    public void EndFrame_ShouldRecordFrameReport()
    {
        var profiler = new FrameProfiler();

        using (profiler.Measure("Phase1"))
        {
            Thread.Sleep(1);
        }

        profiler.EndFrame(1);

        Assert.Equal(1, profiler.RecordedFrameCount);
    }

    [Fact]
    public void GetLatestReport_ShouldReturnLastFrame()
    {
        var profiler = new FrameProfiler();

        using (profiler.Measure("Phase1"))
        {
            Thread.Sleep(1);
        }

        profiler.EndFrame(42);

        var report = profiler.GetLatestReport();
        Assert.NotNull(report);
        Assert.Equal(42, report!.FrameNumber);
    }

    [Fact]
    public void GetLatestReport_WhenEmpty_ShouldReturnNull()
    {
        var profiler = new FrameProfiler();
        Assert.Null(profiler.GetLatestReport());
    }

    [Fact]
    public void GenerateReport_ShouldCalculateAverages()
    {
        var profiler = new FrameProfiler();

        for (int i = 0; i < 10; i++)
        {
            using (profiler.Measure("Phase1"))
            {
                Thread.Sleep(1);
            }
            profiler.EndFrame(i);
        }

        var report = profiler.GenerateReport();

        Assert.Equal(10, report.FrameCount);
        Assert.True(report.AverageFrameTimeMs > 0);
        Assert.True(report.MaxFrameTimeMs >= report.AverageFrameTimeMs);
        Assert.True(report.MinFrameTimeMs <= report.AverageFrameTimeMs);
    }

    [Fact]
    public void GenerateReport_WhenEmpty_ShouldReturnZeros()
    {
        var profiler = new FrameProfiler();
        var report = profiler.GenerateReport();

        Assert.Equal(0, report.FrameCount);
        Assert.Equal(0, report.AverageFrameTimeMs);
    }

    [Fact]
    public void MultiplePhases_ShouldBeTrackedSeparately()
    {
        var profiler = new FrameProfiler();

        using (profiler.Measure("Phase1"))
        {
            Thread.Sleep(5);
        }

        using (profiler.Measure("Phase2"))
        {
            Thread.Sleep(5);
        }

        profiler.EndFrame(1);

        var report = profiler.GetLatestReport();
        Assert.NotNull(report);
        Assert.Equal(2, report!.PhaseTimings.Count);
        Assert.Contains(report.PhaseTimings, t => t.PhaseName == "Phase1");
        Assert.Contains(report.PhaseTimings, t => t.PhaseName == "Phase2");
    }

    [Fact]
    public void PhaseNames_ShouldReturnRegisteredPhases()
    {
        var profiler = new FrameProfiler();

        using (profiler.Measure("A")) { }
        using (profiler.Measure("B")) { }
        using (profiler.Measure("C")) { }

        Assert.Equal(3, profiler.PhaseNames.Count);
        Assert.Equal("A", profiler.PhaseNames[0]);
        Assert.Equal("B", profiler.PhaseNames[1]);
        Assert.Equal("C", profiler.PhaseNames[2]);
    }

    [Fact]
    public void History_ShouldRespectCapacity()
    {
        var profiler = new FrameProfiler(5);

        for (int i = 0; i < 10; i++)
        {
            using (profiler.Measure("Phase1")) { }
            profiler.EndFrame(i);
        }

        Assert.Equal(5, profiler.RecordedFrameCount);

        var report = profiler.GenerateReport();
        Assert.Equal(5, report.FrameCount);
    }

    [Fact]
    public void Clear_ShouldResetHistory()
    {
        var profiler = new FrameProfiler();

        for (int i = 0; i < 5; i++)
        {
            using (profiler.Measure("Phase1")) { }
            profiler.EndFrame(i);
        }

        profiler.Clear();

        Assert.Equal(0, profiler.RecordedFrameCount);
        Assert.Null(profiler.GetLatestReport());
    }

    [Fact]
    public void SamePhase_MultipleMeasures_ShouldAccumulate()
    {
        var profiler = new FrameProfiler();

        using (profiler.Measure("Phase1"))
        {
            Thread.Sleep(5);
        }

        using (profiler.Measure("Phase1"))
        {
            Thread.Sleep(5);
        }

        profiler.EndFrame(1);

        var report = profiler.GetLatestReport();
        Assert.NotNull(report);
        Assert.Single(report!.PhaseTimings);
        Assert.True(report.PhaseTimings[0].ElapsedMs >= 5);
    }

    [Fact]
    public void GenerateReport_PhaseAverages_ShouldBeCalculated()
    {
        var profiler = new FrameProfiler();

        for (int i = 0; i < 5; i++)
        {
            using (profiler.Measure("FastPhase"))
            {
                Thread.Sleep(1);
            }
            using (profiler.Measure("SlowPhase"))
            {
                Thread.Sleep(5);
            }
            profiler.EndFrame(i);
        }

        var report = profiler.GenerateReport();

        Assert.True(report.PhaseAveragesMs.ContainsKey("FastPhase"));
        Assert.True(report.PhaseAveragesMs.ContainsKey("SlowPhase"));
        Assert.True(report.PhaseMaxMs.ContainsKey("FastPhase"));
        Assert.True(report.PhaseMaxMs.ContainsKey("SlowPhase"));
    }
}
