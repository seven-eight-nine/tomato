using System;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリを一意に識別するID。
/// </summary>
public readonly struct InventoryId : IEquatable<InventoryId>, IComparable<InventoryId>
{
    public readonly int Value;

    public InventoryId(int value) => Value = value;

    public bool Equals(InventoryId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is InventoryId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(InventoryId other) => Value.CompareTo(other.Value);

    public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);
    public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);

    public override string ToString() => $"InventoryId({Value})";

    public static readonly InventoryId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
