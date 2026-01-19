using System;
using System.Collections.Generic;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Runtime;

/// <summary>
/// EntityContainer tests - TDD t-wada style
///
/// TODOリスト:
/// - [x] EntityContainerを作成できる
/// - [x] Addでハンドルを追加できる
/// - [x] インデックスでアクセスできる
/// - [x] Capacityが正しく返される
/// - [x] 無効になったスロットにAddすると再利用される
/// - [x] イテレータで有効なハンドルのみイテレートできる
/// - [x] イテレータでskip/offsetによるフレーム分散ができる
/// </summary>
public class EntityContainerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldCreateEmptyContainer()
    {
        var container = new EntityContainer<MockHandle>();

        Assert.Equal(0, container.Capacity);
    }

    [Fact]
    public void Constructor_WithInitialCapacity_ShouldPreallocate()
    {
        var container = new EntityContainer<MockHandle>(16);

        Assert.Equal(0, container.Capacity); // Capacityは追加された数
    }

    #endregion

    #region Add Tests

    [Fact]
    public void Add_ShouldIncreaseCapacity()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        var handle = new MockHandle(1, arena);

        container.Add(handle);

        Assert.Equal(1, container.Capacity);
    }

    [Fact]
    public void Add_MultipleTimes_ShouldIncreaseCapacity()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        arena.SetValid(3, true);

        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));
        container.Add(new MockHandle(3, arena));

        Assert.Equal(3, container.Capacity);
    }

    [Fact]
    public void Add_ToInvalidSlot_ShouldReuseSlot()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));

        // handle1を無効にする
        arena.SetValid(1, false);

        // 新しいハンドルを追加
        arena.SetValid(3, true);
        container.Add(new MockHandle(3, arena));

        // Capacityは増えない（再利用されたため）
        Assert.Equal(2, container.Capacity);
    }

    #endregion

    #region Indexer Tests

    [Fact]
    public void Indexer_ShouldReturnHandle()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(42, true);
        container.Add(new MockHandle(42, arena));

        var retrieved = container[0];

        Assert.Equal(42, retrieved.Id);
    }

    [Fact]
    public void Indexer_MultipleHandles_ShouldReturnCorrectHandle()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        arena.SetValid(3, true);
        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));
        container.Add(new MockHandle(3, arena));

        Assert.Equal(1, container[0].Id);
        Assert.Equal(2, container[1].Id);
        Assert.Equal(3, container[2].Id);
    }

    #endregion

    #region Iterator Basic Tests

    [Fact]
    public void GetIterator_EmptyContainer_ShouldNotIterate()
    {
        var container = new EntityContainer<MockHandle>();
        var count = 0;

        var iterator = container.GetIterator();
        while (iterator.MoveNext())
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetIterator_AllValid_ShouldIterateAll()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        arena.SetValid(3, true);
        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));
        container.Add(new MockHandle(3, arena));

        var ids = new List<int>();
        var iterator = container.GetIterator();
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        Assert.Equal(3, ids.Count);
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
        Assert.Contains(3, ids);
    }

    [Fact]
    public void GetIterator_SomeInvalid_ShouldSkipInvalid()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        arena.SetValid(3, true);
        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));
        container.Add(new MockHandle(3, arena));

        // handle2を無効にする
        arena.SetValid(2, false);

        var ids = new List<int>();
        var iterator = container.GetIterator();
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        Assert.Equal(2, ids.Count);
        Assert.Contains(1, ids);
        Assert.Contains(3, ids);
        Assert.DoesNotContain(2, ids);
    }

    #endregion

    #region Iterator Skip/Offset Tests

    [Fact]
    public void GetIterator_Skip0Offset0_ShouldIterateAll()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 6; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        var ids = new List<int>();
        var iterator = container.GetIterator(skip: 0, offset: 0);
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, ids);
    }

    [Fact]
    public void GetIterator_Skip1Offset0_ShouldIterateEveryOther()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 6; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        var ids = new List<int>();
        var iterator = container.GetIterator(skip: 1, offset: 0);
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        // 0, 2, 4
        Assert.Equal(new[] { 0, 2, 4 }, ids);
    }

    [Fact]
    public void GetIterator_Skip1Offset1_ShouldIterateEveryOtherStartingFrom1()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 6; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        var ids = new List<int>();
        var iterator = container.GetIterator(skip: 1, offset: 1);
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        // 1, 3, 5
        Assert.Equal(new[] { 1, 3, 5 }, ids);
    }

    [Fact]
    public void GetIterator_Skip1_BothOffsets_ShouldCoverAll()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 6; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        var allIds = new HashSet<int>();

        // Frame 1: offset=0
        var iterator1 = container.GetIterator(skip: 1, offset: 0);
        while (iterator1.MoveNext())
        {
            allIds.Add(iterator1.Current.Id);
        }

        // Frame 2: offset=1
        var iterator2 = container.GetIterator(skip: 1, offset: 1);
        while (iterator2.MoveNext())
        {
            allIds.Add(iterator2.Current.Id);
        }

        Assert.Equal(6, allIds.Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.Contains(i, allIds);
        }
    }

    [Fact]
    public void GetIterator_Skip2_ThreeOffsets_ShouldCoverAll()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 9; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        var allIds = new HashSet<int>();

        // Frame 1: offset=0 -> 0, 3, 6
        var iterator1 = container.GetIterator(skip: 2, offset: 0);
        while (iterator1.MoveNext())
        {
            allIds.Add(iterator1.Current.Id);
        }

        // Frame 2: offset=1 -> 1, 4, 7
        var iterator2 = container.GetIterator(skip: 2, offset: 1);
        while (iterator2.MoveNext())
        {
            allIds.Add(iterator2.Current.Id);
        }

        // Frame 3: offset=2 -> 2, 5, 8
        var iterator3 = container.GetIterator(skip: 2, offset: 2);
        while (iterator3.MoveNext())
        {
            allIds.Add(iterator3.Current.Id);
        }

        Assert.Equal(9, allIds.Count);
    }

    [Fact]
    public void GetIterator_WithSkip_ShouldSkipInvalidHandles()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        for (int i = 0; i < 6; i++)
        {
            arena.SetValid(i, true);
            container.Add(new MockHandle(i, arena));
        }

        // Make index 2 invalid
        arena.SetValid(2, false);

        var ids = new List<int>();
        var iterator = container.GetIterator(skip: 1, offset: 0);
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        // 0, 2(invalid->skip), 4
        Assert.Equal(new[] { 0, 4 }, ids);
    }

    #endregion

    #region FreeHint Update Tests

    [Fact]
    public void Iterator_ShouldUpdateFreeHint_WhenInvalidFound()
    {
        var arena = new MockArena();
        var container = new EntityContainer<MockHandle>();
        arena.SetValid(1, true);
        arena.SetValid(2, true);
        arena.SetValid(3, true);
        container.Add(new MockHandle(1, arena));
        container.Add(new MockHandle(2, arena));
        container.Add(new MockHandle(3, arena));

        // handle2を無効にする
        arena.SetValid(2, false);

        // イテレートして無効スロットを検出
        var iterator = container.GetIterator();
        while (iterator.MoveNext()) { }

        // 新しいハンドルを追加すると、index 1 (handle2の位置) が再利用される
        arena.SetValid(4, true);
        container.Add(new MockHandle(4, arena));

        Assert.Equal(3, container.Capacity); // 増えていない
        Assert.Equal(4, container[1].Id); // index 1 に追加された
    }

    #endregion

    #region Mock Implementation

    /// <summary>
    /// テスト用のモックArena。有効性を管理する。
    /// </summary>
    private class MockArena
    {
        private readonly HashSet<int> _validIds = new();

        public void SetValid(int id, bool isValid)
        {
            if (isValid)
                _validIds.Add(id);
            else
                _validIds.Remove(id);
        }

        public bool IsValid(int id) => _validIds.Contains(id);
    }

    /// <summary>
    /// テスト用のモックハンドル（struct）。
    /// 実際のEntityHandleと同様にArenaへの参照を持つ。
    /// </summary>
    private readonly struct MockHandle : IEntityHandle
    {
        private readonly MockArena _arena;
        public readonly int Id;

        public MockHandle(int id, MockArena arena)
        {
            Id = id;
            _arena = arena;
        }

        public bool IsValid => _arena != null && _arena.IsValid(Id);
    }

    #endregion
}
