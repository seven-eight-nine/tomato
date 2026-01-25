using System.Collections.Generic;

namespace Tomato.GameLoop.Collision;

/// <summary>
/// 衝突結果を提供するインターフェース。
/// ゲーム側でSpatialSystemを使用して実装する。
/// </summary>
public interface ICollisionSource
{
    /// <summary>
    /// 現在フレームの衝突ペアを取得する。
    /// </summary>
    IReadOnlyList<CollisionPair> GetCollisions();

    /// <summary>
    /// 衝突結果をクリアする（フレーム開始時に呼ばれる）。
    /// </summary>
    void Clear();
}
