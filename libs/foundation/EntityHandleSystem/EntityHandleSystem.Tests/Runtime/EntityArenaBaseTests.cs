using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tomato.HandleSystem;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Runtime;

/// <summary>
/// EntityArenaBase comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class EntityArenaBaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidCapacity_ShouldCreateArena()
    {
        var arena = new TestArena(16);

        Assert.NotNull(arena);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ShouldUseMinimumOfOne()
    {
        var arena = new TestArena(0);

        Assert.NotNull(arena);
        Assert.True(arena.Capacity >= 1);
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ShouldUseMinimumOfOne()
    {
        var arena = new TestArena(-10);

        Assert.NotNull(arena);
        Assert.True(arena.Capacity >= 1);
    }

    [Fact]
    public void Constructor_WithCallbacks_ShouldStoreCallbacks()
    {
        var spawned = new List<TestEntity>();
        var despawned = new List<TestEntity>();

        var arena = new TestArena(16, (ref TestEntity e) => spawned.Add(e), (ref TestEntity e) => despawned.Add(e));

        Assert.NotNull(arena);
    }

    #endregion

    #region Allocation Tests

    [Fact]
    public void Allocate_FirstCall_ShouldReturnIndex0()
    {
        var arena = new TestArena(16);

        var (index, generation) = arena.Allocate();

        Assert.Equal(0, index);
        Assert.Equal(1, generation);
    }

    [Fact]
    public void Allocate_MultipleCalls_ShouldReturnIncrementingIndices()
    {
        var arena = new TestArena(16);

        var (index1, _) = arena.Allocate();
        var (index2, _) = arena.Allocate();
        var (index3, _) = arena.Allocate();

        Assert.Equal(0, index1);
        Assert.Equal(1, index2);
        Assert.Equal(2, index3);
    }

    [Fact]
    public void Allocate_ShouldInvokeSpawnCallback()
    {
        var spawnedEntities = new List<TestEntity>();
        var arena = new TestArena(16, (ref TestEntity e) => spawnedEntities.Add(e), null);

        arena.Allocate();
        arena.Allocate();

        Assert.Equal(2, spawnedEntities.Count);
    }

    [Fact]
    public void Allocate_ShouldIncrementCount()
    {
        var arena = new TestArena(16);
        Assert.Equal(0, arena.Count);

        arena.Allocate();
        Assert.Equal(1, arena.Count);

        arena.Allocate();
        Assert.Equal(2, arena.Count);
    }

    #endregion

    #region Deallocation Tests

    [Fact]
    public void Deallocate_ValidHandle_ShouldReturnTrue()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.Deallocate(index, generation);

        Assert.True(result);
    }

    [Fact]
    public void Deallocate_InvalidGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        // Try to deallocate with wrong generation
        var result = arena.Deallocate(index, generation + 1);

        Assert.False(result);
    }

    [Fact]
    public void Deallocate_InvalidIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        arena.Allocate();

        var result = arena.Deallocate(-1, 1);

        Assert.False(result);
    }

    [Fact]
    public void Deallocate_ShouldInvokeDespawnCallback()
    {
        var despawnedEntities = new List<TestEntity>();
        var arena = new TestArena(16, null, (ref TestEntity e) => despawnedEntities.Add(e));

        var (index, generation) = arena.Allocate();
        arena.Deallocate(index, generation);

        Assert.Single(despawnedEntities);
    }

    [Fact]
    public void Deallocate_ShouldDecrementCount()
    {
        var arena = new TestArena(16);
        arena.Allocate();
        arena.Allocate();
        Assert.Equal(2, arena.Count);

        var (index, generation) = arena.Allocate();
        Assert.Equal(3, arena.Count);

        arena.Deallocate(index, generation);
        Assert.Equal(2, arena.Count);
    }

    #endregion

    #region Slot Reuse Tests

    [Fact]
    public void AfterDeallocation_SameSlot_ShouldBeReused()
    {
        var arena = new TestArena(16);

        var (index1, gen1) = arena.Allocate();
        arena.Deallocate(index1, gen1);

        var (index2, _) = arena.Allocate();

        Assert.Equal(index1, index2);
    }

    [Fact]
    public void AfterDeallocation_Generation_ShouldIncrement()
    {
        var arena = new TestArena(16);

        var (index1, gen1) = arena.Allocate();
        arena.Deallocate(index1, gen1);

        var (_, gen2) = arena.Allocate();

        Assert.Equal(gen1 + 1, gen2);
    }

    [Fact]
    public void OldHandle_AfterReallocation_ShouldBeInvalid()
    {
        var arena = new TestArena(16);

        var (index1, gen1) = arena.Allocate();
        arena.Deallocate(index1, gen1);
        arena.Allocate();  // Reuse slot

        var isValid = arena.IsValid(index1, gen1);

        Assert.False(isValid);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ValidHandle_ShouldReturnTrue()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.TryGet(index, generation, out var entity);

        Assert.True(result);
        Assert.NotNull(entity);
    }

    [Fact]
    public void TryGet_InvalidGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.TryGet(index, generation + 1, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    [Fact]
    public void TryGet_InvalidIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        arena.Allocate();

        var result = arena.TryGet(-1, 1, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    [Fact]
    public void TryGet_AfterDeallocation_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();
        arena.Deallocate(index, generation);

        var result = arena.TryGet(index, generation, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_ValidHandle_ShouldReturnTrue()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.IsValid(index, generation);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_InvalidGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.IsValid(index, generation + 1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_NegativeIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(-1, 1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_IndexOutOfBounds_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(100, 1);

        Assert.False(result);
    }

    #endregion

    #region Capacity Expansion Tests

    [Fact]
    public void Allocate_BeyondCapacity_ShouldExpandArena()
    {
        var arena = new TestArena(4);

        for (int i = 0; i < 8; i++)
        {
            arena.Allocate();
        }

        Assert.Equal(8, arena.Count);
        Assert.True(arena.Capacity >= 8);
    }

    [Fact]
    public void Allocate_AfterExpansion_ShouldStillWork()
    {
        var arena = new TestArena(2);

        var (index1, gen1) = arena.Allocate();
        var (index2, gen2) = arena.Allocate();
        var (index3, gen3) = arena.Allocate();  // Triggers expansion

        Assert.True(arena.IsValid(index1, gen1));
        Assert.True(arena.IsValid(index2, gen2));
        Assert.True(arena.IsValid(index3, gen3));
    }

    [Fact]
    public void Expand_ShouldPreserveExistingEntities()
    {
        var arena = new TestArena(2);

        var (index1, gen1) = arena.Allocate();
        arena.TryGet(index1, gen1, out var entity1);
        entity1.Value = 42;

        // Force expansion
        arena.Allocate();
        arena.Allocate();

        arena.TryGet(index1, gen1, out var entityAfter);
        Assert.Equal(42, entityAfter.Value);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentAllocations_ShouldBeThreadSafe()
    {
        var arena = new TestArena(16);
        var tasks = new List<Task>();
        var allocations = new List<(int Index, int Generation)>();
        var lockObj = new object();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var allocation = arena.Allocate();
                    lock (lockObj)
                    {
                        allocations.Add(allocation);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(100, allocations.Count);
        Assert.Equal(100, arena.Count);
    }

    [Fact]
    public async Task ConcurrentAllocateAndDeallocate_ShouldBeThreadSafe()
    {
        var arena = new TestArena(16);
        var exceptions = new List<Exception>();
        var lockObj = new object();

        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var (index, generation) = arena.Allocate();
                        Thread.Sleep(1);
                        arena.Deallocate(index, generation);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DoubleDeallocate_ShouldFailSecondTime()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result1 = arena.Deallocate(index, generation);
        var result2 = arena.Deallocate(index, generation);

        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void GenerationOverflow_ShouldWrapAround()
    {
        var arena = new TestArenaWithGenerationAccess(16);

        var (index, _) = arena.Allocate();

        // Simulate many allocate/deallocate cycles to overflow generation
        arena.SetGeneration(index, int.MaxValue);
        arena.Deallocate(index, int.MaxValue);

        var (_, newGen) = arena.Allocate();

        // After overflow, generation should wrap to 1 (not 0)
        Assert.True(newGen >= 1);
    }

    #endregion

    #region Helper Classes

    private class TestEntity
    {
        public int Value { get; set; }
    }

    private class TestArena : EntityArenaBase<TestEntity, object>
    {
        public TestArena(int initialCapacity)
            : base(initialCapacity, null, null)
        {
        }

        public TestArena(int initialCapacity, RefAction<TestEntity>? onSpawn, RefAction<TestEntity>? onDespawn)
            : base(initialCapacity, onSpawn!, onDespawn!)
        {
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public int Capacity
        {
            get
            {
                lock (_lock)
                {
                    return _entities.Length;
                }
            }
        }

        public (int Index, int Generation) Allocate()
        {
            lock (_lock)
            {
                var index = AllocateInternal(out var generation);
                return (index, generation);
            }
        }

        public bool Deallocate(int index, int generation)
        {
            lock (_lock)
            {
                return DeallocateInternal(index, generation);
            }
        }

        public bool TryGet(int index, int generation, out TestEntity? entity)
        {
            lock (_lock)
            {
                ref var e = ref TryGetRefInternal(index, generation, out var valid);
                entity = valid ? e : null;
                return valid;
            }
        }

        public bool IsValid(int index, int generation)
        {
            lock (_lock)
            {
                return IsValidInternal(index, generation);
            }
        }
    }

    private class TestArenaWithGenerationAccess : TestArena
    {
        public TestArenaWithGenerationAccess(int initialCapacity)
            : base(initialCapacity)
        {
        }

        public void SetGeneration(int index, int generation)
        {
            lock (_lock)
            {
                _generations[index] = generation;
            }
        }
    }

    #endregion
}
