using Xunit;

namespace Tomato.TimelineSystem.Tests;

public class InstantClipTests
{
    [Fact]
    public void InstantClip_Fires_WhenFrameIsHit()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestInstantClip("sound", 10));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 8, deltaFrames: 5, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(ClipEventType.Fired, ctx.Events[0].EventType);
        Assert.Equal(10, ctx.Events[0].EventFrame);
    }

    [Fact]
    public void InstantClip_DoesNotFire_WhenFrameIsNotInRange()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestInstantClip("sound", 10));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 0, deltaFrames: 5, ctx);

        Assert.Equal(0, ctx.Events.Length);
    }

    [Fact]
    public void InstantClip_FiresAtExactFrame()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestInstantClip("sound", 10));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 10, deltaFrames: 0, ctx);

        Assert.Equal(1, ctx.Events.Length);
        Assert.Equal(ClipEventType.Fired, ctx.Events[0].EventType);
    }

    [Fact]
    public void MultipleInstantClips_Fire_InOrder()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestInstantClip("sound1", 15));
        track.AddClip(new TestInstantClip("sound2", 10));
        track.AddClip(new TestInstantClip("sound3", 20));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 5, deltaFrames: 20, ctx);

        Assert.Equal(3, ctx.Events.Length);
    }
}
