using System;

namespace Tomato.TimelineSystem;

public sealed class QueryContext
{
    private ClipHit[] _eventBuffer;
    private OverlapInfo[] _overlapBuffer;
    private ClipHit[] _tempEventBuffer;
    private OverlapInfo[] _tempOverlapBuffer;

    private int _eventCount;
    private int _overlapCount;

    public int ResultFrame { get; internal set; }
    public bool DidLoop { get; internal set; }
    public int LoopCount { get; internal set; }

    public QueryContext(int eventCapacity = 64, int overlapCapacity = 16)
    {
        _eventBuffer = new ClipHit[eventCapacity];
        _overlapBuffer = new OverlapInfo[overlapCapacity];
        _tempEventBuffer = new ClipHit[64];
        _tempOverlapBuffer = new OverlapInfo[16];
    }

    public ReadOnlySpan<ClipHit> Events => new ReadOnlySpan<ClipHit>(_eventBuffer, 0, _eventCount);
    public ReadOnlySpan<OverlapInfo> Overlaps => new ReadOnlySpan<OverlapInfo>(_overlapBuffer, 0, _overlapCount);

    public QueryResult GetResult()
    {
        return new QueryResult
        {
            ResultFrame = ResultFrame,
            DidLoop = DidLoop,
            LoopCount = LoopCount,
            Events = Events,
            Overlaps = Overlaps
        };
    }

    public void Reset()
    {
        _eventCount = 0;
        _overlapCount = 0;
        ResultFrame = 0;
        DidLoop = false;
        LoopCount = 0;
    }

    internal Span<ClipHit> GetEventBuffer() => _eventBuffer.AsSpan();
    internal Span<OverlapInfo> GetOverlapBuffer() => _overlapBuffer.AsSpan();

    internal void SetEventCount(int count) => _eventCount = count;
    internal void SetOverlapCount(int count) => _overlapCount = count;

    internal void EnsureEventCapacity(int required)
    {
        if (_eventBuffer.Length < required)
        {
            int newSize = Math.Max(_eventBuffer.Length * 2, required);
            Array.Resize(ref _eventBuffer, newSize);
        }
    }

    internal void EnsureOverlapCapacity(int required)
    {
        if (_overlapBuffer.Length < required)
        {
            int newSize = Math.Max(_overlapBuffer.Length * 2, required);
            Array.Resize(ref _overlapBuffer, newSize);
        }
    }

    internal void AddEvent(in ClipHit hit)
    {
        EnsureEventCapacity(_eventCount + 1);
        _eventBuffer[_eventCount++] = hit;
    }

    internal void AddOverlap(in OverlapInfo overlap)
    {
        EnsureOverlapCapacity(_overlapCount + 1);
        _overlapBuffer[_overlapCount++] = overlap;
    }

    internal Span<OverlapInfo> GetOverlapSpan() => _overlapBuffer.AsSpan(0, _overlapCount);

    internal Span<ClipHit> GetTempEventBuffer() => _tempEventBuffer.AsSpan();
    internal Span<OverlapInfo> GetTempOverlapBuffer() => _tempOverlapBuffer.AsSpan();
}
