using System;

namespace Tomato.TimelineSystem;

public ref struct QueryResult
{
    public int ResultFrame;
    public bool DidLoop;
    public int LoopCount;
    public ReadOnlySpan<ClipHit> Events;
    public ReadOnlySpan<OverlapInfo> Overlaps;
}
