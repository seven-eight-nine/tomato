namespace Tomato.InventorySystem;

/// <summary>
/// バリデーション失敗の理由を表す。
/// </summary>
public readonly struct ValidationFailureReason
{
    /// <summary>失敗理由のコード</summary>
    public readonly ValidationFailureCode Code;

    /// <summary>詳細メッセージ</summary>
    public readonly string? Message;

    /// <summary>関連するアイテムのインスタンスID</summary>
    public readonly ItemInstanceId? RelatedItemId;

    public ValidationFailureReason(ValidationFailureCode code, string? message = null, ItemInstanceId? relatedItemId = null)
    {
        Code = code;
        Message = message;
        RelatedItemId = relatedItemId;
    }

    public override string ToString() =>
        Message != null ? $"{Code}: {Message}" : Code.ToString();
}

/// <summary>
/// バリデーション失敗理由のコード。
/// </summary>
public enum ValidationFailureCode
{
    /// <summary>容量超過</summary>
    CapacityExceeded,
    /// <summary>アイテムの種類が無効</summary>
    InvalidItemType,
    /// <summary>スタック数が無効</summary>
    InvalidStackCount,
    /// <summary>アイテムが見つからない</summary>
    ItemNotFound,
    /// <summary>数量不足</summary>
    InsufficientQuantity,
    /// <summary>転送先が満杯</summary>
    DestinationFull,
    /// <summary>転送不可</summary>
    TransferNotAllowed,
    /// <summary>カスタムバリデーション失敗</summary>
    Custom
}
