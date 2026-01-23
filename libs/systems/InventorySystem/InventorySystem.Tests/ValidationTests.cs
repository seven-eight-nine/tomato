using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class ValidationTests
{
    private static SimpleInventory<TestItem> CreateInventory(int capacity = 10, IInventoryValidator<TestItem>? validator = null)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(1),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true),
            validator);
    }

    [Fact]
    public void AlwaysAllowValidator_ShouldAllowAllOperations()
    {
        // SimpleInventory has internal capacity, so we need to set capacity to 20
        var inventory = CreateInventory(capacity: 20, validator: AlwaysAllowValidator<TestItem>.Instance);

        for (int i = 0; i < 20; i++)
        {
            inventory.AddUnchecked(new TestItem(1, $"Item{i}"));
        }

        Assert.Equal(20, inventory.Count);
    }

    [Fact]
    public void CapacityValidator_ShouldBlockWhenFull()
    {
        var validator = new CapacityValidator<TestItem>(5);
        var inventory = CreateInventory(capacity: 100, validator: validator);

        for (int i = 0; i < 5; i++)
        {
            var result = inventory.TryAdd(new TestItem(1, $"Item{i}"));
            Assert.True(result.Success);
        }

        var failResult = inventory.TryAdd(new TestItem(1, "Extra"));
        Assert.False(failResult.Success);
    }

    [Fact]
    public void CompositeValidator_ShouldCombineValidators()
    {
        var composite = new CompositeValidator<TestItem>()
            .Add(new CapacityValidator<TestItem>(3))
            .Add(new TestTypeValidator(allowedDefinitionId: 1));

        var inventory = CreateInventory(capacity: 100, validator: composite);

        var result1 = inventory.TryAdd(new TestItem(1, "Allowed"));
        Assert.True(result1.Success);

        var result2 = inventory.TryAdd(new TestItem(2, "NotAllowed"));
        Assert.False(result2.Success);
    }

    [Fact]
    public void CompositeValidator_ShouldCollectAllFailureReasons()
    {
        var composite = new CompositeValidator<TestItem>()
            .Add(new CapacityValidator<TestItem>(0))
            .Add(new TestTypeValidator(allowedDefinitionId: 1));

        var inventory = CreateInventory(capacity: 100, validator: composite);
        var item = new TestItem(2, "Invalid");

        var result = inventory.TryAdd(item);

        Assert.False(result.Success);
        Assert.NotNull(result.ValidationResult);
        Assert.True(result.ValidationResult.FailureReasons.Count >= 2);
    }

    [Fact]
    public void ValidationResult_Success_ShouldBeValid()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void ValidationResult_Fail_ShouldHaveReasons()
    {
        var result = ValidationResult.Fail(ValidationFailureCode.CapacityExceeded, "Test message");

        Assert.False(result.IsValid);
        Assert.Single(result.FailureReasons);
        Assert.Equal(ValidationFailureCode.CapacityExceeded, result.FailureReasons[0].Code);
    }

    private class TestTypeValidator : IInventoryValidator<TestItem>
    {
        private readonly int _allowedDefinitionId;

        public TestTypeValidator(int allowedDefinitionId)
        {
            _allowedDefinitionId = allowedDefinitionId;
        }

        public IValidationResult ValidateAdd(IInventory<TestItem> inventory, TestItem item, AddContext context)
        {
            if (item.DefinitionId.Value != _allowedDefinitionId)
            {
                return ValidationResult.Fail(ValidationFailureCode.InvalidItemType, $"Only definition {_allowedDefinitionId} allowed");
            }
            return ValidationResult.Success();
        }

        public IValidationResult ValidateRemove(IInventory<TestItem> inventory, TestItem item, int count) =>
            ValidationResult.Success();

        public IValidationResult ValidateTransfer(IInventory<TestItem> source, IInventory<TestItem> dest, TestItem item, int count) =>
            ValidationResult.Success();
    }
}
