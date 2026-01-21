using System;
using Tomato.EntityHandleSystem;

namespace Tomato.CollisionSystem;

/// <summary>
/// 衝突検出の結果。
/// 2つのボリュームとその衝突情報を保持する。
/// </summary>
public readonly struct CollisionResult
{
    /// <summary>衝突した第1ボリューム。</summary>
    public readonly CollisionVolume Volume1;

    /// <summary>衝突した第2ボリューム。</summary>
    public readonly CollisionVolume Volume2;

    /// <summary>衝突の接触情報。</summary>
    public readonly CollisionContact Contact;

    public CollisionResult(CollisionVolume volume1, CollisionVolume volume2, CollisionContact contact)
    {
        Volume1 = volume1;
        Volume2 = volume2;
        Contact = contact;
    }

    /// <summary>第1ボリュームのオーナー。</summary>
    public VoidHandle Owner1 => Volume1.Owner;

    /// <summary>第2ボリュームのオーナー。</summary>
    public VoidHandle Owner2 => Volume2.Owner;

    public override string ToString()
        => $"Collision({Owner1.Index} vs {Owner2.Index}, Penetration={Contact.Penetration})";
}
