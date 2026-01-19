using Tomato.EntityHandleSystem;

namespace Tomato.CollisionSystem;

/// <summary>
/// Entityが発行する衝突判定ボリューム。
/// 形状、フィルタ、ライフタイムを持つ。
/// </summary>
public sealed class CollisionVolume
{
    /// <summary>発行元Entityのハンドル。</summary>
    public readonly VoidHandle Owner;

    /// <summary>衝突形状。</summary>
    public readonly CollisionShape Shape;

    /// <summary>衝突フィルタ。</summary>
    public readonly CollisionFilter Filter;

    /// <summary>ユーザー定義のボリュームタイプ。</summary>
    public readonly int VolumeType;

    /// <summary>有効期間（フレーム数）。0は無限。</summary>
    public readonly int Lifetime;

    /// <summary>残り有効期間。</summary>
    public int RemainingLifetime { get; private set; }

    /// <summary>有効期限切れかどうか。</summary>
    public bool IsExpired => Lifetime > 0 && RemainingLifetime <= 0;

    public CollisionVolume(
        VoidHandle owner,
        CollisionShape shape,
        CollisionFilter filter,
        int volumeType = 0,
        int lifetime = 0)
    {
        Owner = owner;
        Shape = shape;
        Filter = filter;
        VolumeType = volumeType;
        Lifetime = lifetime;
        RemainingLifetime = lifetime;
    }

    /// <summary>
    /// 1フレーム経過させる。
    /// </summary>
    public void Tick()
    {
        if (Lifetime > 0 && RemainingLifetime > 0)
        {
            RemainingLifetime--;
        }
    }

    /// <summary>
    /// 指定位置でのAABBを取得する。
    /// </summary>
    public AABB GetBounds(Vector3 position)
    {
        return Shape.GetBounds(position);
    }
}
