using System;

namespace Tomato.TimelineSystem;

public readonly struct LoopSettings : IEquatable<LoopSettings>
{
    public readonly bool Enabled;
    public readonly int StartFrame;
    public readonly int EndFrame;

    private LoopSettings(bool enabled, int startFrame, int endFrame)
    {
        Enabled = enabled;
        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public int Duration => EndFrame - StartFrame;

    public static LoopSettings None => new(false, 0, 0);

    public static LoopSettings Create(int startFrame, int endFrame)
    {
        if (endFrame <= startFrame)
        {
            throw new ArgumentException("EndFrame must be greater than StartFrame");
        }
        return new LoopSettings(true, startFrame, endFrame);
    }

    public bool Equals(LoopSettings other) =>
        Enabled == other.Enabled &&
        StartFrame == other.StartFrame &&
        EndFrame == other.EndFrame;

    public override bool Equals(object? obj) => obj is LoopSettings other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Enabled, StartFrame, EndFrame);

    public static bool operator ==(LoopSettings left, LoopSettings right) => left.Equals(right);
    public static bool operator !=(LoopSettings left, LoopSettings right) => !left.Equals(right);
}
