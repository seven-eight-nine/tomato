using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 排他グループの識別子
    /// </summary>
    public readonly struct GroupId : IEquatable<GroupId>, IComparable<GroupId>
    {
        public readonly int Value;

        internal GroupId(int value) => Value = value;

        public bool Equals(GroupId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is GroupId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(GroupId other) => Value.CompareTo(other.Value);

        public static bool operator ==(GroupId left, GroupId right) => left.Equals(right);
        public static bool operator !=(GroupId left, GroupId right) => !left.Equals(right);

        public override string ToString() => $"GroupId({Value})";

        public static readonly GroupId Invalid = new(-1);
        public static readonly GroupId None = new(0);
        public bool IsValid => Value >= 0;
    }
}
