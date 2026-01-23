namespace Tomato.InventorySystem;

/// <summary>
/// アイテム転送操作のコンテキスト情報。
/// </summary>
public readonly struct TransferContext : ITransferContext
{
    /// <summary>転送元インベントリのID</summary>
    public InventoryId SourceId { get; }

    /// <summary>転送先インベントリのID</summary>
    public InventoryId DestinationId { get; }

    /// <summary>転送するアイテムのインスタンスID</summary>
    public ItemInstanceId ItemInstanceId { get; }

    /// <summary>転送する数量</summary>
    public int Count { get; }

    /// <summary>カスタムデータ（ゲーム固有の追加情報）</summary>
    public readonly object? CustomData;

    public TransferContext(
        InventoryId sourceId,
        InventoryId destinationId,
        ItemInstanceId itemInstanceId,
        int count,
        object? customData = null)
    {
        SourceId = sourceId;
        DestinationId = destinationId;
        ItemInstanceId = itemInstanceId;
        Count = count;
        CustomData = customData;
    }

    public override string ToString() =>
        $"TransferContext(Source={SourceId}, Dest={DestinationId}, Item={ItemInstanceId}, Count={Count})";
}
