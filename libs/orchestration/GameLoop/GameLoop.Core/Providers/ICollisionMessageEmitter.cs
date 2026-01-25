using System.Collections.Generic;
using Tomato.GameLoop.Collision;

namespace Tomato.GameLoop.Providers;

/// <summary>
/// 衝突結果からメッセージを発行するインターフェース。
/// ゲーム側で実装し、衝突検出後のメッセージ発行ロジックを提供する。
/// </summary>
public interface ICollisionMessageEmitter
{
    /// <summary>
    /// 衝突結果からメッセージを発行する。
    /// </summary>
    /// <param name="collisions">検出された衝突ペアのリスト</param>
    void EmitMessages(IReadOnlyList<CollisionPair> collisions);
}
