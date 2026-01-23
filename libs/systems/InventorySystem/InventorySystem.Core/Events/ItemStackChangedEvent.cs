namespace Tomato.InventorySystem;

/// <summary>
/// アイテムのスタック数が変化したときのイベント情報。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public readonly struct ItemStackChangedEvent<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>インベントリのID</summary>
    public readonly InventoryId InventoryId;

    /// <summary>スタック数が変化したアイテム</summary>
    public readonly TItem Item;

    /// <summary>変化前のスタック数</summary>
    public readonly int PreviousStackCount;

    /// <summary>変化後のスタック数</summary>
    public readonly int NewStackCount;

    /// <summary>変化量（正の値は増加、負の値は減少）</summary>
    public int Delta => NewStackCount - PreviousStackCount;

    public ItemStackChangedEvent(InventoryId inventoryId, TItem item, int previousStackCount, int newStackCount)
    {
        InventoryId = inventoryId;
        Item = item;
        PreviousStackCount = previousStackCount;
        NewStackCount = newStackCount;
    }

    public override string ToString() =>
        $"ItemStackChangedEvent(Inventory={InventoryId}, Item={Item.InstanceId}, {PreviousStackCount}->{NewStackCount})";
}
