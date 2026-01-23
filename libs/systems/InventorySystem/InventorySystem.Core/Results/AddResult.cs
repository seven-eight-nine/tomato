namespace Tomato.InventorySystem;

/// <summary>
/// アイテム追加操作の結果を表す。
/// </summary>
public readonly struct AddResult
{
    /// <summary>追加が成功したかどうか</summary>
    public readonly bool Success;

    /// <summary>追加されたアイテムのインスタンスID</summary>
    public readonly ItemInstanceId ItemInstanceId;

    /// <summary>失敗理由（成功時はnull）</summary>
    public readonly IValidationResult? ValidationResult;

    private AddResult(bool success, ItemInstanceId itemInstanceId, IValidationResult? validationResult)
    {
        Success = success;
        ItemInstanceId = itemInstanceId;
        ValidationResult = validationResult;
    }

    /// <summary>成功結果を作成する</summary>
    public static AddResult Succeeded(ItemInstanceId itemInstanceId) =>
        new(true, itemInstanceId, null);

    /// <summary>失敗結果を作成する</summary>
    public static AddResult Failed(IValidationResult validationResult) =>
        new(false, ItemInstanceId.Invalid, validationResult);

    public override string ToString() =>
        Success ? $"AddResult(Success, {ItemInstanceId})" : $"AddResult(Failed, {ValidationResult})";
}
