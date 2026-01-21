using Xunit;

namespace Tomato.HandleSystem.Tests;

[Handleable]
public partial struct BasicItem
{
    public int Value;
}

[Handleable]
public partial struct ItemWithMethods
{
    private int _counter;

    [HandleableMethod]
    public void Increment()
    {
        _counter++;
    }

    [HandleableMethod]
    public int GetCounter()
    {
        return _counter;
    }

    [HandleableMethod(Unsafe = true)]
    public void IncrementFast()
    {
        _counter++;
    }
}

[Handleable]
public partial class ClassItem
{
    public string Name = "";

    [HandleableMethod]
    public void SetName(string name)
    {
        Name = name;
    }
}

[Handleable(ArenaName = "CustomPool")]
public partial struct CustomNamedItem
{
    public int Id;
}

[Handleable(InitialCapacity = 64)]
public partial struct SmallCapacityItem
{
    public int Data;
}

public class HandleGeneratorTests
{
    [Fact]
    public void Handle_IsGenerated()
    {
        var arena = new BasicItemArena();
        var handle = arena.Create();
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void Handle_ImplementsIHandle()
    {
        var arena = new BasicItemArena();
        var handle = arena.Create();
        Assert.IsAssignableFrom<IHandle>(handle);
    }

    [Fact]
    public void Arena_ImplementsIArena()
    {
        var arena = new BasicItemArena();
        Assert.IsAssignableFrom<IArena>(arena);
    }

    [Fact]
    public void Arena_Create_ReturnsValidHandle()
    {
        var arena = new BasicItemArena();
        var handle = arena.Create();
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void Handle_Dispose_InvalidatesHandle()
    {
        var arena = new BasicItemArena();
        var handle = arena.Create();
        handle.Dispose();
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void Arena_Count_TracksActiveHandles()
    {
        var arena = new BasicItemArena();
        Assert.Equal(0, arena.Count);

        var h1 = arena.Create();
        Assert.Equal(1, arena.Count);

        var h2 = arena.Create();
        Assert.Equal(2, arena.Count);

        h1.Dispose();
        Assert.Equal(1, arena.Count);
    }

    [Fact]
    public void TryMethod_IsGenerated()
    {
        var arena = new ItemWithMethodsArena();
        var handle = arena.Create();

        var result = handle.TryIncrement();

        Assert.True(result);
    }

    [Fact]
    public void TryMethod_WithReturnValue()
    {
        var arena = new ItemWithMethodsArena();
        var handle = arena.Create();

        handle.TryIncrement();
        handle.TryIncrement();
        var success = handle.TryGetCounter(out int counter);

        Assert.True(success);
        Assert.Equal(2, counter);
    }

    [Fact]
    public void TryMethod_ReturnsFalse_WhenHandleInvalid()
    {
        var arena = new ItemWithMethodsArena();
        var handle = arena.Create();
        handle.Dispose();

        var result = handle.TryIncrement();

        Assert.False(result);
    }

    [Fact]
    public void UnsafeMethod_IsGenerated()
    {
        var arena = new ItemWithMethodsArena();
        var handle = arena.Create();

        handle.IncrementFast_Unsafe();
        handle.TryGetCounter(out int counter);

        Assert.Equal(1, counter);
    }

    [Fact]
    public void ClassItem_Works()
    {
        var arena = new ClassItemArena();
        var handle = arena.Create();

        handle.TrySetName("Test");

        Assert.True(handle.IsValid);
    }

    [Fact]
    public void CustomArenaName_Works()
    {
        var pool = new CustomPool();
        var handle = pool.Create();
        Assert.True(handle.IsValid);
    }

    [Fact]
    public void CustomInitialCapacity_Works()
    {
        var arena = new SmallCapacityItemArena();
        Assert.True(arena.Capacity >= 64);
    }

    [Fact]
    public void Handle_Equality()
    {
        var arena = new BasicItemArena();
        var h1 = arena.Create();
        var h2 = h1;
        var h3 = arena.Create();

        Assert.Equal(h1, h2);
        Assert.True(h1 == h2);
        Assert.NotEqual(h1, h3);
        Assert.True(h1 != h3);
    }

    [Fact]
    public void Handle_Invalid_IsDefault()
    {
        var invalid = BasicItemHandle.Invalid;
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public void Arena_ReusesSlots()
    {
        var arena = new BasicItemArena();

        for (int i = 0; i < 100; i++)
        {
            var handle = arena.Create();
            handle.Dispose();
        }

        Assert.Equal(0, arena.Count);
    }

    [Fact]
    public void OldHandle_InvalidAfterReuse()
    {
        var arena = new BasicItemArena();

        var h1 = arena.Create();
        h1.Dispose();
        var h2 = arena.Create();

        Assert.False(h1.IsValid);
        Assert.True(h2.IsValid);
    }
}
