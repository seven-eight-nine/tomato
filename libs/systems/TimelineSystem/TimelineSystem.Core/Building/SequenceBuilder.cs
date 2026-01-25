using System;

namespace Tomato.TimelineSystem;

public sealed class SequenceBuilder
{
    private readonly Sequence _sequence = new();

    public SequenceBuilder WithLoop(int startFrame, int endFrame)
    {
        _sequence.SetLoopSettings(LoopSettings.Create(startFrame, endFrame));
        return this;
    }

    public SequenceBuilder WithBlendCalculator(IBlendCalculator calculator)
    {
        _sequence.SetBlendCalculator(calculator);
        return this;
    }

    public SequenceBuilder AddTrack<T>(Action<TrackConfigurator<T>> configure) where T : Track, new()
    {
        var track = new T();
        var configurator = new TrackConfigurator<T>(track);
        configure(configurator);
        _sequence.AddTrack(track);
        return this;
    }

    public SequenceBuilder AddTrack(Track track)
    {
        _sequence.AddTrack(track);
        return this;
    }

    public Sequence Build() => _sequence;
}
