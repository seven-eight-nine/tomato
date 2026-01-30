using System;
using System.Collections.Generic;
using Tomato.Math;

namespace Tomato.TimelineSystem;

/// <summary>
/// トラックの基底クラス
/// </summary>
public abstract class Track
{
    private readonly List<Clip> _clips = new();
    private bool _sorted;
    private int _maxClipDuration;

    public TrackId Id { get; private set; } = TrackId.Unassigned;

    internal void AssignId(TrackId id) => Id = id;

    public void AddClip(Clip clip)
    {
        _clips.Add(clip);
        _sorted = false;
        int duration = clip.EndFrame - clip.StartFrame;
        if (duration > _maxClipDuration)
        {
            _maxClipDuration = duration;
        }
    }

    public void RemoveClip(ClipId clipId)
    {
        for (int i = _clips.Count - 1; i >= 0; i--)
        {
            if (_clips[i].Id == clipId)
            {
                _clips.RemoveAt(i);
                return;
            }
        }
    }

    public Clip? GetClip(ClipId clipId)
    {
        for (int i = 0; i < _clips.Count; i++)
        {
            if (_clips[i].Id == clipId)
            {
                return _clips[i];
            }
        }
        return null;
    }

    internal int QueryRange(int startFrame, int endFrame, Span<ClipHit> results, bool includeActive)
    {
        EnsureSorted();

        int count = 0;
        int searchStart = FindFirstPotentialClip(startFrame);

        for (int i = searchStart; i < _clips.Count && count < results.Length; i++)
        {
            var clip = _clips[i];

            if (clip.StartFrame > endFrame)
            {
                break;
            }

            if (clip.Type == ClipType.Instant)
            {
                if (clip.StartFrame >= startFrame && clip.StartFrame <= endFrame)
                {
                    results[count++] = new ClipHit(clip, ClipEventType.Fired, clip.StartFrame, 1.0f);
                }
            }
            else
            {
                bool enterInRange = clip.StartFrame >= startFrame && clip.StartFrame <= endFrame;
                bool exitInRange = clip.EndFrame >= startFrame && clip.EndFrame <= endFrame;
                bool activeAtEnd = clip.StartFrame <= endFrame && clip.EndFrame > endFrame;

                if (enterInRange && count < results.Length)
                {
                    results[count++] = new ClipHit(clip, ClipEventType.Enter, clip.StartFrame, 0f);
                }

                if (exitInRange && count < results.Length)
                {
                    results[count++] = new ClipHit(clip, ClipEventType.Exit, clip.EndFrame, 1.0f);
                }

                if (includeActive && activeAtEnd && !enterInRange && count < results.Length)
                {
                    float progress = CalculateProgress(clip, endFrame);
                    results[count++] = new ClipHit(clip, ClipEventType.Active, endFrame, progress);
                }
            }
        }

        return count;
    }

    internal int GetActiveClips(int frame, Span<OverlapInfo> results)
    {
        EnsureSorted();

        int count = 0;
        int searchStart = FindFirstPotentialClip(frame);

        for (int i = searchStart; i < _clips.Count && count < results.Length; i++)
        {
            var clip = _clips[i];

            if (clip.StartFrame > frame)
            {
                break;
            }

            if (clip.Type == ClipType.Range && clip.StartFrame <= frame && clip.EndFrame >= frame)
            {
                float progress = CalculateProgress(clip, frame);
                results[count++] = new OverlapInfo(clip, progress);
            }
        }

        return count;
    }

    private void EnsureSorted()
    {
        if (_sorted) return;
        _clips.Sort((a, b) => a.StartFrame.CompareTo(b.StartFrame));
        _sorted = true;
    }

    private int FindFirstPotentialClip(int frame)
    {
        if (_clips.Count == 0) return 0;

        int searchFrame = frame - _maxClipDuration;
        if (searchFrame <= _clips[0].StartFrame) return 0;

        int lo = 0;
        int hi = _clips.Count;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_clips[mid].StartFrame < searchFrame)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static float CalculateProgress(Clip clip, int currentFrame)
    {
        int duration = clip.EndFrame - clip.StartFrame;
        if (duration <= 0) return 1.0f;

        float progress = (float)(currentFrame - clip.StartFrame) / duration;
        return MathF.Max(0f, MathF.Min(1f, progress));
    }
}
