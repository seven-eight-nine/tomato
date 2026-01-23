using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// 複数のバリデータを組み合わせるバリデータ。
/// すべてのバリデータが成功した場合のみ成功とする。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class CompositeValidator<TItem> : IInventoryValidator<TItem>
    where TItem : class, IInventoryItem
{
    private readonly List<IInventoryValidator<TItem>> _validators;

    public CompositeValidator()
    {
        _validators = new List<IInventoryValidator<TItem>>();
    }

    public CompositeValidator(IEnumerable<IInventoryValidator<TItem>> validators)
    {
        _validators = new List<IInventoryValidator<TItem>>(validators);
    }

    /// <summary>バリデータを追加する</summary>
    public CompositeValidator<TItem> Add(IInventoryValidator<TItem> validator)
    {
        _validators.Add(validator);
        return this;
    }

    public IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context)
    {
        var failureReasons = new List<ValidationFailureReason>();

        foreach (var validator in _validators)
        {
            var result = validator.ValidateAdd(inventory, item, context);
            if (!result.IsValid)
            {
                failureReasons.AddRange(result.FailureReasons);
            }
        }

        return failureReasons.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Fail(failureReasons);
    }

    public IValidationResult ValidateRemove(IInventory<TItem> inventory, TItem item, int count)
    {
        var failureReasons = new List<ValidationFailureReason>();

        foreach (var validator in _validators)
        {
            var result = validator.ValidateRemove(inventory, item, count);
            if (!result.IsValid)
            {
                failureReasons.AddRange(result.FailureReasons);
            }
        }

        return failureReasons.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Fail(failureReasons);
    }

    public IValidationResult ValidateTransfer(IInventory<TItem> source, IInventory<TItem> dest, TItem item, int count)
    {
        var failureReasons = new List<ValidationFailureReason>();

        foreach (var validator in _validators)
        {
            var result = validator.ValidateTransfer(source, dest, item, count);
            if (!result.IsValid)
            {
                failureReasons.AddRange(result.FailureReasons);
            }
        }

        return failureReasons.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Fail(failureReasons);
    }
}
