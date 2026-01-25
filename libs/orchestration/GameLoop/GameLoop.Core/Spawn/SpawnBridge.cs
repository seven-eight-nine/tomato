using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.CharacterSpawnSystem;

namespace Tomato.GameLoop.Spawn;

/// <summary>
/// CharacterSpawnSystemとEntityContextRegistryを接続するブリッジ。
/// CharacterSpawnControllerのStateChangedイベントを監視し、
/// Entity登録/削除を行う。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class SpawnBridge<TCategory> : ISpawnCompletionHandler
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _registry;
    private readonly IEntitySpawner _arena;
    private readonly IEntityInitializer<TCategory> _initializer;
    private readonly Dictionary<string, AnyHandle> _characterIdToHandle;

    /// <summary>
    /// SpawnBridgeを生成する。
    /// </summary>
    public SpawnBridge(
        EntityContextRegistry<TCategory> registry,
        IEntitySpawner arena,
        IEntityInitializer<TCategory> initializer)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _arena = arena ?? throw new ArgumentNullException(nameof(arena));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        _characterIdToHandle = new Dictionary<string, AnyHandle>();
    }

    /// <summary>
    /// CharacterSpawnControllerをこのブリッジに接続する。
    /// </summary>
    /// <param name="controller">接続するコントローラー</param>
    public void Connect(CharacterSpawnController controller)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        controller.StateChanged += OnStateChanged;
    }

    /// <summary>
    /// CharacterSpawnControllerをこのブリッジから切断する。
    /// </summary>
    /// <param name="controller">切断するコントローラー</param>
    public void Disconnect(CharacterSpawnController controller)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        controller.StateChanged -= OnStateChanged;
    }

    /// <summary>
    /// キャラクターIDからAnyHandleを取得する。
    /// </summary>
    public AnyHandle? GetHandle(string characterId)
    {
        return _characterIdToHandle.TryGetValue(characterId, out var handle) ? handle : null;
    }

    /// <summary>
    /// AnyHandleからEntityContextを取得する。
    /// </summary>
    public EntityContext<TCategory>? GetContext(string characterId)
    {
        if (!_characterIdToHandle.TryGetValue(characterId, out var handle))
            return null;

        return _registry.GetContext(handle);
    }

    // ========================================
    // ISpawnCompletionHandler 実装
    // ========================================

    /// <summary>
    /// キャラクターがアクティブになった時に呼ばれる。
    /// </summary>
    public void OnCharacterActivated(CharacterSpawnController controller)
    {
        if (_characterIdToHandle.ContainsKey(controller.CharacterId))
        {
            // 既に登録済みの場合は再アクティブ化
            var existingHandle = _characterIdToHandle[controller.CharacterId];
            if (_registry.TryGetContext(existingHandle, out var existingContext) && existingContext != null)
            {
                existingContext.IsActive = true;
            }
            return;
        }

        // 1. EntityArenaでEntityをSpawn
        var handle = _arena.Spawn();

        // 2. EntityContextを登録
        var context = _registry.Register(handle);
        context.SpawnController = controller;
        context.IsActive = true;

        // 3. データリソースからEntity初期化
        _initializer.Initialize(context, controller.CharacterId, controller.LoadedDataResource);

        // 4. マッピングを保存
        _characterIdToHandle[controller.CharacterId] = handle;
    }

    /// <summary>
    /// キャラクターが非アクティブになった時に呼ばれる。
    /// </summary>
    public void OnCharacterDeactivated(CharacterSpawnController controller)
    {
        if (_characterIdToHandle.TryGetValue(controller.CharacterId, out var handle))
        {
            if (_registry.TryGetContext(handle, out var context) && context != null)
            {
                context.IsActive = false;
            }
        }
    }

    /// <summary>
    /// キャラクターが完全に削除された時に呼ばれる。
    /// </summary>
    public void OnCharacterRemoved(string characterId)
    {
        if (_characterIdToHandle.TryGetValue(characterId, out var handle))
        {
            _registry.MarkForDeletion(handle);
            _characterIdToHandle.Remove(characterId);
        }
    }

    // ========================================
    // 内部メソッド
    // ========================================

    private void OnStateChanged(object sender, StateChangedEventArgs args)
    {
        var controller = (CharacterSpawnController)sender;

        if (args.NewState == CharacterInternalState.InstantiatedActive)
        {
            OnCharacterActivated(controller);
        }
        else if (args.OldState == CharacterInternalState.InstantiatedActive &&
                 args.NewState == CharacterInternalState.InstantiatedInactive)
        {
            OnCharacterDeactivated(controller);
        }
        else if (args.NewState == CharacterInternalState.NotPlaced)
        {
            OnCharacterRemoved(controller.CharacterId);
        }
    }
}
