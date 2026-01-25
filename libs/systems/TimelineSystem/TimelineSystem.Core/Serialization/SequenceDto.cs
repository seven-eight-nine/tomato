using System.Collections.Generic;

namespace Tomato.TimelineSystem;

public record SequenceDto
{
    public LoopSettingsDto? Loop { get; init; }
    public List<TrackDto> Tracks { get; init; } = new();
}
