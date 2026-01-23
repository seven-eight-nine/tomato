using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.SystemPipeline;

namespace Tomato.GameLoop.Phases;

/// <summary>
/// クリーンアップシステム。
/// 消滅フラグが立ったEntityを削除。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class CleanupSystem<TCategory> : ISerialSystem
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _entityRegistry;
    private readonly IEntityDespawner _despawner;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// CleanupSystemを生成する。
    /// </summary>
    public CleanupSystem(
        EntityContextRegistry<TCategory> entityRegistry,
        IEntityDespawner despawner)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _despawner = despawner ?? throw new ArgumentNullException(nameof(despawner));
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // 1. 削除マーク済みEntityを取得
        var markedEntities = _entityRegistry.GetMarkedForDeletion();

        // 2. 各EntityをDespawn
        foreach (var handle in markedEntities)
        {
            _despawner.Despawn(handle);
        }

        // 3. レジストリから削除
        _entityRegistry.ProcessDeletions();
    }
}

/// <summary>
/// Entityを実際に削除するインターフェース。
/// </summary>
public interface IEntityDespawner
{
    /// <summary>
    /// Entityを削除する。
    /// </summary>
    /// <param name="handle">EntityのAnyHandle</param>
    void Despawn(AnyHandle handle);
}
