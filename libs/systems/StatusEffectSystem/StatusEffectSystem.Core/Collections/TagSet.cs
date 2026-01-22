using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// タグの集合（最大128タグ、2つのulongで管理）
    /// </summary>
    public readonly struct TagSet : IEquatable<TagSet>
    {
        private readonly ulong _bits0;  // TagId 0-63
        private readonly ulong _bits1;  // TagId 64-127

        private TagSet(ulong bits0, ulong bits1)
        {
            _bits0 = bits0;
            _bits1 = bits1;
        }

        public static readonly TagSet Empty = new(0, 0);

        public bool Contains(TagId tag)
        {
            if (tag.Value < 0) return false;
            if (tag.Value < 64)
                return (_bits0 & (1UL << tag.Value)) != 0;
            if (tag.Value < 128)
                return (_bits1 & (1UL << (tag.Value - 64))) != 0;
            return false;
        }

        public TagSet With(TagId tag)
        {
            if (tag.Value < 0) return this;
            if (tag.Value < 64)
                return new TagSet(_bits0 | (1UL << tag.Value), _bits1);
            if (tag.Value < 128)
                return new TagSet(_bits0, _bits1 | (1UL << (tag.Value - 64)));
            return this;
        }

        public TagSet Without(TagId tag)
        {
            if (tag.Value < 0) return this;
            if (tag.Value < 64)
                return new TagSet(_bits0 & ~(1UL << tag.Value), _bits1);
            if (tag.Value < 128)
                return new TagSet(_bits0, _bits1 & ~(1UL << (tag.Value - 64)));
            return this;
        }

        public bool ContainsAny(TagSet other)
            => ((_bits0 & other._bits0) | (_bits1 & other._bits1)) != 0;

        public bool ContainsAll(TagSet other)
            => (_bits0 & other._bits0) == other._bits0
            && (_bits1 & other._bits1) == other._bits1;

        public static TagSet operator |(TagSet a, TagSet b)
            => new(a._bits0 | b._bits0, a._bits1 | b._bits1);

        public static TagSet operator &(TagSet a, TagSet b)
            => new(a._bits0 & b._bits0, a._bits1 & b._bits1);

        public static TagSet operator ~(TagSet a)
            => new(~a._bits0, ~a._bits1);

        public bool Equals(TagSet other)
            => _bits0 == other._bits0 && _bits1 == other._bits1;

        public override bool Equals(object? obj)
            => obj is TagSet other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (_bits0.GetHashCode() * 397) ^ _bits1.GetHashCode();
            }
        }

        public static bool operator ==(TagSet left, TagSet right) => left.Equals(right);
        public static bool operator !=(TagSet left, TagSet right) => !left.Equals(right);

        public int Count => PopCount(_bits0) + PopCount(_bits1);
        public bool IsEmpty => _bits0 == 0 && _bits1 == 0;

        private static int PopCount(ulong value)
        {
            value = value - ((value >> 1) & 0x5555555555555555UL);
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            return (int)(unchecked(((value + (value >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        public override string ToString() => $"TagSet(count:{Count})";
    }
}
