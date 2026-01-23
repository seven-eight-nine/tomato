using System;

namespace Tomato.InventorySystem;

/// <summary>
/// アイテム定義（種類）を一意に識別するID。
/// 同じ種類のアイテムは同じItemDefinitionIdを持つ。
/// </summary>
public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>, IComparable<ItemDefinitionId>
{
    public readonly int Value;

    public ItemDefinitionId(int value) => Value = value;

    public bool Equals(ItemDefinitionId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ItemDefinitionId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(ItemDefinitionId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ItemDefinitionId left, ItemDefinitionId right) => left.Equals(right);
    public static bool operator !=(ItemDefinitionId left, ItemDefinitionId right) => !left.Equals(right);

    public override string ToString() => $"ItemDefinitionId({Value})";

    public static readonly ItemDefinitionId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
