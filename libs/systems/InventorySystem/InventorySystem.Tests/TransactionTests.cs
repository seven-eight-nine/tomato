using System;
using Tomato.SerializationSystem;
using Xunit;

namespace Tomato.InventorySystem.Tests;

public class TransactionTests
{
    private static SimpleInventory<TestItem> CreateInventory(int id, int capacity = 10)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(id),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    #region InventoryTransaction Tests

    [Fact]
    public void Transaction_Commit_PreservesChanges()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using var transaction = InventoryTransaction<TestItem>.Begin(inventory);
        inventory.TryRemove(item.InstanceId);
        transaction.Commit();

        // コミット後は変更が保持される
        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void Transaction_Rollback_RevertChanges()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using var transaction = InventoryTransaction<TestItem>.Begin(inventory);
        inventory.TryRemove(item.InstanceId);
        transaction.Rollback();

        // ロールバック後は元に戻る
        Assert.Equal(1, inventory.Count);
        Assert.NotNull(inventory.Get(item.InstanceId));
    }

    [Fact]
    public void Transaction_DisposeWithoutCommit_RollsBack()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using (var transaction = InventoryTransaction<TestItem>.Begin(inventory))
        {
            inventory.TryRemove(item.InstanceId);
            // Commitを呼ばずにDispose
        }

        // 自動ロールバック
        Assert.Equal(1, inventory.Count);
    }

    [Fact]
    public void Transaction_MultipleInventories_RollbackAll()
    {
        var inv1 = CreateInventory(1);
        var inv2 = CreateInventory(2);
        var item1 = new TestItem(1, "Sword");
        var item2 = new TestItem(2, "Shield");
        inv1.TryAdd(item1);
        inv2.TryAdd(item2);

        using (var transaction = InventoryTransaction<TestItem>.Begin(inv1, inv2))
        {
            inv1.TryRemove(item1.InstanceId);
            inv2.TryRemove(item2.InstanceId);
            // Commitせずに終了
        }

        // 両方ロールバック
        Assert.Equal(1, inv1.Count);
        Assert.Equal(1, inv2.Count);
    }

    [Fact]
    public void Transaction_EnlistLater_Works()
    {
        var inv1 = CreateInventory(1);
        var inv2 = CreateInventory(2);
        var item1 = new TestItem(1, "Sword");
        var item2 = new TestItem(2, "Shield");
        inv1.TryAdd(item1);
        inv2.TryAdd(item2);

        using var transaction = InventoryTransaction<TestItem>.Begin();
        transaction.Enlist(inv1);
        inv1.TryRemove(item1.InstanceId);

        transaction.Enlist(inv2);
        inv2.TryRemove(item2.InstanceId);

        transaction.Rollback();

        Assert.Equal(1, inv1.Count);
        Assert.Equal(1, inv2.Count);
    }

    [Fact]
    public void Transaction_DoubleEnlist_Ignored()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using var transaction = InventoryTransaction<TestItem>.Begin(inventory);
        inventory.TryRemove(item.InstanceId);

        // 同じインベントリを再度登録（最初のスナップショットが維持される）
        inventory.TryAdd(new TestItem(2, "Shield"));
        transaction.Enlist(inventory);

        transaction.Rollback();

        // 最初のスナップショット（Sword 1個）に戻る
        Assert.Equal(1, inventory.Count);
        Assert.NotNull(inventory.Get(item.InstanceId));
    }

    [Fact]
    public void Transaction_CommitAfterCommit_Throws()
    {
        var inventory = CreateInventory(1);
        using var transaction = InventoryTransaction<TestItem>.Begin(inventory);
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    [Fact]
    public void Transaction_RollbackAfterCommit_Throws()
    {
        var inventory = CreateInventory(1);
        using var transaction = InventoryTransaction<TestItem>.Begin(inventory);
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
    }

    [Fact]
    public void Transaction_EnlistAfterCommit_Throws()
    {
        var inventory = CreateInventory(1);
        using var transaction = InventoryTransaction<TestItem>.Begin();
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Enlist(inventory));
    }

    #endregion

    #region TransactionScope Tests

    [Fact]
    public void TransactionScope_Complete_Commits()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using (var scope = new TransactionScope<TestItem>(inventory))
        {
            inventory.TryRemove(item.InstanceId);
            scope.Complete();
        }

        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void TransactionScope_WithoutComplete_RollsBack()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        using (var scope = new TransactionScope<TestItem>(inventory))
        {
            inventory.TryRemove(item.InstanceId);
            // Complete()を呼ばない
        }

        Assert.Equal(1, inventory.Count);
    }

    [Fact]
    public void TransactionScope_EnlistDuringScope_Works()
    {
        var inv1 = CreateInventory(1);
        var inv2 = CreateInventory(2);
        var item1 = new TestItem(1, "Sword");
        var item2 = new TestItem(2, "Shield");
        inv1.TryAdd(item1);
        inv2.TryAdd(item2);

        using (var scope = new TransactionScope<TestItem>(inv1))
        {
            inv1.TryRemove(item1.InstanceId);

            scope.Enlist(inv2);
            inv2.TryRemove(item2.InstanceId);

            // Complete()を呼ばない
        }

        Assert.Equal(1, inv1.Count);
        Assert.Equal(1, inv2.Count);
    }

    [Fact]
    public void TransactionScope_ExceptionDuringScope_RollsBack()
    {
        var inventory = CreateInventory(1);
        var item = new TestItem(1, "Sword");
        inventory.TryAdd(item);

        try
        {
            using (var scope = new TransactionScope<TestItem>(inventory))
            {
                inventory.TryRemove(item.InstanceId);
                throw new Exception("Simulated error");
            }
        }
        catch
        {
            // 例外を無視
        }

        // 例外発生時はロールバック
        Assert.Equal(1, inventory.Count);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Transaction_WithTransfer_RollsBackOnFailure()
    {
        var source = CreateInventory(1);
        var dest = CreateInventory(2, capacity: 1);
        var item1 = new TestItem(1, "Sword");
        var item2 = new TestItem(2, "Shield");
        source.TryAdd(item1);
        source.TryAdd(item2);
        dest.TryAdd(new TestItem(3, "Existing")); // 容量を埋める

        var transferManager = new TransferManager<TestItem>();

        using (var scope = new TransactionScope<TestItem>(source, dest))
        {
            var result1 = transferManager.TryTransfer(source, dest, item1.InstanceId);
            // 容量不足で失敗
            if (!result1.Success)
            {
                // Complete()を呼ばないのでロールバック
            }
        }

        // 変更なし
        Assert.Equal(2, source.Count);
        Assert.Equal(1, dest.Count);
    }

    #endregion
}
