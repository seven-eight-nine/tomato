using System;

namespace Tomato.Time
{
    /// <summary>
    /// ティック単位の持続時間
    /// </summary>
    public readonly struct TickDuration : IEquatable<TickDuration>, IComparable<TickDuration>
    {
        public readonly int Value;

        public TickDuration(int value) => Value = Math.Max(0, value);

        public static readonly TickDuration Zero = new(0);
        public static readonly TickDuration Infinite = new(int.MaxValue);

        public bool IsInfinite => Value == int.MaxValue;
        public bool IsZero => Value == 0;

        public static TickDuration operator +(TickDuration a, TickDuration b)
        {
            if (a.IsInfinite || b.IsInfinite) return Infinite;
            long sum = (long)a.Value + b.Value;
            if (sum > int.MaxValue) return Infinite;
            return new TickDuration((int)sum);
        }

        public static TickDuration operator +(TickDuration a, int ticks)
        {
            if (a.IsInfinite) return Infinite;
            long sum = (long)a.Value + ticks;
            if (sum > int.MaxValue) return Infinite;
            return new TickDuration((int)sum);
        }

        public static TickDuration operator -(TickDuration a, TickDuration b)
        {
            if (a.IsInfinite) return Infinite;
            return new TickDuration(Math.Max(0, a.Value - b.Value));
        }

        public static TickDuration operator *(TickDuration duration, int multiplier)
        {
            if (duration.IsInfinite) return Infinite;
            if (multiplier <= 0) return Zero;
            long product = (long)duration.Value * multiplier;
            if (product > int.MaxValue) return Infinite;
            return new TickDuration((int)product);
        }

        public static bool operator <(TickDuration a, TickDuration b) => a.Value < b.Value;
        public static bool operator >(TickDuration a, TickDuration b) => a.Value > b.Value;
        public static bool operator <=(TickDuration a, TickDuration b) => a.Value <= b.Value;
        public static bool operator >=(TickDuration a, TickDuration b) => a.Value >= b.Value;

        public bool Equals(TickDuration other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is TickDuration other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(TickDuration other) => Value.CompareTo(other.Value);

        public static bool operator ==(TickDuration left, TickDuration right) => left.Equals(right);
        public static bool operator !=(TickDuration left, TickDuration right) => !left.Equals(right);

        public override string ToString() => IsInfinite ? "Infinite" : $"{Value}ticks";
    }
}
