namespace Tomato.TimelineSystem;

/// <summary>
/// クリップの基底クラス
/// </summary>
public abstract class Clip
{
    public ClipId Id { get; }
    public abstract ClipType Type { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }

    protected Clip(ClipId id, int startFrame, int endFrame)
    {
        Id = id;
        StartFrame = startFrame;
        EndFrame = endFrame;
    }
}

/// <summary>
/// 特定のトラック型に所属するクリップの基底クラス
/// </summary>
public abstract class Clip<TTrack> : Clip where TTrack : Track
{
    protected Clip(ClipId id, int startFrame, int endFrame)
        : base(id, startFrame, endFrame)
    {
    }
}
