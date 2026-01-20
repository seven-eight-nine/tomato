using System;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突対象を絞り込むフィルタ。
/// レイヤーとマスクを使用して、衝突可能な対象を制御する。
/// </summary>
public readonly struct CollisionFilter : IEquatable<CollisionFilter>
{
    /// <summary>このフィルタが属するレイヤー（ビットマスク）。</summary>
    public readonly uint Layer;

    /// <summary>衝突対象とするレイヤー（ビットマスク）。</summary>
    public readonly uint Mask;

    public CollisionFilter(uint layer, uint mask)
    {
        Layer = layer;
        Mask = mask;
    }

    /// <summary>
    /// 他のフィルタと衝突可能か判定する。
    /// 双方向のマスクチェックを行う。
    /// </summary>
    public bool CanCollideWith(in CollisionFilter other)
    {
        // 双方向チェック: 両方のフィルタで相手のレイヤーがマスクに含まれている必要がある
        return (Layer & other.Mask) != 0 && (other.Layer & Mask) != 0;
    }

    /// <summary>全てのレイヤーと衝突するフィルタ。</summary>
    public static CollisionFilter All => new(uint.MaxValue, uint.MaxValue);

    /// <summary>何とも衝突しないフィルタ。</summary>
    public static CollisionFilter None => new(0, 0);

    public bool Equals(CollisionFilter other)
        => Layer == other.Layer && Mask == other.Mask;

    public override bool Equals(object? obj)
        => obj is CollisionFilter other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Layer, Mask);

    public static bool operator ==(CollisionFilter left, CollisionFilter right)
        => left.Equals(right);

    public static bool operator !=(CollisionFilter left, CollisionFilter right)
        => !left.Equals(right);

    public override string ToString()
        => $"Filter(Layer=0x{Layer:X}, Mask=0x{Mask:X})";
}
