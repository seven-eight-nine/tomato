using System;

namespace Tomato.TimelineSystem;

public readonly struct ClipId : IEquatable<ClipId>
{
    public readonly int Value;

    public ClipId(int value)
    {
        Value = value;
    }

    public bool Equals(ClipId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ClipId other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => $"ClipId({Value})";

    public static bool operator ==(ClipId left, ClipId right) => left.Equals(right);
    public static bool operator !=(ClipId left, ClipId right) => !left.Equals(right);
}
