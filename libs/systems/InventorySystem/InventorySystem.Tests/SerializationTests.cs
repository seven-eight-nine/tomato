using System.Linq;
using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class SerializationTests
{
    private static SimpleInventory<TestItem> CreateInventory(int id, int capacity = 10)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(id),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    [Fact]
    public void Serialize_EmptyInventory_ShouldSucceed()
    {
        var inventory = CreateInventory(1);
        var serializer = new BinarySerializer();

        inventory.Serialize(serializer);

        Assert.True(serializer.ToArray().Length > 0);
    }

    [Fact]
    public void Serialize_WithItems_ShouldIncludeAllItems()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword", stackCount: 1));
        inventory.TryAdd(new TestItem(2, "Potion", stackCount: 10));

        var serializer = new BinarySerializer();
        inventory.Serialize(serializer);
        var data = serializer.ToArray();

        var newInventory = CreateInventory(1);
        var deserializer = new BinaryDeserializer(data);
        newInventory.Deserialize(ref deserializer);

        Assert.Equal(2, newInventory.Count);
    }

    [Fact]
    public void Deserialize_ShouldRestoreItems()
    {
        var original = CreateInventory(1);
        original.TryAdd(new TestItem(1, "Sword", stackCount: 1));
        original.TryAdd(new TestItem(2, "Shield", stackCount: 1));
        original.TryAdd(new TestItem(3, "Potion", stackCount: 99));

        var serializer = new BinarySerializer();
        original.Serialize(serializer);
        var data = serializer.ToArray();

        var restored = CreateInventory(1);
        var deserializer = new BinaryDeserializer(data);
        restored.Deserialize(ref deserializer);

        Assert.Equal(original.Count, restored.Count);

        var allItems = restored.GetAll().ToList();
        Assert.Equal(3, allItems.Count);
    }

    [Fact]
    public void Deserialize_ShouldPreserveStackCounts()
    {
        var original = CreateInventory(1);
        var potion = new TestItem(1, "Potion", stackCount: 50);
        original.TryAdd(potion);

        var serializer = new BinarySerializer();
        original.Serialize(serializer);
        var data = serializer.ToArray();

        var restored = CreateInventory(1);
        var deserializer = new BinaryDeserializer(data);
        restored.Deserialize(ref deserializer);

        var restoredItems = restored.GetByDefinition(new ItemDefinitionId(1)).ToList();
        Assert.Single(restoredItems);
        Assert.Equal(50, restoredItems[0].StackCount);
    }

    [Fact]
    public void Deserialize_ShouldPreserveItemNames()
    {
        var original = CreateInventory(1);
        original.TryAdd(new TestItem(1, "ExcaliburSword", stackCount: 1));

        var serializer = new BinarySerializer();
        original.Serialize(serializer);
        var data = serializer.ToArray();

        var restored = CreateInventory(1);
        var deserializer = new BinaryDeserializer(data);
        restored.Deserialize(ref deserializer);

        var items = restored.GetAll().ToList();
        Assert.Single(items);
        Assert.Equal("ExcaliburSword", items[0].Name);
    }

    [Fact]
    public void RoundTrip_MultipleSerializations_ShouldBeConsistent()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Item1", stackCount: 5));
        inventory.TryAdd(new TestItem(2, "Item2", stackCount: 10));

        var serializer1 = new BinarySerializer();
        inventory.Serialize(serializer1);
        var data1 = serializer1.ToArray();

        var temp = CreateInventory(1);
        var deserializer1 = new BinaryDeserializer(data1);
        temp.Deserialize(ref deserializer1);

        var serializer2 = new BinarySerializer();
        temp.Serialize(serializer2);
        var data2 = serializer2.ToArray();

        var final = CreateInventory(1);
        var deserializer2 = new BinaryDeserializer(data2);
        final.Deserialize(ref deserializer2);

        Assert.Equal(2, final.Count);
    }

    [Fact]
    public void TestItem_SerializeDeserialize_ShouldPreserveAllFields()
    {
        var original = new TestItem(42, "TestName", stackCount: 123);

        var serializer = new BinarySerializer();
        original.Serialize(serializer);
        var data = serializer.ToArray();

        var deserializer = new BinaryDeserializer(data);
        var restored = TestItem.Deserialize(ref deserializer, true);

        Assert.Equal(original.DefinitionId, restored.DefinitionId);
        Assert.Equal(original.InstanceId, restored.InstanceId);
        Assert.Equal(original.StackCount, restored.StackCount);
        Assert.Equal(original.Name, restored.Name);
    }

    [Fact]
    public void Deserialize_ShouldClearExistingItems()
    {
        var original = CreateInventory(1);
        original.TryAdd(new TestItem(1, "Original", stackCount: 1));

        var serializer = new BinarySerializer();
        original.Serialize(serializer);
        var data = serializer.ToArray();

        var target = CreateInventory(1);
        target.TryAdd(new TestItem(2, "Existing1", stackCount: 1));
        target.TryAdd(new TestItem(3, "Existing2", stackCount: 1));
        Assert.Equal(2, target.Count);

        var deserializer = new BinaryDeserializer(data);
        target.Deserialize(ref deserializer);

        Assert.Equal(1, target.Count);
    }
}
