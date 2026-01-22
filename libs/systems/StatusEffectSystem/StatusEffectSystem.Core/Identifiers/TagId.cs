using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// タグの識別子
    /// </summary>
    public readonly struct TagId : IEquatable<TagId>, IComparable<TagId>
    {
        public readonly int Value;

        internal TagId(int value) => Value = value;

        public bool Equals(TagId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is TagId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(TagId other) => Value.CompareTo(other.Value);

        public static bool operator ==(TagId left, TagId right) => left.Equals(right);
        public static bool operator !=(TagId left, TagId right) => !left.Equals(right);

        public override string ToString() => $"TagId({Value})";

        public static readonly TagId Invalid = new(-1);
        public bool IsValid => Value >= 0;
    }
}
