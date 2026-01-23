using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class TransferTests
{
    private static SimpleInventory<TestItem> CreateInventory(int id, int capacity = 10)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(id),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    [Fact]
    public void Transfer_WhenValid_ShouldSucceed()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, item.InstanceId);

        Assert.True(result.Success);
        Assert.Equal(0, source.Count);
        Assert.Equal(1, dest.Count);
    }

    [Fact]
    public void Transfer_PartialStack_ShouldTransferRequestedAmount()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Potion", stackCount: 10);
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, item.InstanceId, count: 3);

        Assert.True(result.Success);
        Assert.Equal(3, result.TransferredCount);
        var remaining = source.Get(item.InstanceId);
        Assert.NotNull(remaining);
        Assert.Equal(7, remaining.StackCount);
        Assert.Equal(1, dest.Count);
    }

    [Fact]
    public void Transfer_WhenSourceItemNotFound_ShouldFail()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, new ItemInstanceId(999));

        Assert.False(result.Success);
        Assert.Equal(TransferFailureReason.SourceItemNotFound, result.FailureReason);
    }

    [Fact]
    public void Transfer_WhenInsufficientQuantity_ShouldFail()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Potion", stackCount: 5);
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, item.InstanceId, count: 10);

        Assert.False(result.Success);
        Assert.Equal(TransferFailureReason.InsufficientQuantity, result.FailureReason);
    }

    [Fact]
    public void Transfer_WhenDestinationFull_ShouldFail()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2, capacity: 0);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, item.InstanceId);

        Assert.False(result.Success);
        Assert.Equal(TransferFailureReason.DestinationFull, result.FailureReason);
        Assert.Equal(1, source.Count);
    }

    [Fact]
    public void TransferAll_ShouldTransferEntireStack()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Potion", stackCount: 50);
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransferAll(source, dest, item.InstanceId);

        Assert.True(result.Success);
        Assert.Equal(50, result.TransferredCount);
        Assert.Equal(0, source.Count);
        Assert.Equal(1, dest.Count);
    }

    [Fact]
    public void CanTransfer_WhenValid_ShouldReturnTrue()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var canTransfer = manager.CanTransfer(source, dest, item.InstanceId);

        Assert.True(canTransfer);
    }

    [Fact]
    public void CanTransfer_WhenInvalid_ShouldReturnFalse()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2, capacity: 0);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var manager = new TransferManager<TestItem>();
        var canTransfer = manager.CanTransfer(source, dest, item.InstanceId);

        Assert.False(canTransfer);
    }

    [Fact]
    public void Transfer_WithContext_ShouldWork()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var context = new TransferContext(source.Id, dest.Id, item.InstanceId, 1);
        var manager = new TransferManager<TestItem>();
        var result = manager.TryTransfer(source, dest, context);

        Assert.True(result.Success);
    }

    [Fact]
    public void Transfer_WithGlobalValidator_ShouldValidate()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2);
        var item = new TestItem(1, "Sword");
        source.TryAdd(item);

        var blockingValidator = new BlockAllTransfersValidator();
        var manager = new TransferManager<TestItem>(blockingValidator);
        var result = manager.TryTransfer(source, dest, item.InstanceId);

        Assert.False(result.Success);
        Assert.Equal(TransferFailureReason.ValidationFailed, result.FailureReason);
        Assert.Equal(1, source.Count);
    }

    private class BlockAllTransfersValidator : IInventoryValidator<TestItem>
    {
        public IValidationResult ValidateAdd(IInventory<TestItem> inventory, TestItem item, AddContext context) =>
            ValidationResult.Success();

        public IValidationResult ValidateRemove(IInventory<TestItem> inventory, TestItem item, int count) =>
            ValidationResult.Success();

        public IValidationResult ValidateTransfer(IInventory<TestItem> source, IInventory<TestItem> dest, TestItem item, int count) =>
            ValidationResult.Fail(ValidationFailureCode.TransferNotAllowed, "Transfers are blocked");
    }
}
