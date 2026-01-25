using System.Linq;
using Xunit;

namespace Tomato.TimelineSystem.Tests;

public class RangeClipTests
{
    [Fact]
    public void RangeClip_Enter_WhenStartFrameIsHit()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 10, 30));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 5, deltaFrames: 10, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(ClipEventType.Enter, ctx.Events[0].EventType);
        Assert.Equal(10, ctx.Events[0].EventFrame);
    }

    [Fact]
    public void RangeClip_Exit_WhenEndFrameIsHit()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 10, 30));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 25, deltaFrames: 10, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(ClipEventType.Exit, ctx.Events[0].EventType);
        Assert.Equal(30, ctx.Events[0].EventFrame);
    }

    [Fact]
    public void RangeClip_Active_WhenInsideClip()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 10, 30));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 15, deltaFrames: 5, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(ClipEventType.Active, ctx.Events[0].EventType);
        Assert.Equal(0.5f, ctx.Events[0].Progress, 0.01f);
    }

    [Fact]
    public void RangeClip_EnterAndExit_WhenPassingThrough()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 10, 20));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 5, deltaFrames: 20, ctx);

        Assert.Equal(2, ctx.Events.Length);
        Assert.Contains(ctx.Events.ToArray(), e => e.EventType == ClipEventType.Enter);
        Assert.Contains(ctx.Events.ToArray(), e => e.EventType == ClipEventType.Exit);
    }

    [Fact]
    public void RangeClip_Progress_IsCorrect()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 0, 100));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 25, deltaFrames: 0, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(0.25f, ctx.Events[0].Progress, 0.01f);
    }
}
