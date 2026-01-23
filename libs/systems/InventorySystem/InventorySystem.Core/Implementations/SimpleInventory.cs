using System.Collections.Generic;
using Tomato.SerializationSystem;

namespace Tomato.InventorySystem;

/// <summary>
/// シンプルなリスト型インベントリの参考実装。
/// 容量制限のみで、スロット概念なし。
/// </summary>
/// <typeparam name="TItem">格納するアイテムの型</typeparam>
public class SimpleInventory<TItem> : InventoryBase<TItem>
    where TItem : class, IInventoryItem
{
    private readonly Dictionary<ItemInstanceId, TItem> _items;
    private readonly int _capacity;
    private readonly ItemDeserializerDelegate<TItem> _itemFactory;

    /// <summary>
    /// SimpleInventoryを作成する。
    /// </summary>
    /// <param name="id">インベントリのID</param>
    /// <param name="capacity">最大容量</param>
    /// <param name="itemFactory">デシリアライズ時にアイテムを生成するファクトリ</param>
    /// <param name="validator">バリデータ（null時は容量バリデータを使用）</param>
    public SimpleInventory(
        InventoryId id,
        int capacity,
        ItemDeserializerDelegate<TItem> itemFactory,
        IInventoryValidator<TItem>? validator = null)
        : base(id, validator ?? new CapacityValidator<TItem>(capacity))
    {
        _items = new Dictionary<ItemInstanceId, TItem>();
        _capacity = capacity;
        _itemFactory = itemFactory;
    }

    protected override bool HasSpaceCore => _items.Count < _capacity;

    protected override int GetCountCore() => _items.Count;

    protected override AddResult AddCore(TItem item, AddContext context)
    {
        if (_items.Count >= _capacity)
        {
            return AddResult.Failed(ValidationResult.Fail(ValidationFailureCode.CapacityExceeded));
        }

        _items[item.InstanceId] = item;
        return AddResult.Succeeded(item.InstanceId);
    }

    protected override RemoveResult RemoveCore(ItemInstanceId instanceId, int count)
    {
        if (!_items.TryGetValue(instanceId, out var item))
        {
            return RemoveResult.NotFound();
        }

        if (item.StackCount < count)
        {
            return RemoveResult.InsufficientQuantity(item.StackCount);
        }

        if (item.StackCount == count)
        {
            _items.Remove(instanceId);
            return RemoveResult.Succeeded(count, item);
        }
        else
        {
            var previousCount = item.StackCount;
            item.StackCount -= count;
            RaiseStackChanged(item, previousCount, item.StackCount);
            var cloned = (TItem)item.Clone();
            cloned.StackCount = count;
            return RemoveResult.Succeeded(count, cloned);
        }
    }

    protected override TItem? GetCore(ItemInstanceId instanceId)
    {
        return _items.TryGetValue(instanceId, out var item) ? item : null;
    }

    protected override IEnumerable<TItem> GetAllCore()
    {
        return _items.Values;
    }

    protected override void ClearCore()
    {
        _items.Clear();
    }

    protected override TItem DeserializeItem(ref BinaryDeserializer deserializer)
    {
        return _itemFactory(ref deserializer);
    }

    public override void Serialize(BinarySerializer serializer)
    {
        serializer.Write(Id.Value);
        serializer.Write(_capacity);
        serializer.Write(_items.Count);
        foreach (var item in _items.Values)
        {
            item.Serialize(serializer);
        }
    }

    public override void Deserialize(ref BinaryDeserializer deserializer)
    {
        ClearCore();
        var _ = deserializer.ReadInt32(); // inventoryId (skip, already set)
        var capacity = deserializer.ReadInt32(); // capacity (skip, already set)
        var count = deserializer.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var item = DeserializeItem(ref deserializer);
            _items[item.InstanceId] = item;
        }
    }
}
