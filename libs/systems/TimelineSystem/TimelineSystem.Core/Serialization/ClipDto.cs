namespace Tomato.TimelineSystem;

public class ClipDto
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public ClipType Type { get; set; }
    public int StartFrame { get; set; }
    public int EndFrame { get; set; }
}
