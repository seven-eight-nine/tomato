using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 実行時インスタンスID
    /// 上位32bit: 世代番号（削除検出用）
    /// 下位32bit: Arenaインデックス
    /// </summary>
    public readonly struct EffectInstanceId : IEquatable<EffectInstanceId>
    {
        public readonly ulong Value;

        internal EffectInstanceId(int index, int generation)
        {
            Value = ((ulong)(uint)generation << 32) | (uint)index;
        }

        private EffectInstanceId(ulong value) => Value = value;

        public int Index => (int)(Value & 0xFFFFFFFF);
        public int Generation => (int)(Value >> 32);

        public bool Equals(EffectInstanceId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EffectInstanceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(EffectInstanceId left, EffectInstanceId right) => left.Equals(right);
        public static bool operator !=(EffectInstanceId left, EffectInstanceId right) => !left.Equals(right);

        public override string ToString() => $"EffectInstanceId(idx:{Index}, gen:{Generation})";

        public static readonly EffectInstanceId Invalid = new(unchecked((ulong)-1));
        public bool IsValid => Value != unchecked((ulong)-1);
    }
}
