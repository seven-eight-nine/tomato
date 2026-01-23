using System;

namespace Tomato.InventorySystem;

/// <summary>
/// スナップショットを一意に識別するID。
/// </summary>
public readonly struct SnapshotId : IEquatable<SnapshotId>, IComparable<SnapshotId>
{
    public readonly long Value;

    public SnapshotId(long value) => Value = value;

    public bool Equals(SnapshotId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is SnapshotId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(SnapshotId other) => Value.CompareTo(other.Value);

    public static bool operator ==(SnapshotId left, SnapshotId right) => left.Equals(right);
    public static bool operator !=(SnapshotId left, SnapshotId right) => !left.Equals(right);

    public override string ToString() => $"SnapshotId({Value})";

    public static readonly SnapshotId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
