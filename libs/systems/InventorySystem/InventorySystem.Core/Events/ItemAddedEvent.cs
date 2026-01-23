namespace Tomato.InventorySystem;

/// <summary>
/// アイテムが追加されたときのイベント情報。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public readonly struct ItemAddedEvent<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>追加先インベントリのID</summary>
    public readonly InventoryId InventoryId;

    /// <summary>追加されたアイテム</summary>
    public readonly TItem Item;

    /// <summary>追加時のコンテキスト</summary>
    public readonly AddContext Context;

    public ItemAddedEvent(InventoryId inventoryId, TItem item, AddContext context)
    {
        InventoryId = inventoryId;
        Item = item;
        Context = context;
    }

    public override string ToString() =>
        $"ItemAddedEvent(Inventory={InventoryId}, Item={Item.InstanceId}, Source={Context.Source})";
}
