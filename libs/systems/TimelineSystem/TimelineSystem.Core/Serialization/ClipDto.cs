namespace Tomato.TimelineSystem;

public record ClipDto
{
    public int Id { get; init; }
    public int TrackId { get; init; }
    public ClipType Type { get; init; }
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }
}
