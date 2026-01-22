using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// フラグの識別子（最大64個）
    /// </summary>
    public readonly struct FlagId : IEquatable<FlagId>, IComparable<FlagId>
    {
        public readonly int Value;

        internal FlagId(int value) => Value = value;

        public bool Equals(FlagId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is FlagId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(FlagId other) => Value.CompareTo(other.Value);

        public static bool operator ==(FlagId left, FlagId right) => left.Equals(right);
        public static bool operator !=(FlagId left, FlagId right) => !left.Equals(right);

        public override string ToString() => $"FlagId({Value})";

        public static readonly FlagId Invalid = new(-1);
        public bool IsValid => Value >= 0 && Value < 64;
    }
}
