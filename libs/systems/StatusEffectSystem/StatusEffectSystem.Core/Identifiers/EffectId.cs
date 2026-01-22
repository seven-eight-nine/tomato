using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 状態異常定義の識別子
    /// </summary>
    public readonly struct EffectId : IEquatable<EffectId>, IComparable<EffectId>
    {
        public readonly int Value;

        internal EffectId(int value) => Value = value;

        public bool Equals(EffectId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EffectId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(EffectId other) => Value.CompareTo(other.Value);

        public static bool operator ==(EffectId left, EffectId right) => left.Equals(right);
        public static bool operator !=(EffectId left, EffectId right) => !left.Equals(right);

        public override string ToString() => $"EffectId({Value})";

        public static readonly EffectId Invalid = new(-1);
        public bool IsValid => Value >= 0;
    }
}
