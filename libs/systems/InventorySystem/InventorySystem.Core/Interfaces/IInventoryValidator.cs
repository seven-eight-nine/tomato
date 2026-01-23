namespace Tomato.InventorySystem;

/// <summary>
/// インベントリ操作のバリデーションを行うインターフェース。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public interface IInventoryValidator<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>アイテム追加のバリデーション</summary>
    IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context);

    /// <summary>アイテム削除のバリデーション</summary>
    IValidationResult ValidateRemove(IInventory<TItem> inventory, TItem item, int count);

    /// <summary>アイテム転送のバリデーション</summary>
    IValidationResult ValidateTransfer(IInventory<TItem> source, IInventory<TItem> dest, TItem item, int count);
}
