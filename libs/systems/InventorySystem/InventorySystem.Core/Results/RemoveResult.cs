namespace Tomato.InventorySystem;

/// <summary>
/// アイテム削除操作の結果を表す。
/// </summary>
public readonly struct RemoveResult
{
    /// <summary>削除が成功したかどうか</summary>
    public readonly bool Success;

    /// <summary>実際に削除された数量</summary>
    public readonly int RemovedCount;

    /// <summary>削除されたアイテム（削除されなかった場合はnull）</summary>
    public readonly IInventoryItem? RemovedItem;

    /// <summary>失敗理由</summary>
    public readonly RemoveFailureReason FailureReason;

    private RemoveResult(bool success, int removedCount, IInventoryItem? removedItem, RemoveFailureReason failureReason)
    {
        Success = success;
        RemovedCount = removedCount;
        RemovedItem = removedItem;
        FailureReason = failureReason;
    }

    /// <summary>成功結果を作成する</summary>
    public static RemoveResult Succeeded(int removedCount, IInventoryItem removedItem) =>
        new(true, removedCount, removedItem, RemoveFailureReason.None);

    /// <summary>アイテムが見つからなかった場合の失敗結果</summary>
    public static RemoveResult NotFound() =>
        new(false, 0, null, RemoveFailureReason.ItemNotFound);

    /// <summary>数量が不足している場合の失敗結果</summary>
    public static RemoveResult InsufficientQuantity(int available) =>
        new(false, 0, null, RemoveFailureReason.InsufficientQuantity);

    /// <summary>バリデーションに失敗した場合の失敗結果</summary>
    public static RemoveResult ValidationFailed() =>
        new(false, 0, null, RemoveFailureReason.ValidationFailed);

    public override string ToString() =>
        Success ? $"RemoveResult(Success, Removed={RemovedCount})" : $"RemoveResult(Failed, {FailureReason})";
}

/// <summary>
/// 削除失敗の理由。
/// </summary>
public enum RemoveFailureReason
{
    /// <summary>失敗なし（成功時）</summary>
    None,
    /// <summary>アイテムが見つからなかった</summary>
    ItemNotFound,
    /// <summary>数量が不足している</summary>
    InsufficientQuantity,
    /// <summary>バリデーションに失敗した</summary>
    ValidationFailed
}
