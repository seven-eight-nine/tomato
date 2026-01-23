namespace Tomato.InventorySystem;

/// <summary>
/// 容量制限を検証するバリデータ（参考実装）。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class CapacityValidator<TItem> : IInventoryValidator<TItem>
    where TItem : class, IInventoryItem
{
    private readonly int _maxCapacity;

    /// <summary>
    /// 容量バリデータを作成する。
    /// </summary>
    /// <param name="maxCapacity">最大容量</param>
    public CapacityValidator(int maxCapacity)
    {
        _maxCapacity = maxCapacity;
    }

    public IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context)
    {
        if (inventory.Count >= _maxCapacity)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.CapacityExceeded,
                $"Inventory is full (capacity: {_maxCapacity})");
        }

        return ValidationResult.Success();
    }

    public IValidationResult ValidateRemove(IInventory<TItem> inventory, TItem item, int count)
    {
        return ValidationResult.Success();
    }

    public IValidationResult ValidateTransfer(IInventory<TItem> source, IInventory<TItem> dest, TItem item, int count)
    {
        if (dest.Count >= _maxCapacity)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.DestinationFull,
                $"Destination inventory is full (capacity: {_maxCapacity})");
        }

        return ValidationResult.Success();
    }
}
