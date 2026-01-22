using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// フラグの集合（最大64フラグ）
    /// </summary>
    public readonly struct FlagSet : IEquatable<FlagSet>
    {
        private readonly ulong _bits;

        public FlagSet(ulong bits) => _bits = bits;

        public static readonly FlagSet Empty = new(0);

        public bool Has(FlagId flag)
        {
            if (flag.Value < 0 || flag.Value >= 64) return false;
            return (_bits & (1UL << flag.Value)) != 0;
        }

        public FlagSet With(FlagId flag)
        {
            if (flag.Value < 0 || flag.Value >= 64) return this;
            return new FlagSet(_bits | (1UL << flag.Value));
        }

        public FlagSet Without(FlagId flag)
        {
            if (flag.Value < 0 || flag.Value >= 64) return this;
            return new FlagSet(_bits & ~(1UL << flag.Value));
        }

        public FlagSet Toggle(FlagId flag)
        {
            if (flag.Value < 0 || flag.Value >= 64) return this;
            return new FlagSet(_bits ^ (1UL << flag.Value));
        }

        public static FlagSet operator |(FlagSet a, FlagSet b) => new(a._bits | b._bits);
        public static FlagSet operator &(FlagSet a, FlagSet b) => new(a._bits & b._bits);
        public static FlagSet operator ~(FlagSet a) => new(~a._bits);

        public bool Equals(FlagSet other) => _bits == other._bits;
        public override bool Equals(object? obj) => obj is FlagSet other && Equals(other);
        public override int GetHashCode() => _bits.GetHashCode();

        public static bool operator ==(FlagSet left, FlagSet right) => left.Equals(right);
        public static bool operator !=(FlagSet left, FlagSet right) => !left.Equals(right);

        public int Count => PopCount(_bits);
        public bool IsEmpty => _bits == 0;

        private static int PopCount(ulong value)
        {
            value = value - ((value >> 1) & 0x5555555555555555UL);
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            return (int)(unchecked(((value + (value >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        public override string ToString() => $"FlagSet(0x{_bits:X16})";
    }
}
