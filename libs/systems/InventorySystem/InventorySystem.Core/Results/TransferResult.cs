namespace Tomato.InventorySystem;

/// <summary>
/// アイテム転送操作の結果を表す。
/// </summary>
public readonly struct TransferResult
{
    /// <summary>転送が成功したかどうか</summary>
    public readonly bool Success;

    /// <summary>転送されたアイテムのインスタンスID</summary>
    public readonly ItemInstanceId TransferredItemId;

    /// <summary>転送された数量</summary>
    public readonly int TransferredCount;

    /// <summary>失敗理由</summary>
    public readonly TransferFailureReason FailureReason;

    /// <summary>バリデーション結果（失敗時のみ）</summary>
    public readonly IValidationResult? ValidationResult;

    private TransferResult(
        bool success,
        ItemInstanceId transferredItemId,
        int transferredCount,
        TransferFailureReason failureReason,
        IValidationResult? validationResult)
    {
        Success = success;
        TransferredItemId = transferredItemId;
        TransferredCount = transferredCount;
        FailureReason = failureReason;
        ValidationResult = validationResult;
    }

    /// <summary>成功結果を作成する</summary>
    public static TransferResult Succeeded(ItemInstanceId itemId, int count) =>
        new(true, itemId, count, TransferFailureReason.None, null);

    /// <summary>転送元にアイテムがない場合の失敗結果</summary>
    public static TransferResult SourceItemNotFound() =>
        new(false, ItemInstanceId.Invalid, 0, TransferFailureReason.SourceItemNotFound, null);

    /// <summary>数量が不足している場合の失敗結果</summary>
    public static TransferResult InsufficientQuantity() =>
        new(false, ItemInstanceId.Invalid, 0, TransferFailureReason.InsufficientQuantity, null);

    /// <summary>バリデーションに失敗した場合の失敗結果</summary>
    public static TransferResult ValidationFailed(IValidationResult validationResult) =>
        new(false, ItemInstanceId.Invalid, 0, TransferFailureReason.ValidationFailed, validationResult);

    /// <summary>転送先に空きがない場合の失敗結果</summary>
    public static TransferResult DestinationFull() =>
        new(false, ItemInstanceId.Invalid, 0, TransferFailureReason.DestinationFull, null);

    public override string ToString() =>
        Success
            ? $"TransferResult(Success, ItemId={TransferredItemId}, Count={TransferredCount})"
            : $"TransferResult(Failed, {FailureReason})";
}

/// <summary>
/// 転送失敗の理由。
/// </summary>
public enum TransferFailureReason
{
    /// <summary>失敗なし（成功時）</summary>
    None,
    /// <summary>転送元にアイテムが見つからなかった</summary>
    SourceItemNotFound,
    /// <summary>数量が不足している</summary>
    InsufficientQuantity,
    /// <summary>バリデーションに失敗した</summary>
    ValidationFailed,
    /// <summary>転送先に空きがない</summary>
    DestinationFull
}
