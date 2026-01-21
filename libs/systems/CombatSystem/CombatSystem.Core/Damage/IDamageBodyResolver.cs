namespace Tomato.CombatSystem;

/// <summary>
/// 衝突形状からDamageBodyを解決する。アプリ側で実装する。
/// </summary>
public interface IDamageBodyResolver
{
    /// <summary>
    /// 衝突形状IDからDamageBodyを取得。
    /// </summary>
    DamageBody? Resolve(int collisionShapeId);

    /// <summary>
    /// 衝突形状オブジェクトからDamageBodyを取得。
    /// </summary>
    DamageBody? Resolve(object collisionShape);
}
