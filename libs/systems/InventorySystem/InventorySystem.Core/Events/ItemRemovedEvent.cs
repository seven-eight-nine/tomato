namespace Tomato.InventorySystem;

/// <summary>
/// アイテムが削除されたときのイベント情報。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public readonly struct ItemRemovedEvent<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>削除元インベントリのID</summary>
    public readonly InventoryId InventoryId;

    /// <summary>削除されたアイテム</summary>
    public readonly TItem Item;

    /// <summary>削除された数量</summary>
    public readonly int RemovedCount;

    /// <summary>削除理由</summary>
    public readonly RemoveReason Reason;

    public ItemRemovedEvent(InventoryId inventoryId, TItem item, int removedCount, RemoveReason reason)
    {
        InventoryId = inventoryId;
        Item = item;
        RemovedCount = removedCount;
        Reason = reason;
    }

    public override string ToString() =>
        $"ItemRemovedEvent(Inventory={InventoryId}, Item={Item.InstanceId}, Count={RemovedCount}, Reason={Reason})";
}

/// <summary>
/// アイテム削除の理由。
/// </summary>
public enum RemoveReason
{
    /// <summary>手動削除</summary>
    Manual,
    /// <summary>使用</summary>
    Used,
    /// <summary>転送</summary>
    Transfer,
    /// <summary>破棄</summary>
    Discard,
    /// <summary>クリア</summary>
    Clear,
    /// <summary>システム削除</summary>
    System
}
