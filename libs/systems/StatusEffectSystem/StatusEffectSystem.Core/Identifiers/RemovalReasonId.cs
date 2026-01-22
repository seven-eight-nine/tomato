using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 除去理由ID
    /// </summary>
    public readonly struct RemovalReasonId : IEquatable<RemovalReasonId>
    {
        public readonly int Value;

        public RemovalReasonId(int value) => Value = value;

        public bool Equals(RemovalReasonId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is RemovalReasonId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(RemovalReasonId left, RemovalReasonId right) => left.Equals(right);
        public static bool operator !=(RemovalReasonId left, RemovalReasonId right) => !left.Equals(right);

        public override string ToString() => $"RemovalReasonId({Value})";

        /// <summary>期限切れによる自動除去（ライブラリ内部で使用）</summary>
        public const int Expired = 0;
    }
}
