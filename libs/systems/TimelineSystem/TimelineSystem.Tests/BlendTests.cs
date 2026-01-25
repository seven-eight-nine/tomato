using Xunit;

namespace Tomato.TimelineSystem.Tests;

public class BlendTests
{
    [Fact]
    public void Overlap_DetectedWithMultipleClips()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim1", 0, 40));
        track.AddClip(new TestRangeClip("anim2", 20, 60));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 30, deltaFrames: 0, ctx);

        Assert.Equal(2, ctx.Overlaps.Length);
    }

    [Fact]
    public void BlendWeight_CalculatedByProgress()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim1", 0, 40));
        track.AddClip(new TestRangeClip("anim2", 20, 60));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 30, deltaFrames: 0, ctx);

        float totalWeight = 0f;
        foreach (var overlap in ctx.Overlaps)
        {
            totalWeight += overlap.BlendWeight;
        }

        Assert.Equal(1.0f, totalWeight, 0.01f);
    }

    [Fact]
    public void BlendWeight_ProportionalToProgress()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim1", 0, 100));
        track.AddClip(new TestRangeClip("anim2", 50, 100));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 75, deltaFrames: 0, ctx);

        Assert.Equal(2, ctx.Overlaps.Length);

        var overlap1 = ctx.Overlaps[0];
        var overlap2 = ctx.Overlaps[1];

        Assert.Equal(0.75f, overlap1.Progress, 0.01f);
        Assert.Equal(0.5f, overlap2.Progress, 0.01f);

        Assert.True(overlap1.BlendWeight > overlap2.BlendWeight);
    }

    [Fact]
    public void SingleClip_HasFullWeight()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim1", 0, 100));
        track.AddClip(new TestRangeClip("anim2", 50, 100));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 25, deltaFrames: 0, ctx);

        Assert.Equal(0, ctx.Overlaps.Length);
    }

    [Fact]
    public void NoOverlap_WhenSeparateTracks()
    {
        var sequence = new Sequence();

        var track1 = sequence.CreateTrack<TestTrack>();
        track1.AddClip(new TestRangeClip("anim1", 0, 40));

        var track2 = sequence.CreateTrack<TestTrack>();
        track2.AddClip(new TestRangeClip("anim2", 20, 60));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 30, deltaFrames: 0, ctx);

        Assert.Equal(0, ctx.Overlaps.Length);
    }
}
