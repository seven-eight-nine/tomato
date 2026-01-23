using System;
using System.Collections.Generic;
using Tomato.SerializationSystem;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリの基底抽象クラス。
/// 派生クラスは抽象メソッドを実装することで独自のストレージ構造を持てる。
/// </summary>
/// <typeparam name="TItem">格納するアイテムの型</typeparam>
public abstract class InventoryBase<TItem> : IInventory<TItem>
    where TItem : class, IInventoryItem
{
    private static long _snapshotIdCounter;

    private readonly IInventoryValidator<TItem> _validator;

    /// <summary>インベントリのID</summary>
    public InventoryId Id { get; }

    /// <summary>現在格納されているアイテムの数</summary>
    public int Count => GetCountCore();

    /// <summary>空きスペースがあるかどうか</summary>
    public bool HasSpace => HasSpaceCore;

    #region Events

    public event Action<ItemAddedEvent<TItem>>? OnItemAdded;
    public event Action<ItemRemovedEvent<TItem>>? OnItemRemoved;
    public event Action<ItemStackChangedEvent<TItem>>? OnItemStackChanged;

    #endregion

    protected InventoryBase(InventoryId id, IInventoryValidator<TItem>? validator = null)
    {
        Id = id;
        _validator = validator ?? AlwaysAllowValidator<TItem>.Instance;
    }

    #region Abstract Methods

    /// <summary>アイテムを追加する内部実装</summary>
    protected abstract AddResult AddCore(TItem item, AddContext context);

    /// <summary>アイテムを削除する内部実装</summary>
    protected abstract RemoveResult RemoveCore(ItemInstanceId instanceId, int count);

    /// <summary>アイテムを取得する内部実装</summary>
    protected abstract TItem? GetCore(ItemInstanceId instanceId);

    /// <summary>すべてのアイテムを取得する内部実装</summary>
    protected abstract IEnumerable<TItem> GetAllCore();

    /// <summary>アイテム数を取得する内部実装</summary>
    protected abstract int GetCountCore();

    /// <summary>空きスペースがあるかどうかの内部実装</summary>
    protected abstract bool HasSpaceCore { get; }

    /// <summary>アイテムをデシリアライズする内部実装</summary>
    protected abstract TItem DeserializeItem(ref BinaryDeserializer deserializer);

    /// <summary>インベントリをクリアする内部実装</summary>
    protected abstract void ClearCore();

    #endregion

    #region Add

    public IValidationResult CanAdd(TItem item, AddContext? context = null)
    {
        var ctx = context ?? AddContext.Default;
        return _validator.ValidateAdd(this, item, ctx);
    }

    public AddResult TryAdd(TItem item, AddContext? context = null)
    {
        var ctx = context ?? AddContext.Default;
        var validation = _validator.ValidateAdd(this, item, ctx);
        if (!validation.IsValid)
        {
            return AddResult.Failed(validation);
        }

        var result = AddCore(item, ctx);
        if (result.Success)
        {
            RaiseItemAdded(item, ctx);
        }

        return result;
    }

    public void AddUnchecked(TItem item)
    {
        var ctx = AddContext.Default;
        var result = AddCore(item, ctx);
        if (result.Success)
        {
            RaiseItemAdded(item, ctx);
        }
    }

    #endregion

    #region Remove

    public IValidationResult CanRemove(ItemInstanceId instanceId, int count = 1)
    {
        var item = GetCore(instanceId);
        if (item == null)
        {
            return ValidationResult.Fail(ValidationFailureCode.ItemNotFound, "Item not found");
        }

        if (item.StackCount < count)
        {
            return ValidationResult.Fail(ValidationFailureCode.InsufficientQuantity,
                $"Insufficient quantity: has {item.StackCount}, requested {count}");
        }

        return _validator.ValidateRemove(this, item, count);
    }

    public RemoveResult TryRemove(ItemInstanceId instanceId, int count = 1)
    {
        var item = GetCore(instanceId);
        if (item == null)
        {
            return RemoveResult.NotFound();
        }

        if (item.StackCount < count)
        {
            return RemoveResult.InsufficientQuantity(item.StackCount);
        }

        var validation = _validator.ValidateRemove(this, item, count);
        if (!validation.IsValid)
        {
            return RemoveResult.ValidationFailed();
        }

        var result = RemoveCore(instanceId, count);
        if (result.Success && result.RemovedItem != null)
        {
            RaiseItemRemoved((TItem)result.RemovedItem, result.RemovedCount, RemoveReason.Manual);
        }

        return result;
    }

    public int RemoveWhere(Func<TItem, bool> predicate)
    {
        var toRemove = new List<ItemInstanceId>();
        foreach (var item in GetAllCore())
        {
            if (predicate(item))
            {
                toRemove.Add(item.InstanceId);
            }
        }

        int removedCount = 0;
        foreach (var instanceId in toRemove)
        {
            var item = GetCore(instanceId);
            if (item != null)
            {
                var result = RemoveCore(instanceId, item.StackCount);
                if (result.Success)
                {
                    removedCount++;
                    RaiseItemRemoved(item, item.StackCount, RemoveReason.Manual);
                }
            }
        }

        return removedCount;
    }

    public void Clear()
    {
        var items = new List<TItem>(GetAllCore());
        ClearCore();
        foreach (var item in items)
        {
            RaiseItemRemoved(item, item.StackCount, RemoveReason.Clear);
        }
    }

    #endregion

    #region Query

    public TItem? Get(ItemInstanceId instanceId) => GetCore(instanceId);

    public IEnumerable<TItem> GetByDefinition(ItemDefinitionId definitionId)
    {
        foreach (var item in GetAllCore())
        {
            if (item.DefinitionId == definitionId)
            {
                yield return item;
            }
        }
    }

    public IEnumerable<TItem> GetAll() => GetAllCore();

    public bool Contains(ItemInstanceId instanceId) => GetCore(instanceId) != null;

    public int GetTotalStackCount(ItemDefinitionId definitionId)
    {
        int total = 0;
        foreach (var item in GetByDefinition(definitionId))
        {
            total += item.StackCount;
        }
        return total;
    }

    #endregion

    #region Serialization

    public virtual void Serialize(BinarySerializer serializer)
    {
        serializer.Write(Id.Value);
        var items = new List<TItem>(GetAllCore());
        serializer.Write(items.Count);
        foreach (var item in items)
        {
            item.Serialize(serializer);
        }
    }

    public virtual void Deserialize(ref BinaryDeserializer deserializer)
    {
        ClearCore();
        var inventoryId = deserializer.ReadInt32();
        var count = deserializer.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var item = DeserializeItem(ref deserializer);
            AddCore(item, AddContext.Default);
        }
    }

    #endregion

    #region Snapshot

    public InventorySnapshot CreateSnapshot()
    {
        var serializer = new BinarySerializer();
        Serialize(serializer);
        var snapshotId = new SnapshotId(System.Threading.Interlocked.Increment(ref _snapshotIdCounter));
        return new InventorySnapshot(snapshotId, Id, serializer.ToArray());
    }

    public void RestoreFromSnapshot(InventorySnapshot snapshot)
    {
        var deserializer = new BinaryDeserializer(snapshot.Data);
        Deserialize(ref deserializer);
    }

    #endregion

    #region Event Helpers

    protected void RaiseItemAdded(TItem item, AddContext context)
    {
        OnItemAdded?.Invoke(new ItemAddedEvent<TItem>(Id, item, context));
    }

    protected void RaiseItemRemoved(TItem item, int count, RemoveReason reason)
    {
        OnItemRemoved?.Invoke(new ItemRemovedEvent<TItem>(Id, item, count, reason));
    }

    protected void RaiseStackChanged(TItem item, int previousCount, int newCount)
    {
        OnItemStackChanged?.Invoke(new ItemStackChangedEvent<TItem>(Id, item, previousCount, newCount));
    }

    #endregion
}
