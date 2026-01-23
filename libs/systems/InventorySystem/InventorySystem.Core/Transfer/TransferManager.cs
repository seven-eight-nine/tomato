namespace Tomato.InventorySystem;

/// <summary>
/// インベントリ間のアイテム転送を管理するマネージャー。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class TransferManager<TItem>
    where TItem : class, IInventoryItem
{
    private readonly IInventoryValidator<TItem>? _globalValidator;

    /// <summary>
    /// TransferManagerを作成する。
    /// </summary>
    /// <param name="globalValidator">グローバルバリデータ（すべての転送に適用）</param>
    public TransferManager(IInventoryValidator<TItem>? globalValidator = null)
    {
        _globalValidator = globalValidator;
    }

    /// <summary>
    /// アイテムを転送する。
    /// </summary>
    /// <param name="source">転送元インベントリ</param>
    /// <param name="destination">転送先インベントリ</param>
    /// <param name="itemInstanceId">転送するアイテムのインスタンスID</param>
    /// <param name="count">転送する数量</param>
    /// <returns>転送結果</returns>
    public TransferResult TryTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId,
        int count = 1)
    {
        var item = source.Get(itemInstanceId);
        if (item == null)
        {
            return TransferResult.SourceItemNotFound();
        }

        if (item.StackCount < count)
        {
            return TransferResult.InsufficientQuantity();
        }

        if (!destination.HasSpace)
        {
            return TransferResult.DestinationFull();
        }

        if (_globalValidator != null)
        {
            var validationResult = _globalValidator.ValidateTransfer(source, destination, item, count);
            if (!validationResult.IsValid)
            {
                return TransferResult.ValidationFailed(validationResult);
            }
        }

        var removeResult = source.TryRemove(itemInstanceId, count);
        if (!removeResult.Success)
        {
            return TransferResult.SourceItemNotFound();
        }

        TItem transferItem;
        if (count == item.StackCount)
        {
            transferItem = item;
        }
        else
        {
            transferItem = (TItem)item.Clone();
            transferItem.StackCount = count;
        }

        var addContext = new AddContext(AddSource.Transfer, true, null);
        var addResult = destination.TryAdd(transferItem, addContext);

        if (!addResult.Success)
        {
            source.AddUnchecked(item);
            if (addResult.ValidationResult != null)
            {
                return TransferResult.ValidationFailed(addResult.ValidationResult);
            }
            return TransferResult.DestinationFull();
        }

        return TransferResult.Succeeded(transferItem.InstanceId, count);
    }

    /// <summary>
    /// コンテキストを使用してアイテムを転送する。
    /// </summary>
    public TransferResult TryTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        TransferContext context)
    {
        return TryTransfer(source, destination, context.ItemInstanceId, context.Count);
    }

    /// <summary>
    /// アイテム全量を転送する。
    /// </summary>
    public TransferResult TryTransferAll(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId)
    {
        var item = source.Get(itemInstanceId);
        if (item == null)
        {
            return TransferResult.SourceItemNotFound();
        }

        return TryTransfer(source, destination, itemInstanceId, item.StackCount);
    }

    /// <summary>
    /// 転送をシミュレートする（実際の変更は行わない）。
    /// </summary>
    public bool CanTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId,
        int count = 1)
    {
        var item = source.Get(itemInstanceId);
        if (item == null)
        {
            return false;
        }

        if (item.StackCount < count)
        {
            return false;
        }

        if (!destination.HasSpace)
        {
            return false;
        }

        if (_globalValidator != null)
        {
            var validationResult = _globalValidator.ValidateTransfer(source, destination, item, count);
            if (!validationResult.IsValid)
            {
                return false;
            }
        }

        return true;
    }
}
