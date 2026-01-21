using System.Collections.Generic;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Runtime;

/// <summary>
/// AnyHandle tests - TDD t-wada style
/// </summary>
public class AnyHandleTests
{
    [Fact]
    public void AnyHandle_Default_ShouldBeInvalid()
    {
        var handle = default(AnyHandle);

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void AnyHandle_Invalid_ShouldBeInvalid()
    {
        var handle = AnyHandle.Invalid;

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void AnyHandle_WithValidArena_ShouldBeValid()
    {
        var arena = new MockArena();
        arena.SetValid(0, 1, true);
        var handle = new AnyHandle(arena, 0, 1);

        Assert.True(handle.IsValid);
    }

    [Fact]
    public void AnyHandle_WithInvalidGeneration_ShouldBeInvalid()
    {
        var arena = new MockArena();
        arena.SetValid(0, 1, true);
        var handle = new AnyHandle(arena, 0, 2); // Wrong generation

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void AnyHandle_Equality_SameValues_ShouldBeEqual()
    {
        var arena = new MockArena();
        var handle1 = new AnyHandle(arena, 5, 10);
        var handle2 = new AnyHandle(arena, 5, 10);

        Assert.Equal(handle1, handle2);
        Assert.True(handle1 == handle2);
    }

    [Fact]
    public void AnyHandle_Equality_DifferentIndex_ShouldNotBeEqual()
    {
        var arena = new MockArena();
        var handle1 = new AnyHandle(arena, 5, 10);
        var handle2 = new AnyHandle(arena, 6, 10);

        Assert.NotEqual(handle1, handle2);
        Assert.True(handle1 != handle2);
    }

    [Fact]
    public void EntityContainer_WithAnyHandle_ShouldWork()
    {
        var arena1 = new MockArena();
        var arena2 = new MockArena();
        arena1.SetValid(0, 1, true);
        arena2.SetValid(0, 1, true);

        var container = new EntityContainer<AnyHandle>();
        container.Add(new AnyHandle(arena1, 0, 1));
        container.Add(new AnyHandle(arena2, 0, 1));

        var count = 0;
        var iterator = container.GetIterator();
        while (iterator.MoveNext())
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void EntityContainer_WithAnyHandle_ShouldSkipInvalid()
    {
        var arena1 = new MockArena();
        var arena2 = new MockArena();
        arena1.SetValid(0, 1, true);
        arena2.SetValid(0, 1, false); // Invalid

        var container = new EntityContainer<AnyHandle>();
        container.Add(new AnyHandle(arena1, 0, 1));
        container.Add(new AnyHandle(arena2, 0, 1));

        var count = 0;
        var iterator = container.GetIterator();
        while (iterator.MoveNext())
        {
            count++;
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void AnyHandle_TryAs_WithCorrectType_ShouldSucceed()
    {
        var arena = new MockArena();
        arena.SetValid(5, 3, true);
        var voidHandle = new AnyHandle(arena, 5, 3);

        var result = voidHandle.TryAs<MockArena>(out var typedArena);

        Assert.True(result);
        Assert.Same(arena, typedArena);
    }

    [Fact]
    public void AnyHandle_TryAs_WithWrongType_ShouldFail()
    {
        var arena = new MockArena();
        var voidHandle = new AnyHandle(arena, 5, 3);

        var result = voidHandle.TryAs<OtherMockArena>(out var typedArena);

        Assert.False(result);
        Assert.Null(typedArena);
    }

    [Fact]
    public void AnyHandle_TryAs_WithNull_ShouldFail()
    {
        var voidHandle = AnyHandle.Invalid;

        var result = voidHandle.TryAs<MockArena>(out var typedArena);

        Assert.False(result);
        Assert.Null(typedArena);
    }

    #region Mock Implementation

    private class OtherMockArena : IEntityArena
    {
        public bool IsValid(int index, int generation) => false;
    }

    private class MockArena : IEntityArena
    {
        private readonly Dictionary<(int, int), bool> _validHandles = new();

        public void SetValid(int index, int generation, bool isValid)
        {
            _validHandles[(index, generation)] = isValid;
        }

        public bool IsValid(int index, int generation)
        {
            return _validHandles.TryGetValue((index, generation), out var valid) && valid;
        }
    }

    #endregion
}
