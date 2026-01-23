namespace Tomato.InventorySystem;

/// <summary>
/// 転送操作のコンテキスト情報を提供するインターフェース。
/// </summary>
public interface ITransferContext
{
    /// <summary>転送元インベントリのID</summary>
    InventoryId SourceId { get; }

    /// <summary>転送先インベントリのID</summary>
    InventoryId DestinationId { get; }

    /// <summary>転送するアイテムのインスタンスID</summary>
    ItemInstanceId ItemInstanceId { get; }

    /// <summary>転送する数量</summary>
    int Count { get; }
}
