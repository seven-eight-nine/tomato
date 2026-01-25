using System;
using System.Collections.Generic;

namespace Tomato.TimelineSystem;

public sealed class Sequence
{
    private readonly List<Track> _tracks = new();
    private LoopSettings _loopSettings = LoopSettings.None;
    private IBlendCalculator _blendCalculator = ProgressBasedBlend.Instance;
    private int _nextTrackId;

    public bool IsLoop => _loopSettings.Enabled;
    public LoopSettings LoopSettings => _loopSettings;

    public IReadOnlyList<Track> Tracks => _tracks;

    public void SetLoopSettings(LoopSettings settings)
    {
        _loopSettings = settings;
    }

    public void SetBlendCalculator(IBlendCalculator calculator)
    {
        _blendCalculator = calculator ?? ProgressBasedBlend.Instance;
    }

    public void AddTrack(Track track)
    {
        track.AssignId(new TrackId(_nextTrackId++));
        _tracks.Add(track);
    }

    public T CreateTrack<T>() where T : Track, new()
    {
        var track = new T();
        AddTrack(track);
        return track;
    }

    public void RemoveTrack(TrackId trackId)
    {
        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            if (_tracks[i].Id == trackId)
            {
                _tracks.RemoveAt(i);
                return;
            }
        }
    }

    public Track? GetTrack(TrackId trackId)
    {
        for (int i = 0; i < _tracks.Count; i++)
        {
            if (_tracks[i].Id == trackId)
            {
                return _tracks[i];
            }
        }
        return null;
    }

    public void Query(int currentFrame, int deltaFrames, QueryContext ctx)
    {
        if (deltaFrames < 0)
        {
            throw new ArgumentException("deltaFrames must be non-negative", nameof(deltaFrames));
        }

        ctx.Reset();

        int rangeStart = currentFrame;
        int rangeEnd = currentFrame + deltaFrames;
        int resultFrame = rangeEnd;

        bool didLoop = false;
        int loopCount = 0;

        if (_loopSettings.Enabled && rangeEnd >= _loopSettings.EndFrame)
        {
            int loopDuration = _loopSettings.Duration;

            if (rangeStart < _loopSettings.EndFrame)
            {
                QueryRangeInternal(rangeStart, _loopSettings.EndFrame, ctx, false);
            }

            int overshoot = rangeEnd - _loopSettings.EndFrame;
            loopCount = 1 + overshoot / loopDuration;
            int remainder = overshoot % loopDuration;

            resultFrame = _loopSettings.StartFrame + remainder;
            didLoop = true;

            if (loopCount > 1)
            {
                QueryRangeInternal(_loopSettings.StartFrame, _loopSettings.EndFrame, ctx, false);
            }

            if (remainder > 0 || loopCount == 1)
            {
                QueryRangeInternal(_loopSettings.StartFrame, resultFrame, ctx, true);
            }
        }
        else
        {
            QueryRangeInternal(rangeStart, rangeEnd, ctx, true);
        }

        CollectOverlaps(resultFrame, ctx);
        _blendCalculator.CalculateWeights(ctx.GetOverlapSpan());

        ctx.ResultFrame = resultFrame;
        ctx.DidLoop = didLoop;
        ctx.LoopCount = loopCount;
    }

    private void QueryRangeInternal(int startFrame, int endFrame, QueryContext ctx, bool includeActive)
    {
        Span<ClipHit> tempBuffer = ctx.GetTempEventBuffer();

        for (int i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            int hitCount = track.QueryRange(startFrame, endFrame, tempBuffer, includeActive);

            for (int j = 0; j < hitCount; j++)
            {
                ctx.AddEvent(tempBuffer[j]);
            }
        }
    }

    private void CollectOverlaps(int frame, QueryContext ctx)
    {
        Span<OverlapInfo> tempBuffer = ctx.GetTempOverlapBuffer();

        for (int i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            int activeCount = track.GetActiveClips(frame, tempBuffer);

            if (activeCount > 1)
            {
                for (int j = 0; j < activeCount; j++)
                {
                    ctx.AddOverlap(tempBuffer[j]);
                }
            }
        }
    }
}
