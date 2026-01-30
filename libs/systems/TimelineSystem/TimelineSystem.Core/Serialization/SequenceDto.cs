using System.Collections.Generic;

namespace Tomato.TimelineSystem;

public class SequenceDto
{
    public LoopSettingsDto? Loop { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}
