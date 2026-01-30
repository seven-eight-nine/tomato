using System;

namespace Tomato.CollisionSystem;

/// <summary>
/// Shape を識別するハンドル。
/// Index と Generation の組み合わせで、削除後の誤参照を防ぐ。
/// </summary>
public readonly struct ShapeHandle : IEquatable<ShapeHandle>
{
    /// <summary>
    /// 内部インデックス。
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// 世代番号（削除・再利用の追跡用）。
    /// </summary>
    public readonly int Generation;

    public ShapeHandle(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    /// <summary>
    /// 無効なハンドル。
    /// </summary>
    public static ShapeHandle Invalid => new(-1, 0);

    /// <summary>
    /// 有効なハンドルかどうか。
    /// </summary>
    public bool IsValid => Index >= 0;

    public bool Equals(ShapeHandle other)
        => Index == other.Index && Generation == other.Generation;

    public override bool Equals(object? obj)
        => obj is ShapeHandle other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return Index * 31 + Generation;
        }
    }

    public static bool operator ==(ShapeHandle left, ShapeHandle right)
        => left.Equals(right);

    public static bool operator !=(ShapeHandle left, ShapeHandle right)
        => !left.Equals(right);

    public override string ToString()
        => $"Shape({Index}:{Generation})";
}
