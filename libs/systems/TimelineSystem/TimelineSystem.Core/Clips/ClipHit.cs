namespace Tomato.TimelineSystem;

public readonly struct ClipHit
{
    public readonly Clip Clip;
    public readonly ClipEventType EventType;
    public readonly int EventFrame;
    public readonly float Progress;

    public ClipHit(Clip clip, ClipEventType eventType, int eventFrame, float progress)
    {
        Clip = clip;
        EventType = eventType;
        EventFrame = eventFrame;
        Progress = progress;
    }
}
