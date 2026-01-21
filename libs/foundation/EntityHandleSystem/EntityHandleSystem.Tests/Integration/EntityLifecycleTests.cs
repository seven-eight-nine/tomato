using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.HandleSystem;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Integration;

/// <summary>
/// Entity lifecycle integration tests - t-wada style thorough coverage
/// Tests complete entity lifecycle, array of handles, collection equality, and callback integration.
/// </summary>
public class EntityLifecycleTests
{
    #region Create -> Get -> Modify -> Dispose -> Access (invalid) Tests

    [Fact]
    public void FullLifecycle_Create_ShouldReturnValidHandle()
    {
        var arena = new TestArena(16);

        var (index, generation) = arena.Allocate();

        Assert.True(arena.IsValid(index, generation));
    }

    [Fact]
    public void FullLifecycle_Get_ShouldReturnSameEntity()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        arena.TryGet(index, generation, out var entity1);
        arena.TryGet(index, generation, out var entity2);

        Assert.Same(entity1, entity2);
    }

    [Fact]
    public void FullLifecycle_Modify_ShouldPersist()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        arena.TryGet(index, generation, out var entity);
        entity!.Value = 42;
        entity.Name = "TestEntity";

        arena.TryGet(index, generation, out var entityAgain);

        Assert.Equal(42, entityAgain!.Value);
        Assert.Equal("TestEntity", entityAgain.Name);
    }

    [Fact]
    public void FullLifecycle_Dispose_ShouldInvalidateHandle()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        arena.Deallocate(index, generation);

        Assert.False(arena.IsValid(index, generation));
    }

    [Fact]
    public void FullLifecycle_AccessAfterDispose_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();
        arena.Deallocate(index, generation);

        var result = arena.TryGet(index, generation, out var entity);

        Assert.False(result);
        Assert.Null(entity);
    }

    [Fact]
    public void FullLifecycle_CompleteSequence()
    {
        var arena = new TestArena(16);

        // Create
        var (index, generation) = arena.Allocate();
        Assert.True(arena.IsValid(index, generation));

        // Get
        Assert.True(arena.TryGet(index, generation, out var entity));
        Assert.NotNull(entity);

        // Modify
        entity!.Value = 100;
        entity.Name = "Player";

        // Verify modification
        Assert.True(arena.TryGet(index, generation, out var modified));
        Assert.Equal(100, modified!.Value);
        Assert.Equal("Player", modified.Name);

        // Dispose
        Assert.True(arena.Deallocate(index, generation));

        // Access after dispose (should fail)
        Assert.False(arena.IsValid(index, generation));
        Assert.False(arena.TryGet(index, generation, out _));
    }

    [Fact]
    public void FullLifecycle_MultipleEntities_IndependentLifecycles()
    {
        var arena = new TestArena(16);

        var handle1 = arena.Allocate();
        var handle2 = arena.Allocate();
        var handle3 = arena.Allocate();

        // Set values
        arena.TryGet(handle1.Index, handle1.Generation, out var e1);
        arena.TryGet(handle2.Index, handle2.Generation, out var e2);
        arena.TryGet(handle3.Index, handle3.Generation, out var e3);

        e1!.Value = 1;
        e2!.Value = 2;
        e3!.Value = 3;

        // Dispose middle entity
        arena.Deallocate(handle2.Index, handle2.Generation);

        // Verify states
        Assert.True(arena.IsValid(handle1.Index, handle1.Generation));
        Assert.False(arena.IsValid(handle2.Index, handle2.Generation));
        Assert.True(arena.IsValid(handle3.Index, handle3.Generation));

        // Verify values still accessible for valid handles
        arena.TryGet(handle1.Index, handle1.Generation, out var check1);
        arena.TryGet(handle3.Index, handle3.Generation, out var check3);

        Assert.Equal(1, check1!.Value);
        Assert.Equal(3, check3!.Value);
    }

    #endregion

    #region Array of Handles to Multiple Entities Tests

    [Fact]
    public void ArrayOfHandles_AllShouldBeValid()
    {
        var arena = new TestArena(100);
        var handles = new List<(int Index, int Generation)>();

        for (int i = 0; i < 50; i++)
        {
            handles.Add(arena.Allocate());
        }

        foreach (var (index, generation) in handles)
        {
            Assert.True(arena.IsValid(index, generation));
        }
    }

    [Fact]
    public void ArrayOfHandles_EachShouldReferToDifferentEntity()
    {
        var arena = new TestArena(100);
        var handles = new List<(int Index, int Generation)>();
        var entities = new HashSet<TestEntity>();

        for (int i = 0; i < 10; i++)
        {
            var handle = arena.Allocate();
            handles.Add(handle);

            arena.TryGet(handle.Index, handle.Generation, out var entity);
            entities.Add(entity!);
        }

        Assert.Equal(10, entities.Count);
    }

    [Fact]
    public void ArrayOfHandles_ModificationsShouldBeIndependent()
    {
        var arena = new TestArena(100);
        var handles = new List<(int Index, int Generation)>();

        for (int i = 0; i < 10; i++)
        {
            var handle = arena.Allocate();
            handles.Add(handle);

            arena.TryGet(handle.Index, handle.Generation, out var entity);
            entity!.Value = i * 10;
        }

        for (int i = 0; i < handles.Count; i++)
        {
            arena.TryGet(handles[i].Index, handles[i].Generation, out var entity);
            Assert.Equal(i * 10, entity!.Value);
        }
    }

    [Fact]
    public void ArrayOfHandles_PartialDeallocation()
    {
        var arena = new TestArena(100);
        var handles = new List<(int Index, int Generation)>();

        for (int i = 0; i < 20; i++)
        {
            handles.Add(arena.Allocate());
        }

        // Deallocate every other handle
        for (int i = 0; i < handles.Count; i += 2)
        {
            arena.Deallocate(handles[i].Index, handles[i].Generation);
        }

        // Verify states
        for (int i = 0; i < handles.Count; i++)
        {
            var expectedValid = i % 2 != 0;
            Assert.Equal(expectedValid, arena.IsValid(handles[i].Index, handles[i].Generation));
        }
    }

    [Fact]
    public void ArrayOfHandles_ReallocationAfterDeallocation()
    {
        var arena = new TestArena(4);
        var handles = new List<(int Index, int Generation)>();

        // Fill arena
        for (int i = 0; i < 4; i++)
        {
            handles.Add(arena.Allocate());
        }

        // Deallocate all
        foreach (var handle in handles)
        {
            arena.Deallocate(handle.Index, handle.Generation);
        }

        handles.Clear();

        // Reallocate
        for (int i = 0; i < 4; i++)
        {
            handles.Add(arena.Allocate());
        }

        // All should be valid with incremented generations
        foreach (var handle in handles)
        {
            Assert.True(arena.IsValid(handle.Index, handle.Generation));
            Assert.Equal(2, handle.Generation); // Generation 1 -> 2 after reallocation
        }
    }

    [Fact]
    public void ArrayOfHandles_LargeScale()
    {
        var arena = new TestArena(16);
        var handles = new List<(int Index, int Generation)>();

        // Create many entities (forcing expansion)
        for (int i = 0; i < 1000; i++)
        {
            handles.Add(arena.Allocate());
        }

        Assert.Equal(1000, arena.Count);

        // All should be valid
        foreach (var handle in handles)
        {
            Assert.True(arena.IsValid(handle.Index, handle.Generation));
        }
    }

    #endregion

    #region Handle Equality in Collections Tests

    [Fact]
    public void HandleEquality_SameHandle_ShouldBeEqual()
    {
        var handle1 = (Index: 5, Generation: 3);
        var handle2 = (Index: 5, Generation: 3);

        Assert.Equal(handle1, handle2);
    }

    [Fact]
    public void HandleEquality_DifferentIndex_ShouldNotBeEqual()
    {
        var handle1 = (Index: 5, Generation: 3);
        var handle2 = (Index: 6, Generation: 3);

        Assert.NotEqual(handle1, handle2);
    }

    [Fact]
    public void HandleEquality_DifferentGeneration_ShouldNotBeEqual()
    {
        var handle1 = (Index: 5, Generation: 3);
        var handle2 = (Index: 5, Generation: 4);

        Assert.NotEqual(handle1, handle2);
    }

    [Fact]
    public void HandleEquality_InHashSet_ShouldWorkCorrectly()
    {
        var handles = new HashSet<(int Index, int Generation)>();

        handles.Add((0, 1));
        handles.Add((1, 1));
        handles.Add((0, 1)); // Duplicate

        Assert.Equal(2, handles.Count);
        Assert.Contains((0, 1), handles);
        Assert.Contains((1, 1), handles);
    }

    [Fact]
    public void HandleEquality_InDictionary_ShouldWorkAsKey()
    {
        var handleData = new Dictionary<(int Index, int Generation), string>();

        handleData[(0, 1)] = "Entity0";
        handleData[(1, 1)] = "Entity1";
        handleData[(0, 1)] = "Entity0Updated"; // Update

        Assert.Equal(2, handleData.Count);
        Assert.Equal("Entity0Updated", handleData[(0, 1)]);
        Assert.Equal("Entity1", handleData[(1, 1)]);
    }

    [Fact]
    public void HandleEquality_AfterReallocation_OldAndNewShouldNotBeEqual()
    {
        var arena = new TestArena(4);

        var oldHandle = arena.Allocate();
        arena.Deallocate(oldHandle.Index, oldHandle.Generation);
        var newHandle = arena.Allocate();

        // Same index, different generation
        Assert.Equal(oldHandle.Index, newHandle.Index);
        Assert.NotEqual(oldHandle.Generation, newHandle.Generation);
        Assert.NotEqual(oldHandle, newHandle);
    }

    [Fact]
    public void HandleEquality_TrackingValidHandlesInHashSet()
    {
        var arena = new TestArena(16);
        var validHandles = new HashSet<(int Index, int Generation)>();

        // Create entities and track
        for (int i = 0; i < 5; i++)
        {
            validHandles.Add(arena.Allocate());
        }

        Assert.Equal(5, validHandles.Count);

        // Remove one
        var toRemove = validHandles.First();
        arena.Deallocate(toRemove.Index, toRemove.Generation);
        validHandles.Remove(toRemove);

        Assert.Equal(4, validHandles.Count);

        // All remaining should be valid
        foreach (var handle in validHandles)
        {
            Assert.True(arena.IsValid(handle.Index, handle.Generation));
        }
    }

    #endregion

    #region Callback Integration (Spawn Initializes, Despawn Cleans Up) Tests

    [Fact]
    public void Callback_SpawnInitializes_EntityValues()
    {
        int spawnCount = 0;
        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                spawnCount++;
                e.Value = 100;
                e.Name = $"Entity{spawnCount}";
            },
            null);

        var (index, generation) = arena.Allocate();
        arena.TryGet(index, generation, out var entity);

        Assert.Equal(100, entity!.Value);
        Assert.Equal("Entity1", entity.Name);
    }

    [Fact]
    public void Callback_DespawnCleansUp_EntityValues()
    {
        TestEntity? despawnedEntity = null;
        var arena = new TestArena(16,
            null,
            (ref TestEntity e) =>
            {
                despawnedEntity = e;
                e.Value = 0;
                e.Name = null;
            });

        var (index, generation) = arena.Allocate();
        arena.TryGet(index, generation, out var entity);
        entity!.Value = 42;
        entity.Name = "Active";

        arena.Deallocate(index, generation);

        Assert.Same(entity, despawnedEntity);
        Assert.Equal(0, despawnedEntity!.Value);
        Assert.Null(despawnedEntity.Name);
    }

    [Fact]
    public void Callback_SpawnAndDespawn_FullCycle()
    {
        var spawnLog = new List<int>();
        var despawnLog = new List<int>();
        int nextId = 0;

        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                e.Value = nextId++;
                spawnLog.Add(e.Value);
            },
            (ref TestEntity e) =>
            {
                despawnLog.Add(e.Value);
            });

        var handle1 = arena.Allocate();
        var handle2 = arena.Allocate();
        var handle3 = arena.Allocate();

        arena.Deallocate(handle2.Index, handle2.Generation);
        arena.Deallocate(handle1.Index, handle1.Generation);

        Assert.Equal(new[] { 0, 1, 2 }, spawnLog);
        Assert.Equal(new[] { 1, 0 }, despawnLog);
    }

    [Fact]
    public void Callback_SpawnReusesEntity_WithCleanState()
    {
        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                e.Value = 0;
                e.Name = "New";
            },
            (ref TestEntity e) =>
            {
                e.Value = -1;
                e.Name = "Recycled";
            });

        var handle1 = arena.Allocate();
        arena.TryGet(handle1.Index, handle1.Generation, out var entity1);
        entity1!.Value = 999;

        arena.Deallocate(handle1.Index, handle1.Generation);

        // Reallocate same slot
        var handle2 = arena.Allocate();
        arena.TryGet(handle2.Index, handle2.Generation, out var entity2);

        // Spawn callback should have reset the value
        Assert.Equal(0, entity2!.Value);
        Assert.Equal("New", entity2.Name);
    }

    [Fact]
    public void Callback_MultipleEntities_IndependentCallbacks()
    {
        var callbackOrder = new List<string>();
        var arena = new TestArena(16,
            (ref TestEntity e) => callbackOrder.Add($"spawn-{e.GetHashCode() % 100}"),
            (ref TestEntity e) => callbackOrder.Add($"despawn-{e.GetHashCode() % 100}"));

        var h1 = arena.Allocate();
        var h2 = arena.Allocate();
        arena.Deallocate(h1.Index, h1.Generation);
        arena.Deallocate(h2.Index, h2.Generation);

        Assert.Equal(4, callbackOrder.Count);
        Assert.StartsWith("spawn", callbackOrder[0]);
        Assert.StartsWith("spawn", callbackOrder[1]);
        Assert.StartsWith("despawn", callbackOrder[2]);
        Assert.StartsWith("despawn", callbackOrder[3]);
    }

    [Fact]
    public void Callback_EntityReuseWithCallbacks_MaintainsCorrectState()
    {
        var arena = new TestArena(1,
            (ref TestEntity e) => e.Value = 1,
            (ref TestEntity e) => e.Value = -1);

        // First cycle
        var h1 = arena.Allocate();
        arena.TryGet(h1.Index, h1.Generation, out var e1);
        Assert.Equal(1, e1!.Value);

        e1.Value = 100;
        arena.Deallocate(h1.Index, h1.Generation);
        // After deallocation, the old entity has despawn callback applied
        Assert.Equal(-1, e1.Value);

        // Second cycle - reuses same slot (but new entity instance due to reset)
        var h2 = arena.Allocate();
        arena.TryGet(h2.Index, h2.Generation, out var e2);

        // Same slot index is reused
        Assert.Equal(h1.Index, h2.Index);
        // New entity is initialized by spawn callback
        Assert.Equal(1, e2!.Value);
    }

    [Fact]
    public void Callback_CallbackOrder_SpawnBeforeReturn()
    {
        bool spawnCalled = false;
        int valueInCallback = 0;

        var arena = new TestArena(16,
            (ref TestEntity e) =>
            {
                spawnCalled = true;
                e.Value = 42;
                valueInCallback = e.Value;
            },
            null);

        var (index, generation) = arena.Allocate();

        Assert.True(spawnCalled);
        Assert.Equal(42, valueInCallback);

        arena.TryGet(index, generation, out var entity);
        Assert.Equal(42, entity!.Value);
    }

    [Fact]
    public void Callback_DespawnOrder_BeforeGenerationIncrement()
    {
        int generationInCallback = 0;
        var arena = new TestArenaWithGenerationAccess(16,
            null,
            (entity, getGeneration) =>
            {
                generationInCallback = getGeneration();
            });

        var (index, generation) = arena.AllocateWithCallback();
        arena.DeallocateWithCallback(index, generation);

        // Despawn should see the current generation before it's incremented
        Assert.Equal(generation, generationInCallback);
    }

    #endregion

    #region Complex Integration Scenarios Tests

    [Fact]
    public void ComplexScenario_EntityPool_SimulatesGameLoop()
    {
        var spawnedEntities = new List<(int Index, int Generation)>();
        var arena = new TestArena(8,
            (ref TestEntity e) => e.Value = 100, // Initial health
            (ref TestEntity e) => e.Value = 0);  // Reset on despawn

        // Spawn phase
        for (int i = 0; i < 5; i++)
        {
            spawnedEntities.Add(arena.Allocate());
        }

        // Update phase - simulate damage
        foreach (var handle in spawnedEntities)
        {
            arena.TryGet(handle.Index, handle.Generation, out var entity);
            entity!.Value -= 20;
        }

        // Verify all have 80 health
        foreach (var handle in spawnedEntities)
        {
            arena.TryGet(handle.Index, handle.Generation, out var entity);
            Assert.Equal(80, entity!.Value);
        }

        // Despawn first 2
        arena.Deallocate(spawnedEntities[0].Index, spawnedEntities[0].Generation);
        arena.Deallocate(spawnedEntities[1].Index, spawnedEntities[1].Generation);

        // Spawn 3 new ones (should reuse slots)
        for (int i = 0; i < 3; i++)
        {
            var newHandle = arena.Allocate();
            arena.TryGet(newHandle.Index, newHandle.Generation, out var entity);
            Assert.Equal(100, entity!.Value); // Fresh health
        }

        Assert.Equal(6, arena.Count);
    }

    [Fact]
    public void ComplexScenario_HandleCaching_RevalidationNeeded()
    {
        var arena = new TestArena(4);
        var cachedHandle = arena.Allocate();

        // Cache the handle
        Assert.True(arena.IsValid(cachedHandle.Index, cachedHandle.Generation));

        // Deallocate through some other reference
        arena.Deallocate(cachedHandle.Index, cachedHandle.Generation);

        // Cached handle is now invalid
        Assert.False(arena.IsValid(cachedHandle.Index, cachedHandle.Generation));

        // New allocation reuses slot
        var newHandle = arena.Allocate();
        Assert.Equal(cachedHandle.Index, newHandle.Index);
        Assert.NotEqual(cachedHandle.Generation, newHandle.Generation);

        // Old cached handle still invalid
        Assert.False(arena.IsValid(cachedHandle.Index, cachedHandle.Generation));

        // New handle is valid
        Assert.True(arena.IsValid(newHandle.Index, newHandle.Generation));
    }

    [Fact]
    public void ComplexScenario_ParentChildRelationships()
    {
        var arena = new TestArena(16);

        // Create parent
        var parentHandle = arena.Allocate();
        arena.TryGet(parentHandle.Index, parentHandle.Generation, out var parent);
        parent!.Name = "Parent";
        parent.Value = parentHandle.Index * 1000 + parentHandle.Generation;

        // Create children that reference parent via stored handle info
        var childHandles = new List<(int Index, int Generation)>();
        for (int i = 0; i < 3; i++)
        {
            var childHandle = arena.Allocate();
            childHandles.Add(childHandle);

            arena.TryGet(childHandle.Index, childHandle.Generation, out var child);
            child!.Name = $"Child{i}";
            child.ParentIndex = parentHandle.Index;
            child.ParentGeneration = parentHandle.Generation;
        }

        // Verify children can find parent
        foreach (var childHandle in childHandles)
        {
            arena.TryGet(childHandle.Index, childHandle.Generation, out var child);
            Assert.True(arena.IsValid(child!.ParentIndex, child.ParentGeneration));
        }

        // Destroy parent
        arena.Deallocate(parentHandle.Index, parentHandle.Generation);

        // Children's parent references are now invalid
        foreach (var childHandle in childHandles)
        {
            arena.TryGet(childHandle.Index, childHandle.Generation, out var child);
            Assert.False(arena.IsValid(child!.ParentIndex, child.ParentGeneration));
        }
    }

    #endregion

    #region Helper Classes

    private class TestEntity
    {
        public int Value { get; set; }
        public string? Name { get; set; }
        public int ParentIndex { get; set; } = -1;
        public int ParentGeneration { get; set; } = 0;
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
        private readonly Action<TestEntity, Func<int>>? _onDespawnWithGeneration;

        public TestArenaWithGenerationAccess(
            int initialCapacity,
            RefAction<TestEntity>? onSpawn,
            Action<TestEntity, Func<int>>? onDespawnWithGeneration)
            : base(initialCapacity, onSpawn, null)
        {
            _onDespawnWithGeneration = onDespawnWithGeneration;
        }

        private int _lastDeallocateIndex;

        public (int Index, int Generation) AllocateWithCallback()
        {
            lock (_lock)
            {
                var index = AllocateInternal(out var generation);
                return (index, generation);
            }
        }

        public bool DeallocateWithCallback(int index, int generation)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _entities.Length)
                {
                    return false;
                }

                if (_generations[index] != generation)
                {
                    return false;
                }

                _lastDeallocateIndex = index;

                // Invoke custom despawn callback with generation accessor
                _onDespawnWithGeneration?.Invoke(_entities[index], () => _generations[_lastDeallocateIndex]);

                return DeallocateInternal(index, generation);
            }
        }
    }

    #endregion
}
