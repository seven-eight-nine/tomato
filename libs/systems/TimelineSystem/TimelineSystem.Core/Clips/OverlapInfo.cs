namespace Tomato.TimelineSystem;

public struct OverlapInfo
{
    public readonly Clip Clip;
    public readonly float Progress;
    public float BlendWeight;

    public OverlapInfo(Clip clip, float progress)
    {
        Clip = clip;
        Progress = progress;
        BlendWeight = 0f;
    }

    public OverlapInfo(Clip clip, float progress, float blendWeight)
    {
        Clip = clip;
        Progress = progress;
        BlendWeight = blendWeight;
    }
}
