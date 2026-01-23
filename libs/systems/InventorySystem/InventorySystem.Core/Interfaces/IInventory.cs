using System;
using System.Collections.Generic;
using Tomato.SerializationSystem;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリのインターフェース。
/// アイテムの追加、削除、クエリ操作を提供する。
/// </summary>
/// <typeparam name="TItem">格納するアイテムの型</typeparam>
public interface IInventory<TItem> : ISerializable, ISnapshotable<IInventory<TItem>>
    where TItem : class, IInventoryItem
{
    #region Properties

    /// <summary>インベントリのID</summary>
    InventoryId Id { get; }

    /// <summary>現在格納されているアイテムの数</summary>
    int Count { get; }

    /// <summary>空きスペースがあるかどうか</summary>
    bool HasSpace { get; }

    #endregion

    #region Add

    /// <summary>アイテムを追加可能かどうかを確認する</summary>
    IValidationResult CanAdd(TItem item, AddContext? context = null);

    /// <summary>アイテムの追加を試みる</summary>
    AddResult TryAdd(TItem item, AddContext? context = null);

    /// <summary>バリデーションをスキップしてアイテムを追加する</summary>
    void AddUnchecked(TItem item);

    #endregion

    #region Remove

    /// <summary>アイテムを削除可能かどうかを確認する</summary>
    IValidationResult CanRemove(ItemInstanceId instanceId, int count = 1);

    /// <summary>指定したインスタンスIDのアイテムの削除を試みる</summary>
    RemoveResult TryRemove(ItemInstanceId instanceId, int count = 1);

    /// <summary>条件に一致するアイテムをすべて削除する</summary>
    int RemoveWhere(Func<TItem, bool> predicate);

    /// <summary>すべてのアイテムを削除する</summary>
    void Clear();

    #endregion

    #region Query

    /// <summary>指定したインスタンスIDのアイテムを取得する</summary>
    TItem? Get(ItemInstanceId instanceId);

    /// <summary>指定した定義IDのアイテムをすべて取得する</summary>
    IEnumerable<TItem> GetByDefinition(ItemDefinitionId definitionId);

    /// <summary>すべてのアイテムを取得する</summary>
    IEnumerable<TItem> GetAll();

    /// <summary>指定したインスタンスIDのアイテムが存在するかどうか</summary>
    bool Contains(ItemInstanceId instanceId);

    /// <summary>指定した定義IDのアイテムの合計スタック数を取得する</summary>
    int GetTotalStackCount(ItemDefinitionId definitionId);

    #endregion

    #region Events

    /// <summary>アイテムが追加されたときに発火するイベント</summary>
    event Action<ItemAddedEvent<TItem>>? OnItemAdded;

    /// <summary>アイテムが削除されたときに発火するイベント</summary>
    event Action<ItemRemovedEvent<TItem>>? OnItemRemoved;

    /// <summary>アイテムのスタック数が変化したときに発火するイベント</summary>
    event Action<ItemStackChangedEvent<TItem>>? OnItemStackChanged;

    #endregion
}
