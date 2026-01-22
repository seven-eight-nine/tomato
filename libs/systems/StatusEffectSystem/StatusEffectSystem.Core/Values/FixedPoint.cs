using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 固定小数点数（決定論的計算用）
    /// 小数点以下4桁の精度（SCALE = 10000）
    /// </summary>
    public readonly struct FixedPoint : IEquatable<FixedPoint>, IComparable<FixedPoint>
    {
        private const int SCALE = 10000;

        public readonly long RawValue;

        private FixedPoint(long rawValue) => RawValue = rawValue;

        public static FixedPoint FromInt(int value) => new((long)value * SCALE);
        public static FixedPoint FromFloat(float value) => new((long)(value * SCALE));
        public static FixedPoint FromDouble(double value) => new((long)(value * SCALE));
        public static FixedPoint FromRaw(long rawValue) => new(rawValue);

        public int ToInt() => (int)(RawValue / SCALE);
        public float ToFloat() => RawValue / (float)SCALE;
        public double ToDouble() => RawValue / (double)SCALE;

        public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new(a.RawValue + b.RawValue);
        public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new(a.RawValue - b.RawValue);
        public static FixedPoint operator -(FixedPoint a) => new(-a.RawValue);

        public static FixedPoint operator *(FixedPoint a, FixedPoint b)
            => new(a.RawValue * b.RawValue / SCALE);

        public static FixedPoint operator /(FixedPoint a, FixedPoint b)
        {
            if (b.RawValue == 0) throw new DivideByZeroException();
            return new(a.RawValue * SCALE / b.RawValue);
        }

        public static FixedPoint operator *(FixedPoint a, int b) => new(a.RawValue * b);
        public static FixedPoint operator /(FixedPoint a, int b)
        {
            if (b == 0) throw new DivideByZeroException();
            return new(a.RawValue / b);
        }

        public static bool operator <(FixedPoint a, FixedPoint b) => a.RawValue < b.RawValue;
        public static bool operator >(FixedPoint a, FixedPoint b) => a.RawValue > b.RawValue;
        public static bool operator <=(FixedPoint a, FixedPoint b) => a.RawValue <= b.RawValue;
        public static bool operator >=(FixedPoint a, FixedPoint b) => a.RawValue >= b.RawValue;

        public bool Equals(FixedPoint other) => RawValue == other.RawValue;
        public override bool Equals(object? obj) => obj is FixedPoint other && Equals(other);
        public override int GetHashCode() => RawValue.GetHashCode();
        public int CompareTo(FixedPoint other) => RawValue.CompareTo(other.RawValue);

        public static bool operator ==(FixedPoint left, FixedPoint right) => left.Equals(right);
        public static bool operator !=(FixedPoint left, FixedPoint right) => !left.Equals(right);

        public static readonly FixedPoint Zero = new(0);
        public static readonly FixedPoint One = FromInt(1);
        public static readonly FixedPoint MinValue = new(long.MinValue);
        public static readonly FixedPoint MaxValue = new(long.MaxValue);

        public FixedPoint Abs() => new(Math.Abs(RawValue));

        public static FixedPoint Min(FixedPoint a, FixedPoint b)
            => a.RawValue < b.RawValue ? a : b;

        public static FixedPoint Max(FixedPoint a, FixedPoint b)
            => a.RawValue > b.RawValue ? a : b;

        public static FixedPoint Clamp(FixedPoint value, FixedPoint min, FixedPoint max)
        {
            if (value.RawValue < min.RawValue) return min;
            if (value.RawValue > max.RawValue) return max;
            return value;
        }

        public override string ToString() => ToFloat().ToString("F4");
    }
}
