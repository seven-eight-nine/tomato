using System;

namespace Tomato.InventorySystem;

/// <summary>
/// アイテムインスタンスを一意に識別するID。
/// 同じ種類のアイテムでも、各インスタンスは異なるItemInstanceIdを持つ。
/// </summary>
public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>, IComparable<ItemInstanceId>
{
    public readonly long Value;

    public ItemInstanceId(long value) => Value = value;

    public bool Equals(ItemInstanceId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ItemInstanceId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(ItemInstanceId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ItemInstanceId left, ItemInstanceId right) => left.Equals(right);
    public static bool operator !=(ItemInstanceId left, ItemInstanceId right) => !left.Equals(right);

    public override string ToString() => $"ItemInstanceId({Value})";

    public static readonly ItemInstanceId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
