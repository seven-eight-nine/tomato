using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class SnapshotTests
{
    private static SimpleInventory<TestItem> CreateInventory(int id, int capacity = 10)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(id),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    [Fact]
    public void CreateSnapshot_ShouldCaptureCurrentState()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));
        inventory.TryAdd(new TestItem(2, "Shield"));

        var snapshot = inventory.CreateSnapshot();

        Assert.True(snapshot.Id.IsValid);
        Assert.Equal(inventory.Id, snapshot.InventoryId);
        Assert.True(snapshot.Data.Length > 0);
    }

    [Fact]
    public void RestoreFromSnapshot_ShouldRestoreState()
    {
        var inventory = CreateInventory(1);
        var item1 = new TestItem(1, "Sword");
        var item2 = new TestItem(2, "Shield");
        inventory.TryAdd(item1);
        inventory.TryAdd(item2);

        var snapshot = inventory.CreateSnapshot();

        inventory.Clear();
        inventory.TryAdd(new TestItem(3, "NewItem"));

        inventory.RestoreFromSnapshot(snapshot);

        Assert.Equal(2, inventory.Count);
    }

    [Fact]
    public void SnapshotManager_CreateSnapshot_ShouldStoreSnapshot()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));

        var manager = new SnapshotManager<TestItem>();
        var snapshotId = manager.CreateSnapshot(inventory);

        Assert.True(snapshotId.IsValid);
        Assert.Equal(1, manager.SnapshotCount);
    }

    [Fact]
    public void SnapshotManager_GetSnapshot_ShouldReturnStoredSnapshot()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));

        var manager = new SnapshotManager<TestItem>();
        var snapshotId = manager.CreateSnapshot(inventory);

        var retrieved = manager.GetSnapshot(snapshotId);

        Assert.NotNull(retrieved);
        Assert.Equal(snapshotId, retrieved.Id);
    }

    [Fact]
    public void SnapshotManager_RestoreSnapshot_ShouldRestoreState()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));
        inventory.TryAdd(new TestItem(2, "Shield"));

        var manager = new SnapshotManager<TestItem>();
        var snapshotId = manager.CreateSnapshot(inventory);

        inventory.Clear();
        Assert.Equal(0, inventory.Count);

        var success = manager.TryRestoreSnapshot(snapshotId, inventory);

        Assert.True(success);
        Assert.Equal(2, inventory.Count);
    }

    [Fact]
    public void SnapshotManager_RestoreLatest_ShouldRestoreLastSnapshot()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "First"));

        var manager = new SnapshotManager<TestItem>();
        manager.CreateSnapshot(inventory);

        inventory.TryAdd(new TestItem(2, "Second"));
        manager.CreateSnapshot(inventory);

        inventory.Clear();
        var success = manager.TryRestoreLatest(inventory);

        Assert.True(success);
        Assert.Equal(2, inventory.Count);
    }

    [Fact]
    public void SnapshotManager_RemoveSnapshot_ShouldRemoveStoredSnapshot()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));

        var manager = new SnapshotManager<TestItem>();
        var snapshotId = manager.CreateSnapshot(inventory);

        var removed = manager.TryRemoveSnapshot(snapshotId);

        Assert.True(removed);
        Assert.Null(manager.GetSnapshot(snapshotId));
        Assert.Equal(0, manager.SnapshotCount);
    }

    [Fact]
    public void SnapshotManager_ClearSnapshots_ShouldRemoveAllForInventory()
    {
        var inventory1 = CreateInventory(1);
        var inventory2 = CreateInventory(2);
        inventory1.TryAdd(new TestItem(1, "Item1"));
        inventory2.TryAdd(new TestItem(2, "Item2"));

        var manager = new SnapshotManager<TestItem>();
        manager.CreateSnapshot(inventory1);
        manager.CreateSnapshot(inventory1);
        manager.CreateSnapshot(inventory2);

        var cleared = manager.ClearSnapshots(inventory1.Id);

        Assert.Equal(2, cleared);
        Assert.Equal(1, manager.SnapshotCount);
    }

    [Fact]
    public void SnapshotManager_MaxSnapshots_ShouldEvictOldest()
    {
        var inventory = CreateInventory(1);
        var manager = new SnapshotManager<TestItem>(maxSnapshotsPerInventory: 2);

        inventory.TryAdd(new TestItem(1, "First"));
        var id1 = manager.CreateSnapshot(inventory);

        inventory.TryAdd(new TestItem(2, "Second"));
        var id2 = manager.CreateSnapshot(inventory);

        inventory.TryAdd(new TestItem(3, "Third"));
        var id3 = manager.CreateSnapshot(inventory);

        Assert.Null(manager.GetSnapshot(id1));
        Assert.NotNull(manager.GetSnapshot(id2));
        Assert.NotNull(manager.GetSnapshot(id3));
        Assert.Equal(2, manager.GetSnapshotCount(inventory.Id));
    }

    [Fact]
    public void SnapshotManager_GetSnapshotIds_ShouldReturnAllIdsForInventory()
    {
        var inventory = CreateInventory(1);
        var manager = new SnapshotManager<TestItem>();

        inventory.TryAdd(new TestItem(1, "Item"));
        manager.CreateSnapshot(inventory);
        manager.CreateSnapshot(inventory);
        manager.CreateSnapshot(inventory);

        var ids = manager.GetSnapshotIds(inventory.Id);

        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public void SnapshotManager_ClearAll_ShouldRemoveAllSnapshots()
    {
        var inventory1 = CreateInventory(1);
        var inventory2 = CreateInventory(2);
        inventory1.TryAdd(new TestItem(1, "Item1"));
        inventory2.TryAdd(new TestItem(2, "Item2"));

        var manager = new SnapshotManager<TestItem>();
        manager.CreateSnapshot(inventory1);
        manager.CreateSnapshot(inventory2);

        manager.ClearAll();

        Assert.Equal(0, manager.SnapshotCount);
    }

    #region HasSnapshot / HasSnapshotFor Tests

    [Fact]
    public void HasSnapshot_WhenExists_ShouldReturnTrue()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));

        var manager = new SnapshotManager<TestItem>();
        var snapshotId = manager.CreateSnapshot(inventory);

        Assert.True(manager.HasSnapshot(snapshotId));
    }

    [Fact]
    public void HasSnapshot_WhenNotExists_ShouldReturnFalse()
    {
        var manager = new SnapshotManager<TestItem>();

        Assert.False(manager.HasSnapshot(new SnapshotId(999)));
    }

    [Fact]
    public void HasSnapshotFor_WhenInventoryHasSnapshots_ShouldReturnTrue()
    {
        var inventory = CreateInventory(1);
        inventory.TryAdd(new TestItem(1, "Sword"));

        var manager = new SnapshotManager<TestItem>();
        manager.CreateSnapshot(inventory);

        Assert.True(manager.HasSnapshotFor(inventory.Id));
    }

    [Fact]
    public void HasSnapshotFor_WhenInventoryHasNoSnapshots_ShouldReturnFalse()
    {
        var inventory = CreateInventory(1);
        var manager = new SnapshotManager<TestItem>();

        Assert.False(manager.HasSnapshotFor(inventory.Id));
    }

    #endregion
}
