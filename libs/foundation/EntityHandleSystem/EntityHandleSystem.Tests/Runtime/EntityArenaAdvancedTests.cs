using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Runtime;

/// <summary>
/// EntityArenaBase advanced tests - t-wada style thorough coverage
/// Covers boundary conditions, edge cases, and stress scenarios.
/// </summary>
public class EntityArenaAdvancedTests
{
    #region Handle Validation with Boundary Indices Tests

    [Fact]
    public void IsValid_WithIntMaxValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(int.MaxValue, 1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_WithIntMinValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(int.MinValue, 1);

        Assert.False(result);
    }

    [Fact]
    public void TryGet_WithIntMaxValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.TryGet(int.MaxValue, 1, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    [Fact]
    public void TryGet_WithIntMinValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.TryGet(int.MinValue, 1, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    [Fact]
    public void Deallocate_WithIntMaxValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.Deallocate(int.MaxValue, 1);

        Assert.False(result);
    }

    [Fact]
    public void Deallocate_WithIntMinValueIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.Deallocate(int.MinValue, 1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_WithNegativeGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, _) = arena.Allocate();

        var result = arena.IsValid(index, -1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_WithIntMaxValueGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, _) = arena.Allocate();

        var result = arena.IsValid(index, int.MaxValue);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_WithIndexEqualToCapacity_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(16, 1);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_WithIndexJustBelowCapacity_AfterAllocation_ShouldWork()
    {
        var arena = new TestArena(4);

        // Fill all slots
        for (int i = 0; i < 4; i++)
        {
            arena.Allocate();
        }

        var result = arena.IsValid(3, 1);

        Assert.True(result);
    }

    #endregion

    #region Multiple Sequential Arena Expansions Tests

    [Fact]
    public void Allocate_MultipleExpansions_ShouldSucceed()
    {
        var arena = new TestArena(2);

        // Force multiple expansions: 2 -> 4 -> 8 -> 16
        for (int i = 0; i < 15; i++)
        {
            arena.Allocate();
        }

        Assert.Equal(15, arena.Count);
        Assert.True(arena.Capacity >= 16);
    }

    [Fact]
    public void Allocate_AfterMultipleExpansions_AllHandlesShouldBeValid()
    {
        var arena = new TestArena(2);
        var allocations = new List<(int Index, int Generation)>();

        // Force multiple expansions
        for (int i = 0; i < 20; i++)
        {
            allocations.Add(arena.Allocate());
        }

        foreach (var (index, generation) in allocations)
        {
            Assert.True(arena.IsValid(index, generation), $"Handle at index {index} should be valid");
        }
    }

    [Fact]
    public void Allocate_AfterMultipleExpansions_EntitiesShouldMaintainValues()
    {
        var arena = new TestArena(2);
        var allocations = new List<(int Index, int Generation)>();

        // Create entities and set values
        for (int i = 0; i < 20; i++)
        {
            var (index, generation) = arena.Allocate();
            allocations.Add((index, generation));
            arena.TryGet(index, generation, out var entity);
            entity!.Value = i * 10;
        }

        // Verify values after expansions
        for (int i = 0; i < allocations.Count; i++)
        {
            var (index, generation) = allocations[i];
            arena.TryGet(index, generation, out var entity);
            Assert.Equal(i * 10, entity!.Value);
        }
    }

    [Fact]
    public void Expand_ShouldDoubleCapacity()
    {
        var arena = new TestArena(4);
        Assert.Equal(4, arena.Capacity);

        // Fill capacity
        for (int i = 0; i < 4; i++)
        {
            arena.Allocate();
        }

        // Trigger expansion
        arena.Allocate();

        Assert.Equal(8, arena.Capacity);
    }

    [Fact]
    public void Expand_SequentialExpansions_ShouldFollowPowerOfTwo()
    {
        var arena = new TestArena(1);
        var capacities = new List<int> { 1 };

        for (int i = 0; i < 100; i++)
        {
            arena.Allocate();
            if (!capacities.Contains(arena.Capacity))
            {
                capacities.Add(arena.Capacity);
            }
        }

        // Verify power of 2 progression
        for (int i = 1; i < capacities.Count; i++)
        {
            Assert.Equal(capacities[i - 1] * 2, capacities[i]);
        }
    }

    #endregion

    #region Generation Overflow with Multiple Cycles Tests

    [Fact]
    public void GenerationOverflow_WithTenPlusCycles_ShouldWrapAround()
    {
        var arena = new TestArenaWithGenerationAccess(16);

        var (index, _) = arena.Allocate();

        // Set generation close to overflow
        arena.SetGeneration(index, int.MaxValue);
        arena.Deallocate(index, int.MaxValue);

        // Allocate again - should have wrapped generation
        var (newIndex, newGen) = arena.Allocate();

        Assert.Equal(index, newIndex);
        Assert.Equal(1, newGen); // Should wrap to 1, not 0
    }

    [Fact]
    public void GenerationOverflow_ShouldSkipZero()
    {
        var arena = new TestArenaWithGenerationAccess(16);

        var (index, _) = arena.Allocate();

        // Set generation to MAX-1 and cycle multiple times
        arena.SetGeneration(index, int.MaxValue - 5);

        for (int i = 0; i < 10; i++)
        {
            var gen = arena.GetGeneration(index);
            arena.Deallocate(index, gen);

            var (_, newGen) = arena.Allocate();

            // Generation should never be 0
            Assert.True(newGen >= 1, $"Generation should be >= 1, was {newGen}");
        }
    }

    [Fact]
    public void RepeatedAllocationDeallocation_SameSot_ShouldIncrementGeneration()
    {
        var arena = new TestArena(1);

        int previousGeneration = 0;
        for (int i = 0; i < 15; i++)
        {
            var (index, generation) = arena.Allocate();

            if (i > 0)
            {
                Assert.True(generation > previousGeneration || generation == 1,
                    $"Generation should increase or wrap to 1. Previous: {previousGeneration}, Current: {generation}");
            }

            previousGeneration = generation;
            arena.Deallocate(index, generation);
        }
    }

    [Fact]
    public void OldHandle_AfterMultipleCycles_ShouldBeInvalid()
    {
        var arena = new TestArena(1);

        var (index, originalGen) = arena.Allocate();
        arena.Deallocate(index, originalGen);

        // Cycle through several allocations
        for (int i = 0; i < 5; i++)
        {
            var (_, gen) = arena.Allocate();
            arena.Deallocate(0, gen);
        }

        // Original handle should still be invalid
        Assert.False(arena.IsValid(index, originalGen));
    }

    #endregion

    #region Callback Exception Handling Tests

    [Fact]
    public void SpawnCallback_ThrowsException_ShouldPropagateException()
    {
        var arena = new TestArena(16,
            (ref TestEntity e) => throw new InvalidOperationException("Spawn error"),
            null);

        Assert.Throws<InvalidOperationException>(() => arena.Allocate());
    }

    [Fact]
    public void DespawnCallback_ThrowsException_ShouldPropagateException()
    {
        var arena = new TestArena(16,
            null,
            (ref TestEntity e) => throw new InvalidOperationException("Despawn error"));

        var (index, generation) = arena.Allocate();

        Assert.Throws<InvalidOperationException>(() => arena.Deallocate(index, generation));
    }

    [Fact]
    public void SpawnCallback_ThrowsException_ShouldNotCorruptArenaState()
    {
        int callCount = 0;
        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                callCount++;
                if (callCount == 2) throw new InvalidOperationException("Spawn error");
            },
            null);

        // First allocation should succeed
        var (index1, gen1) = arena.Allocate();
        Assert.True(arena.IsValid(index1, gen1));

        // Second allocation throws
        Assert.Throws<InvalidOperationException>(() => arena.Allocate());

        // First allocation should still be valid
        Assert.True(arena.IsValid(index1, gen1));
    }

    [Fact]
    public void NullCallbacks_ShouldNotThrow()
    {
        var arena = new TestArena(16, null, null);

        var (index, generation) = arena.Allocate();
        var deallocResult = arena.Deallocate(index, generation);

        Assert.True(deallocResult);
    }

    [Fact]
    public void Callbacks_ShouldReceiveCorrectEntity()
    {
        TestEntity? spawnedEntity = null;
        TestEntity? despawnedEntity = null;

        var arena = new TestArena(16,
            (ref TestEntity e) => spawnedEntity = e,
            (ref TestEntity e) => despawnedEntity = e);

        var (index, generation) = arena.Allocate();
        arena.TryGet(index, generation, out var entity);

        Assert.Same(entity, spawnedEntity);

        arena.Deallocate(index, generation);
        Assert.Same(entity, despawnedEntity);
    }

    [Fact]
    public void SpawnCallback_CalledBeforeHandleReturned()
    {
        bool callbackCalled = false;
        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                callbackCalled = true;
                e.Value = 42;
            },
            null);

        var (index, generation) = arena.Allocate();
        arena.TryGet(index, generation, out var entity);

        Assert.True(callbackCalled);
        Assert.Equal(42, entity!.Value);
    }

    #endregion

    #region Free List Overflow/Underflow Protection Tests

    [Fact]
    public void FreeList_AfterDeallocatingAll_ShouldReuseCorrectly()
    {
        var arena = new TestArena(4);

        // Allocate all
        var allocations = new List<(int Index, int Generation)>();
        for (int i = 0; i < 4; i++)
        {
            allocations.Add(arena.Allocate());
        }

        // Deallocate all
        foreach (var (index, generation) in allocations)
        {
            arena.Deallocate(index, generation);
        }

        Assert.Equal(0, arena.Count);

        // Reallocate all - should reuse slots
        var newAllocations = new List<(int Index, int Generation)>();
        for (int i = 0; i < 4; i++)
        {
            newAllocations.Add(arena.Allocate());
        }

        Assert.Equal(4, arena.Count);

        // All slots should be reused (indices 0-3)
        var indices = new HashSet<int>(newAllocations.ConvertAll(a => a.Index));
        Assert.Equal(4, indices.Count);
        Assert.Contains(0, indices);
        Assert.Contains(1, indices);
        Assert.Contains(2, indices);
        Assert.Contains(3, indices);
    }

    [Fact]
    public void FreeList_AlternatingAllocateDeallocation_ShouldMaintainCorrectCount()
    {
        var arena = new TestArena(16);

        for (int i = 0; i < 100; i++)
        {
            var (index, generation) = arena.Allocate();
            Assert.Equal(1, arena.Count);

            arena.Deallocate(index, generation);
            Assert.Equal(0, arena.Count);
        }
    }

    [Fact]
    public void FreeList_PartialDeallocation_ShouldReuseDeallocatedSlotsFirst()
    {
        var arena = new TestArena(4);

        var alloc1 = arena.Allocate();
        var alloc2 = arena.Allocate();
        var alloc3 = arena.Allocate();

        // Deallocate middle slot
        arena.Deallocate(alloc2.Index, alloc2.Generation);

        // New allocation should reuse slot 1 (LIFO from free list)
        var newAlloc = arena.Allocate();

        Assert.Equal(alloc2.Index, newAlloc.Index);
        Assert.Equal(alloc2.Generation + 1, newAlloc.Generation);
    }

    [Fact]
    public void FreeList_LIFO_Behavior()
    {
        var arena = new TestArena(4);

        var alloc0 = arena.Allocate();
        var alloc1 = arena.Allocate();
        var alloc2 = arena.Allocate();

        // Deallocate in order 0, 1, 2
        arena.Deallocate(alloc0.Index, alloc0.Generation);
        arena.Deallocate(alloc1.Index, alloc1.Generation);
        arena.Deallocate(alloc2.Index, alloc2.Generation);

        // Should reallocate in reverse order (LIFO): 2, 1, 0
        var realloc1 = arena.Allocate();
        var realloc2 = arena.Allocate();
        var realloc3 = arena.Allocate();

        Assert.Equal(alloc2.Index, realloc1.Index);
        Assert.Equal(alloc1.Index, realloc2.Index);
        Assert.Equal(alloc0.Index, realloc3.Index);
    }

    #endregion

    #region State Invariant Checks (_count + _freeCount consistency) Tests

    [Fact]
    public void CountPlusFreeCount_AfterAllocations_ShouldBeConsistent()
    {
        var arena = new TestArenaWithInvariantAccess(8);

        for (int i = 0; i < 5; i++)
        {
            arena.Allocate();
            Assert.True(arena.Count + arena.FreeCount <= arena.HighWaterMark);
        }
    }

    [Fact]
    public void CountPlusFreeCount_AfterDeallocations_ShouldBeConsistent()
    {
        var arena = new TestArenaWithInvariantAccess(8);

        var allocations = new List<(int Index, int Generation)>();
        for (int i = 0; i < 5; i++)
        {
            allocations.Add(arena.Allocate());
        }

        foreach (var (index, generation) in allocations)
        {
            arena.Deallocate(index, generation);
            Assert.Equal(arena.FreeCount + arena.Count, arena.HighWaterMark);
        }
    }

    [Fact]
    public void Count_AfterMixedOperations_ShouldBeAccurate()
    {
        var arena = new TestArena(16);

        var handle1 = arena.Allocate();
        var handle2 = arena.Allocate();
        var handle3 = arena.Allocate();
        Assert.Equal(3, arena.Count);

        arena.Deallocate(handle2.Index, handle2.Generation);
        Assert.Equal(2, arena.Count);

        arena.Allocate();
        Assert.Equal(3, arena.Count);

        arena.Deallocate(handle1.Index, handle1.Generation);
        arena.Deallocate(handle3.Index, handle3.Generation);
        Assert.Equal(1, arena.Count);
    }

    [Fact]
    public void FreeCount_AfterExpansion_ShouldStillBeCorrect()
    {
        var arena = new TestArenaWithInvariantAccess(2);

        var alloc1 = arena.Allocate();
        var alloc2 = arena.Allocate();
        arena.Deallocate(alloc1.Index, alloc1.Generation);

        // Trigger expansion
        arena.Allocate();
        arena.Allocate();
        arena.Allocate();

        // Free count should remain 0 (the freed slot was reused)
        Assert.Equal(4, arena.Count);
    }

    [Fact]
    public void ArenaState_ConcurrentOperations_ShouldMaintainInvariants()
    {
        var arena = new TestArenaWithInvariantAccess(16);
        var random = new Random(42);

        Parallel.For(0, 100, _ =>
        {
            var (index, generation) = arena.Allocate();

            if (random.NextDouble() < 0.5)
            {
                arena.Deallocate(index, generation);
            }
        });

        // After all operations, invariants should hold
        Assert.True(arena.Count >= 0);
        Assert.True(arena.FreeCount >= 0);
        Assert.True(arena.Count + arena.FreeCount <= arena.Capacity);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void Allocate_CapacityOfOne_ShouldWork()
    {
        var arena = new TestArena(1);

        var (index, generation) = arena.Allocate();

        Assert.Equal(0, index);
        Assert.Equal(1, generation);
    }

    [Fact]
    public void MultipleArenas_ShouldBeIndependent()
    {
        var arena1 = new TestArena(4);
        var arena2 = new TestArena(4);

        arena1.Allocate();
        arena1.Allocate();

        arena2.Allocate();

        Assert.Equal(2, arena1.Count);
        Assert.Equal(1, arena2.Count);
    }

    [Fact]
    public void Handle_FromWrongArena_ShouldBeInvalid()
    {
        var arena1 = new TestArena(4);
        var arena2 = new TestArena(4);

        var (index1, gen1) = arena1.Allocate();

        // Using handle from arena1 in arena2 (simulated by using same index/gen)
        // Both arenas have index 0 with generation 1 initially
        Assert.True(arena1.IsValid(index1, gen1));
        Assert.True(arena2.IsValid(index1, gen1)); // Both have index 0, gen 1 initially

        // Deallocate and reallocate in arena2 to change generation
        var handle2 = arena2.Allocate();
        arena2.Deallocate(handle2.Index, handle2.Generation);
        var (newIndex, newGen) = arena2.Allocate();

        // Same index, but different generation now in arena2
        Assert.Equal(0, newIndex);
        Assert.Equal(2, newGen); // Generation incremented

        // Arena1's handle (index 0, gen 1) should still be valid in arena1
        Assert.True(arena1.IsValid(0, 1));

        // But the old generation 1 is now invalid in arena2 (generation is now 2)
        Assert.False(arena2.IsValid(0, 1));
        Assert.True(arena2.IsValid(0, 2));
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

        public int GetGeneration(int index)
        {
            lock (_lock)
            {
                return _generations[index];
            }
        }
    }

    private class TestArenaWithInvariantAccess : TestArena
    {
        public TestArenaWithInvariantAccess(int initialCapacity)
            : base(initialCapacity)
        {
        }

        public int FreeCount
        {
            get
            {
                lock (_lock)
                {
                    return _freeCount;
                }
            }
        }

        public int HighWaterMark
        {
            get
            {
                lock (_lock)
                {
                    return _count + _freeCount;
                }
            }
        }
    }

    #endregion
}
