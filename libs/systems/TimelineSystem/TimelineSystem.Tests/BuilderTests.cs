using Xunit;

namespace Tomato.TimelineSystem.Tests;

public class BuilderTests
{
    [Fact]
    public void SequenceBuilder_CreatesSequence()
    {
        var sequence = new SequenceBuilder()
            .WithLoop(0, 100)
            .AddTrack<TestTrack>(track => track
                .AddClip(new TestRangeClip("anim", 0, 50))
            )
            .Build();

        Assert.True(sequence.IsLoop);
        Assert.Single(sequence.Tracks);
    }

    [Fact]
    public void SequenceBuilder_AllowsMultipleTracks()
    {
        var sequence = new SequenceBuilder()
            .AddTrack<TestTrack>(track => track
                .AddClip(new TestRangeClip("anim", 0, 50))
            )
            .AddTrack<TestTrack>(track => track
                .AddClip(new TestInstantClip("sound", 25))
            )
            .Build();

        Assert.Equal(2, sequence.Tracks.Count);
    }

    [Fact]
    public void DirectConstruction_Works()
    {
        var sequence = new Sequence();
        sequence.SetLoopSettings(LoopSettings.Create(0, 100));

        var track = sequence.CreateTrack<TestTrack>();
        track.AddClip(new TestRangeClip("anim", 0, 50));

        Assert.True(sequence.IsLoop);
        Assert.Single(sequence.Tracks);
    }

    [Fact]
    public void TrackRemoval_Works()
    {
        var sequence = new Sequence();

        var track1 = sequence.CreateTrack<TestTrack>();
        var track2 = sequence.CreateTrack<TestTrack>();

        Assert.Equal(2, sequence.Tracks.Count);

        var track1Id = track1.Id;
        sequence.RemoveTrack(track1Id);

        Assert.Single(sequence.Tracks);
        Assert.Equal(track2.Id, sequence.Tracks[0].Id);
    }

    [Fact]
    public void ClipRemoval_Works()
    {
        var sequence = new Sequence();
        var track = sequence.CreateTrack<TestTrack>();

        var clip1 = new TestRangeClip("anim1", 0, 50);
        var clip2 = new TestRangeClip("anim2", 30, 80);

        track.AddClip(clip1);
        track.AddClip(clip2);

        track.RemoveClip(clip1.Id);

        Assert.Null(track.GetClip(clip1.Id));
        Assert.NotNull(track.GetClip(clip2.Id));
    }

    [Fact]
    public void TrackId_AutoAssigned()
    {
        var sequence = new Sequence();

        var track1 = sequence.CreateTrack<TestTrack>();
        var track2 = sequence.CreateTrack<TestTrack>();

        Assert.False(track1.Id.IsUnassigned);
        Assert.False(track2.Id.IsUnassigned);
        Assert.NotEqual(track1.Id, track2.Id);
    }

    [Fact]
    public void TrackId_SequenceBuilder()
    {
        var sequence = new SequenceBuilder()
            .AddTrack<TestTrack>(track => track
                .AddClip(new TestRangeClip("anim1", 0, 50))
            )
            .AddTrack<TestTrack>(track => track
                .AddClip(new TestRangeClip("anim2", 0, 50))
            )
            .Build();

        Assert.Equal(2, sequence.Tracks.Count);
        Assert.False(sequence.Tracks[0].Id.IsUnassigned);
        Assert.False(sequence.Tracks[1].Id.IsUnassigned);
        Assert.NotEqual(sequence.Tracks[0].Id, sequence.Tracks[1].Id);
    }

    [Fact]
    public void TrackId_Sequential()
    {
        var sequence = new Sequence();

        var track0 = sequence.CreateTrack<TestTrack>();
        var track1 = sequence.CreateTrack<TestTrack>();
        var track2 = sequence.CreateTrack<TestTrack>();

        Assert.Equal(new TrackId(0), track0.Id);
        Assert.Equal(new TrackId(1), track1.Id);
        Assert.Equal(new TrackId(2), track2.Id);
    }
}
