using System;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態を一意に識別する構造体。
/// </summary>
public readonly struct StateId : IEquatable<StateId>
{
    public string Value { get; }

    public StateId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(StateId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is StateId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public override string ToString() => Value;

    public static bool operator ==(StateId left, StateId right) => left.Equals(right);
    public static bool operator !=(StateId left, StateId right) => !left.Equals(right);

    public static implicit operator StateId(string value) => new StateId(value);
    public static implicit operator string(StateId id) => id.Value;

    /// <summary>
    /// Any State を表す特殊な StateId。
    /// どの状態からでも遷移可能な状態を定義する際に使用。
    /// </summary>
    public static readonly StateId Any = new StateId("__ANY__");

    /// <summary>
    /// この StateId が Any State かどうかを判定。
    /// </summary>
    public bool IsAny => Value == Any.Value;
}
