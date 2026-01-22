using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 付与失敗理由ID
    /// </summary>
    public readonly struct FailureReasonId : IEquatable<FailureReasonId>
    {
        public readonly int Value;

        public FailureReasonId(int value) => Value = value;

        public bool Equals(FailureReasonId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is FailureReasonId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(FailureReasonId left, FailureReasonId right) => left.Equals(right);
        public static bool operator !=(FailureReasonId left, FailureReasonId right) => !left.Equals(right);

        public override string ToString() => $"FailureReasonId({Value})";

        /// <summary>効果定義が未登録（ライブラリ内部で使用）</summary>
        public const int DefinitionNotFound = 0;
    }
}
