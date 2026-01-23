using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Spawn;
using Tomato.CommandGenerator;
using EHSIEntityArena = Tomato.EntityHandleSystem.IEntityArena;

namespace Tomato.GameLoop.Tests.Context;

public enum TestCategory
{
    FullBody,
    Upper,
    Lower
}

/// <summary>
/// テスト用のモックArena
/// </summary>
public class MockEntityArena : EHSIEntityArena, IEntitySpawner
{
    private readonly HashSet<(int index, int generation)> _validHandles = new();
    private int _nextIndex = 0;

    public AnyHandle Spawn()
    {
        var index = _nextIndex++;
        var generation = 1;
        _validHandles.Add((index, generation));
        return new AnyHandle(this, index, generation);
    }

    public bool Despawn(AnyHandle handle)
    {
        var key = (handle.Index, handle.Generation);
        if (_validHandles.Contains(key))
        {
            _validHandles.Remove(key);
            return true;
        }
        return false;
    }

    public bool IsValid(int index, int generation)
    {
        return _validHandles.Contains((index, generation));
    }
}

public class EntityContextRegistryTests
{
    [Fact]
    public void Register_NewHandle_ReturnsContext()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();

        // Act
        var context = registry.Register(handle);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(handle, context.Handle);
        Assert.True(registry.Exists(handle));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Register_DuplicateHandle_ThrowsException()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();
        registry.Register(handle);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(handle));
    }

    [Fact]
    public void TryGetContext_ExistingHandle_ReturnsTrue()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();
        var registeredContext = registry.Register(handle);

        // Act
        var result = registry.TryGetContext(handle, out var context);

        // Assert
        Assert.True(result);
        Assert.Same(registeredContext, context);
    }

    [Fact]
    public void TryGetContext_NonExistingHandle_ReturnsFalse()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();

        // Act
        var result = registry.TryGetContext(handle, out var context);

        // Assert
        Assert.False(result);
        Assert.Null(context);
    }

    [Fact]
    public void MarkForDeletion_ExistingHandle_MarksContext()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();
        var context = registry.Register(handle);

        // Act
        registry.MarkForDeletion(handle);

        // Assert
        Assert.True(context.IsMarkedForDeletion);
        Assert.Single(registry.GetMarkedForDeletion());
    }

    [Fact]
    public void ProcessDeletions_RemovesMarkedEntities()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle = arena.Spawn();
        registry.Register(handle);
        registry.MarkForDeletion(handle);

        // Act
        registry.ProcessDeletions();

        // Assert
        Assert.False(registry.Exists(handle));
        Assert.Equal(0, registry.Count);
        Assert.Empty(registry.GetMarkedForDeletion());
    }

    [Fact]
    public void GetAllEntities_ReturnsAllActiveEntities()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);

        // Act
        var entities = registry.GetAllEntities().ToList();

        // Assert
        Assert.Equal(2, entities.Count);
        Assert.Contains(handle1, entities);
        Assert.Contains(handle2, entities);
    }

    [Fact]
    public void Clear_RemovesAllEntities()
    {
        // Arrange
        var registry = new EntityContextRegistry<TestCategory>();
        var arena = new MockEntityArena();
        var handle1 = arena.Spawn();
        var handle2 = arena.Spawn();
        registry.Register(handle1);
        registry.Register(handle2);

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.Exists(handle1));
        Assert.False(registry.Exists(handle2));
    }
}
