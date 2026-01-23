using Tomato.SerializationSystem;

namespace Tomato.InventorySystem.Tests;

public class TestItem : IInventoryItem
{
    private static long _nextInstanceId;

    public ItemDefinitionId DefinitionId { get; }
    public ItemInstanceId InstanceId { get; }
    public int StackCount { get; set; }
    public string Name { get; }

    public TestItem(int definitionId, string name, int stackCount = 1)
    {
        DefinitionId = new ItemDefinitionId(definitionId);
        InstanceId = new ItemInstanceId(System.Threading.Interlocked.Increment(ref _nextInstanceId));
        Name = name;
        StackCount = stackCount;
    }

    public TestItem(ItemDefinitionId definitionId, ItemInstanceId instanceId, int stackCount)
        : this(definitionId, instanceId, $"Item_{definitionId.Value}", stackCount)
    {
    }

    private TestItem(ItemDefinitionId definitionId, ItemInstanceId instanceId, string name, int stackCount)
    {
        DefinitionId = definitionId;
        InstanceId = instanceId;
        Name = name;
        StackCount = stackCount;
    }

    public IInventoryItem Clone()
    {
        return new TestItem(DefinitionId, new ItemInstanceId(System.Threading.Interlocked.Increment(ref _nextInstanceId)), Name, StackCount);
    }

    public void Serialize(BinarySerializer serializer)
    {
        serializer.Write(DefinitionId.Value);
        serializer.Write(InstanceId.Value);
        serializer.Write(StackCount);
        serializer.Write(Name);
    }

    public void Deserialize(ref BinaryDeserializer deserializer)
    {
        // Note: This is for deserialization into an existing object
        // For creating new objects, use the factory method
    }

    public static TestItem Deserialize(ref BinaryDeserializer deserializer, bool _)
    {
        var definitionId = new ItemDefinitionId(deserializer.ReadInt32());
        var instanceId = new ItemInstanceId(deserializer.ReadInt64());
        var stackCount = deserializer.ReadInt32();
        var name = deserializer.ReadString() ?? string.Empty;
        return new TestItem(definitionId, instanceId, name, stackCount);
    }

    public override string ToString() => $"TestItem({Name}, Def={DefinitionId.Value}, Inst={InstanceId.Value}, Stack={StackCount})";
}
