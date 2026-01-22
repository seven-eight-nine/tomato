using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// ゲーム内の論理時刻（フレーム番号）
    /// 決定論的実行のため、実時間ではなくティック単位で管理
    /// </summary>
    public readonly struct GameTick : IEquatable<GameTick>, IComparable<GameTick>
    {
        public readonly long Value;

        public GameTick(long value) => Value = value;

        public static GameTick operator +(GameTick tick, TickDuration duration)
        {
            if (duration.IsInfinite) return new GameTick(long.MaxValue);
            return new GameTick(tick.Value + duration.Value);
        }

        public static GameTick operator -(GameTick tick, TickDuration duration)
        {
            if (duration.IsInfinite) return new GameTick(long.MinValue);
            return new GameTick(tick.Value - duration.Value);
        }

        public static TickDuration operator -(GameTick a, GameTick b)
        {
            var diff = a.Value - b.Value;
            if (diff > int.MaxValue) return TickDuration.Infinite;
            if (diff < 0) return TickDuration.Zero;
            return new TickDuration((int)diff);
        }

        public static bool operator <(GameTick a, GameTick b) => a.Value < b.Value;
        public static bool operator >(GameTick a, GameTick b) => a.Value > b.Value;
        public static bool operator <=(GameTick a, GameTick b) => a.Value <= b.Value;
        public static bool operator >=(GameTick a, GameTick b) => a.Value >= b.Value;

        public bool Equals(GameTick other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is GameTick other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(GameTick other) => Value.CompareTo(other.Value);

        public static bool operator ==(GameTick left, GameTick right) => left.Equals(right);
        public static bool operator !=(GameTick left, GameTick right) => !left.Equals(right);

        public override string ToString() => $"Tick({Value})";

        public static readonly GameTick Zero = new(0);
        public static readonly GameTick MaxValue = new(long.MaxValue);
    }
}
