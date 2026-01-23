using System;

namespace Tomato.FlowTree;

/// <summary>
/// Blackboardキーの基底インターフェース。
/// </summary>
public interface IBlackboardKey
{
    /// <summary>
    /// キーID。
    /// </summary>
    int Id { get; }
}

/// <summary>
/// 型安全なBlackboardキー（ゼロGC）。
/// </summary>
/// <typeparam name="T">値の型</typeparam>
public readonly struct BlackboardKey<T> : IBlackboardKey, IEquatable<BlackboardKey<T>>
{
    /// <summary>
    /// キーID。
    /// </summary>
    public readonly int Id;

    int IBlackboardKey.Id => Id;

    /// <summary>
    /// BlackboardKeyを作成する。
    /// </summary>
    /// <param name="id">キーID</param>
    public BlackboardKey(int id) => Id = id;

    public bool Equals(BlackboardKey<T> other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is BlackboardKey<T> other && Equals(other);
    public override int GetHashCode() => Id;
    public override string ToString() => $"BlackboardKey<{typeof(T).Name}>({Id})";

    public static bool operator ==(BlackboardKey<T> left, BlackboardKey<T> right) => left.Equals(right);
    public static bool operator !=(BlackboardKey<T> left, BlackboardKey<T> right) => !left.Equals(right);
}
