using System.Linq;
using Xunit;

namespace Tomato.TimelineSystem.Tests;

public class LoopTests
{
    [Fact]
    public void Loop_WrapsFrame_WhenExceedingEndFrame()
    {
        var sequence = new SequenceBuilder()
            .WithLoop(startFrame: 0, endFrame: 100)
            .Build();

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 90, deltaFrames: 20, ctx);

        Assert.True(ctx.DidLoop);
        Assert.Equal(1, ctx.LoopCount);
        Assert.Equal(10, ctx.ResultFrame);
    }

    [Fact]
    public void Loop_CountsMultipleLoops()
    {
        var sequence = new SequenceBuilder()
            .WithLoop(startFrame: 0, endFrame: 100)
            .Build();

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 90, deltaFrames: 220, ctx);

        Assert.True(ctx.DidLoop);
        Assert.Equal(3, ctx.LoopCount);
        Assert.Equal(10, ctx.ResultFrame);
    }

    [Fact]
    public void Loop_FiresEventsAtLoopBoundary()
    {
        var sequence = new Sequence();
        sequence.SetLoopSettings(LoopSettings.Create(0, 100));

        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestInstantClip("start", 0));
        track.AddClip(new TestInstantClip("end", 100));

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 95, deltaFrames: 10, ctx);

        Assert.True(ctx.DidLoop);
        Assert.Contains(ctx.Events.ToArray(), e => e.EventFrame == 100);
        Assert.Contains(ctx.Events.ToArray(), e => e.EventFrame == 0);
    }

    [Fact]
    public void NoLoop_DoesNotWrap()
    {
        var sequence = new Sequence();

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 90, deltaFrames: 20, ctx);

        Assert.False(ctx.DidLoop);
        Assert.Equal(0, ctx.LoopCount);
        Assert.Equal(110, ctx.ResultFrame);
    }

    [Fact]
    public void Loop_WithOffset_WrapsCorrectly()
    {
        var sequence = new SequenceBuilder()
            .WithLoop(startFrame: 50, endFrame: 150)
            .Build();

        var ctx = new QueryContext();
        sequence.Query(currentFrame: 140, deltaFrames: 30, ctx);

        Assert.True(ctx.DidLoop);
        Assert.Equal(70, ctx.ResultFrame);
    }
}
