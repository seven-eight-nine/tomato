using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.EntitySystem.Context;
using Tomato.EntitySystem.Providers;
using Tomato.SystemPipeline;
using Tomato.CollisionSystem;

namespace Tomato.EntitySystem.Phases;

/// <summary>
/// 衝突判定システム。
/// 全Entityの衝突ボリュームを収集し、衝突を検出してメッセージを発行。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class CollisionSystem<TCategory> : ISerialSystem
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _entityRegistry;
    private readonly CollisionDetector _detector;
    private readonly ICollisionMessageEmitter _emitter;
    private readonly IEntityPositionProvider _positionProvider;
    private readonly List<CollisionResult> _results;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// CollisionSystemを生成する。
    /// </summary>
    /// <param name="entityRegistry">エンティティレジストリ</param>
    /// <param name="detector">衝突検出器</param>
    /// <param name="positionProvider">位置プロバイダ</param>
    /// <param name="emitter">衝突メッセージエミッター（ゲーム側で実装）</param>
    public CollisionSystem(
        EntityContextRegistry<TCategory> entityRegistry,
        CollisionDetector detector,
        IEntityPositionProvider positionProvider,
        ICollisionMessageEmitter emitter)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        _results = new List<CollisionResult>();
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<VoidHandle> entities,
        in SystemContext context)
    {
        // 1. 前フレームのボリュームをクリア
        _detector.Clear();
        _results.Clear();

        // 2. 全Entityのボリュームを収集・登録
        foreach (var handle in entities)
        {
            if (!_entityRegistry.TryGetContext(handle, out var entityContext) || entityContext == null)
                continue;

            if (!entityContext.IsActive)
                continue;

            var position = _positionProvider.GetPosition(handle);

            foreach (var volume in entityContext.CollisionVolumes)
            {
                if (!volume.IsExpired)
                {
                    _detector.AddVolume(volume, position);
                }
            }
        }

        // 3. 衝突検出
        _detector.DetectCollisions(_results);

        // 4. 衝突結果からメッセージ発行
        _emitter.EmitMessages(_results);

        // 5. ボリュームのTick処理（有効期限管理）
        foreach (var handle in entities)
        {
            if (!_entityRegistry.TryGetContext(handle, out var entityContext) || entityContext == null)
                continue;

            // 期限切れボリュームを削除
            entityContext.CollisionVolumes.RemoveAll(v => v.IsExpired);

            // 残りのボリュームをTick
            foreach (var volume in entityContext.CollisionVolumes)
            {
                volume.Tick();
            }
        }
    }
}
