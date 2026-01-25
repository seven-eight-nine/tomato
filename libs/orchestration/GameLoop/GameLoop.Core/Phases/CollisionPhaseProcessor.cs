using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Collision;
using Tomato.GameLoop.Providers;
using Tomato.SystemPipeline;

namespace Tomato.GameLoop.Phases;

/// <summary>
/// 衝突判定システム。
/// ICollisionSourceから衝突結果を取得し、ICollisionMessageEmitterでメッセージを発行する。
/// 衝突検出自体はゲーム側がSpatialSystemを使用して行う。
/// </summary>
public sealed class CollisionSystem : ISerialSystem
{
    private readonly ICollisionSource _source;
    private readonly ICollisionMessageEmitter _emitter;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// CollisionSystemを生成する。
    /// </summary>
    /// <param name="source">衝突結果ソース（ゲーム側でSpatialSystemを使用して実装）</param>
    /// <param name="emitter">衝突メッセージエミッター（ゲーム側で実装）</param>
    public CollisionSystem(
        ICollisionSource source,
        ICollisionMessageEmitter emitter)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // 1. ソースから衝突結果を取得
        var collisions = _source.GetCollisions();

        // 2. メッセージ発行
        _emitter.EmitMessages(collisions);

        // 3. 次フレームのためにクリア
        _source.Clear();
    }
}
