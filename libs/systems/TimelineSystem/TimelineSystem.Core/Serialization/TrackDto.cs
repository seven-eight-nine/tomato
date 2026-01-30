using System.Collections.Generic;

namespace Tomato.TimelineSystem;

public class TrackDto
{
    public int Id { get; set; }
    public List<ClipDto> Clips { get; set; } = new List<ClipDto>();
}
