using System.Collections.Generic;

namespace Tomato.TimelineSystem;

public record TrackDto
{
    public int Id { get; init; }
    public List<ClipDto> Clips { get; init; } = new();
}
