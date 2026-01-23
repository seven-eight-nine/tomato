using System.Linq;
using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class SimpleInventoryTests
{
    private static SimpleInventory<TestItem> CreateInventory(int capacity = 10)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(1),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    [Fact]
    public void TryAdd_WhenEmpty_ShouldSucceed()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Sword");

        var result = inventory.TryAdd(item);

        Assert.True(result.Success);
        Assert.Equal(item.InstanceId, result.ItemInstanceId);
        Assert.Equal(1, inventory.Count);
    }

    [Fact]
    public void TryAdd_WhenFull_ShouldFail()
    {
        var inventory = CreateInventory(capacity: 2);
        inventory.TryAdd(new TestItem(1, "Item1"));
        inventory.TryAdd(new TestItem(2, "Item2"));

        var result = inventory.TryAdd(new TestItem(3, "Item3"));

        Assert.False(result.Success);
        Assert.Equal(2, inventory.Count);
    }

    [Fact]
    public void TryRemove_WhenItemExists_ShouldSucceed()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        var result = inventory.TryRemove(item.InstanceId);

        Assert.True(result.Success);
        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void TryRemove_WhenItemNotExists_ShouldFail()
    {
        var inventory = CreateInventory();

        var result = inventory.TryRemove(new ItemInstanceId(999));

        Assert.False(result.Success);
        Assert.Equal(RemoveFailureReason.ItemNotFound, result.FailureReason);
    }

    [Fact]
    public void TryRemove_PartialStack_ShouldDecrementStackCount()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Potion", stackCount: 5);
        inventory.TryAdd(item);

        var result = inventory.TryRemove(item.InstanceId, count: 3);

        Assert.True(result.Success);
        Assert.Equal(3, result.RemovedCount);
        var remaining = inventory.Get(item.InstanceId);
        Assert.NotNull(remaining);
        Assert.Equal(2, remaining.StackCount);
    }

    [Fact]
    public void Get_WhenItemExists_ShouldReturnItem()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Shield");
        inventory.TryAdd(item);

        var retrieved = inventory.Get(item.InstanceId);

        Assert.NotNull(retrieved);
        Assert.Equal(item.InstanceId, retrieved.InstanceId);
    }

    [Fact]
    public void Get_WhenItemNotExists_ShouldReturnNull()
    {
        var inventory = CreateInventory();

        var retrieved = inventory.Get(new ItemInstanceId(999));

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetByDefinition_ShouldReturnMatchingItems()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(new TestItem(1, "Sword1"));
        inventory.TryAdd(new TestItem(1, "Sword2"));
        inventory.TryAdd(new TestItem(2, "Shield"));

        var swords = inventory.GetByDefinition(new ItemDefinitionId(1));

        Assert.Equal(2, swords.Count());
    }

    [Fact]
    public void GetTotalStackCount_ShouldSumStackCounts()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(new TestItem(1, "Potion1", stackCount: 10));
        inventory.TryAdd(new TestItem(1, "Potion2", stackCount: 5));

        var total = inventory.GetTotalStackCount(new ItemDefinitionId(1));

        Assert.Equal(15, total);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(new TestItem(1, "Item1"));
        inventory.TryAdd(new TestItem(2, "Item2"));

        inventory.Clear();

        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void RemoveWhere_ShouldRemoveMatchingItems()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(new TestItem(1, "Sword"));
        inventory.TryAdd(new TestItem(2, "Shield"));
        inventory.TryAdd(new TestItem(1, "Sword2"));

        var removed = inventory.RemoveWhere(item => item.DefinitionId.Value == 1);

        Assert.Equal(2, removed);
        Assert.Equal(1, inventory.Count);
    }

    [Fact]
    public void HasSpace_WhenNotFull_ShouldReturnTrue()
    {
        var inventory = CreateInventory(capacity: 5);
        inventory.TryAdd(new TestItem(1, "Item"));

        Assert.True(inventory.HasSpace);
    }

    [Fact]
    public void HasSpace_WhenFull_ShouldReturnFalse()
    {
        var inventory = CreateInventory(capacity: 1);
        inventory.TryAdd(new TestItem(1, "Item"));

        Assert.False(inventory.HasSpace);
    }

    [Fact]
    public void OnItemAdded_ShouldBeRaised()
    {
        var inventory = CreateInventory();
        var eventRaised = false;
        TestItem? addedItem = null;

        inventory.OnItemAdded += e =>
        {
            eventRaised = true;
            addedItem = e.Item;
        };

        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        Assert.True(eventRaised);
        Assert.Equal(item, addedItem);
    }

    [Fact]
    public void OnItemRemoved_ShouldBeRaised()
    {
        var inventory = CreateInventory();
        var eventRaised = false;

        inventory.OnItemRemoved += e => eventRaised = true;

        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);
        inventory.TryRemove(item.InstanceId);

        Assert.True(eventRaised);
    }

    [Fact]
    public void OnItemStackChanged_ShouldBeRaisedOnPartialRemove()
    {
        var inventory = CreateInventory();
        var eventRaised = false;
        int previousCount = 0;
        int newCount = 0;

        inventory.OnItemStackChanged += e =>
        {
            eventRaised = true;
            previousCount = e.PreviousStackCount;
            newCount = e.NewStackCount;
        };

        var item = new TestItem(1, "Potion", stackCount: 10);
        inventory.TryAdd(item);
        inventory.TryRemove(item.InstanceId, count: 3);

        Assert.True(eventRaised);
        Assert.Equal(10, previousCount);
        Assert.Equal(7, newCount);
    }

    #region CanAdd / CanRemove Tests

    [Fact]
    public void CanAdd_WhenHasSpace_ShouldReturnValid()
    {
        var inventory = CreateInventory(capacity: 5);
        var item = new TestItem(1, "Sword");

        var result = inventory.CanAdd(item);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanAdd_WhenFull_ShouldReturnInvalid()
    {
        var inventory = CreateInventory(capacity: 1);
        inventory.TryAdd(new TestItem(1, "Existing"));

        var result = inventory.CanAdd(new TestItem(2, "New"));

        Assert.False(result.IsValid);
        Assert.Contains(result.FailureReasons, r => r.Code == ValidationFailureCode.CapacityExceeded);
    }

    [Fact]
    public void CanRemove_WhenItemExists_ShouldReturnValid()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        var result = inventory.CanRemove(item.InstanceId);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanRemove_WhenItemNotExists_ShouldReturnInvalid()
    {
        var inventory = CreateInventory();

        var result = inventory.CanRemove(new ItemInstanceId(999));

        Assert.False(result.IsValid);
        Assert.Contains(result.FailureReasons, r => r.Code == ValidationFailureCode.ItemNotFound);
    }

    [Fact]
    public void CanRemove_WhenInsufficientQuantity_ShouldReturnInvalid()
    {
        var inventory = CreateInventory();
        var item = new TestItem(1, "Potion", stackCount: 3);
        inventory.TryAdd(item);

        var result = inventory.CanRemove(item.InstanceId, count: 10);

        Assert.False(result.IsValid);
        Assert.Contains(result.FailureReasons, r => r.Code == ValidationFailureCode.InsufficientQuantity);
    }

    #endregion
}
