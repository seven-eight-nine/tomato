using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.TimelineSystem.Tests;

public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Query_1000Clips_100Tracks_Performance()
    {
        const int trackCount = 100;
        const int clipsPerTrack = 10;
        const int totalClips = trackCount * clipsPerTrack;
        const int iterations = 10000;

        var sequence = CreateLargeSequence(trackCount, clipsPerTrack);
        var ctx = new QueryContext(eventCapacity: 256, overlapCapacity: 64);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            sequence.Query(i * 10, 5, ctx);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            int frame = (i * 7) % 1000;
            sequence.Query(frame, 5, ctx);
        }
        sw.Stop();

        double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Total clips: {totalClips}");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average per query: {avgMicroseconds:F2} us");

        Assert.True(avgMicroseconds < 100, $"Query took {avgMicroseconds:F2} us, expected < 100 us");
    }

    [Fact]
    public void Query_DenseOverlaps_Performance()
    {
        const int clipCount = 50;
        const int iterations = 10000;

        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        for (int i = 0; i < clipCount; i++)
        {
            track.AddClip(new TestRangeClip($"clip{i}", i * 2, i * 2 + 100));
        }

        var ctx = new QueryContext(eventCapacity: 256, overlapCapacity: 128);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            sequence.Query(50, 1, ctx);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            sequence.Query(50, 1, ctx);
        }
        sw.Stop();

        double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Overlapping clips at frame 50: ~{clipCount}");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average per query: {avgMicroseconds:F2} us");

        Assert.True(avgMicroseconds < 50, $"Query took {avgMicroseconds:F2} us, expected < 50 us");
    }

    [Fact]
    public void Query_WithLoop_Performance()
    {
        const int iterations = 10000;

        var sequence = new Sequence();
        sequence.SetLoopSettings(LoopSettings.Create(0, 1000));

        var track = sequence.CreateTrack<TestTrack>();
        for (int i = 0; i < 100; i++)
        {
            track.AddClip(new TestInstantClip($"event{i}", i * 10));
        }

        var ctx = new QueryContext();

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            sequence.Query(990, 50, ctx);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            sequence.Query(990, 50, ctx);
        }
        sw.Stop();

        double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Loop crossing query");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average per query: {avgMicroseconds:F2} us");

        Assert.True(avgMicroseconds < 50, $"Query took {avgMicroseconds:F2} us, expected < 50 us");
    }

    [Fact]
    public void QueryContext_Reuse_NoAllocation()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        for (int i = 0; i < 100; i++)
        {
            track.AddClip(new TestRangeClip($"clip{i}", i * 10, i * 10 + 20));
        }

        var ctx = new QueryContext();

        // Warmup and prime buffers
        for (int i = 0; i < 100; i++)
        {
            sequence.Query(i * 5, 10, ctx);
        }

        long memoryBefore = GC.GetAllocatedBytesForCurrentThread();

        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            int frame = (i * 7) % 900;
            sequence.Query(frame, 10, ctx);
        }

        long memoryAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocatedBytes = memoryAfter - memoryBefore;

        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Allocated bytes: {allocatedBytes}");
        _output.WriteLine($"Bytes per query: {(double)allocatedBytes / iterations:F2}");

        Assert.True(allocatedBytes < 1000, $"Allocated {allocatedBytes} bytes, expected minimal allocation");
    }

    [Fact]
    public void BinarySearch_LargeClipList_Performance()
    {
        const int clipCount = 10000;
        const int iterations = 100000;

        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        var random = new Random(42);

        for (int i = 0; i < clipCount; i++)
        {
            int start = random.Next(0, 100000);
            track.AddClip(new TestRangeClip($"clip{i}", start, start + 100));
        }

        var ctx = new QueryContext(eventCapacity: 512, overlapCapacity: 128);

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            sequence.Query(random.Next(0, 100000), 10, ctx);
        }

        random = new Random(42);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            int frame = random.Next(0, 100000);
            sequence.Query(frame, 10, ctx);
        }
        sw.Stop();

        double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Total clips: {clipCount}");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average per query: {avgMicroseconds:F2} us");

        Assert.True(avgMicroseconds < 50, $"Query took {avgMicroseconds:F2} us, expected < 50 us");
    }

    [Fact]
    public void BlendCalculation_ManyOverlaps_Performance()
    {
        const int overlapCount = 100;
        const int iterations = 100000;

        var overlaps = new OverlapInfo[overlapCount];
        for (int i = 0; i < overlapCount; i++)
        {
            overlaps[i] = new OverlapInfo(
                new TestRangeClip($"clip{i}", 0, 100),
                (float)(i + 1) / overlapCount
            );
        }

        var blend = ProgressBasedBlend.Instance;

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            blend.CalculateWeights(overlaps.AsSpan());
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            blend.CalculateWeights(overlaps.AsSpan());
        }
        sw.Stop();

        double avgNanoseconds = (sw.Elapsed.TotalMilliseconds * 1_000_000) / iterations;
        _output.WriteLine($"Overlaps: {overlapCount}");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average per calculation: {avgNanoseconds:F0} ns");

        Assert.True(avgNanoseconds < 10000, $"Blend calculation took {avgNanoseconds:F0} ns, expected < 10000 ns");
    }

    private static Sequence CreateLargeSequence(int trackCount, int clipsPerTrack)
    {
        var sequence = new Sequence();

        for (int t = 0; t < trackCount; t++)
        {
            var track = sequence.CreateTrack<TestTrack>();
            for (int c = 0; c < clipsPerTrack; c++)
            {
                int start = c * 100 + t;
                track.AddClip(new TestRangeClip($"t{t}c{c}", start, start + 50));
            }
        }

        return sequence;
    }
}
