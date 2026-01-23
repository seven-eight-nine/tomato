namespace Tomato.InventorySystem;

/// <summary>
/// すべての操作を許可するバリデータ。
/// バリデーションを無効化したい場合や、テスト用途に使用する。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class AlwaysAllowValidator<TItem> : IInventoryValidator<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>シングルトンインスタンス</summary>
    public static readonly AlwaysAllowValidator<TItem> Instance = new();

    private AlwaysAllowValidator() { }

    public IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context) =>
        ValidationResult.Success();

    public IValidationResult ValidateRemove(IInventory<TItem> inventory, TItem item, int count) =>
        ValidationResult.Success();

    public IValidationResult ValidateTransfer(IInventory<TItem> source, IInventory<TItem> dest, TItem item, int count) =>
        ValidationResult.Success();
}
