using System;

namespace Tomato.TimelineSystem;

public readonly struct TrackId : IEquatable<TrackId>
{
    public readonly int Value;

    public static readonly TrackId Unassigned = new(-1);

    public bool IsUnassigned => Value < 0;

    public TrackId(int value)
    {
        Value = value;
    }

    public bool Equals(TrackId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is TrackId other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => IsUnassigned ? "TrackId(Unassigned)" : $"TrackId({Value})";

    public static bool operator ==(TrackId left, TrackId right) => left.Equals(right);
    public static bool operator !=(TrackId left, TrackId right) => !left.Equals(right);
}
