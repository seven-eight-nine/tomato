using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリ操作のトランザクションを管理する。
/// 複数のインベントリに対する操作をアトミックにコミットまたはロールバックできる。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class InventoryTransaction<TItem> : IDisposable
    where TItem : class, IInventoryItem
{
    private readonly Dictionary<InventoryId, InventorySnapshot> _snapshots = new();
    private readonly Dictionary<InventoryId, IInventory<TItem>> _inventories = new();
    private bool _committed;
    private bool _disposed;

    /// <summary>トランザクションがコミット済みかどうか</summary>
    public bool IsCommitted => _committed;

    /// <summary>トランザクションが破棄済みかどうか</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// トランザクションを開始する。
    /// </summary>
    public static InventoryTransaction<TItem> Begin()
    {
        return new InventoryTransaction<TItem>();
    }

    /// <summary>
    /// トランザクションを開始し、指定したインベントリを登録する。
    /// </summary>
    public static InventoryTransaction<TItem> Begin(params IInventory<TItem>[] inventories)
    {
        var transaction = new InventoryTransaction<TItem>();
        foreach (var inventory in inventories)
        {
            transaction.Enlist(inventory);
        }
        return transaction;
    }

    /// <summary>
    /// インベントリをトランザクションに登録する。
    /// 登録時点のスナップショットが保存される。
    /// </summary>
    /// <param name="inventory">登録するインベントリ</param>
    /// <exception cref="InvalidOperationException">既にコミットまたは破棄されている場合</exception>
    public void Enlist(IInventory<TItem> inventory)
    {
        ThrowIfFinalized();

        if (_snapshots.ContainsKey(inventory.Id))
        {
            return; // 既に登録済み
        }

        _snapshots[inventory.Id] = inventory.CreateSnapshot();
        _inventories[inventory.Id] = inventory;
    }

    /// <summary>
    /// 全ての変更をコミットする。
    /// コミット後はロールバックできない。
    /// </summary>
    /// <exception cref="InvalidOperationException">既にコミットまたは破棄されている場合</exception>
    public void Commit()
    {
        ThrowIfFinalized();

        _committed = true;
        // スナップショットは破棄しない（コミット済みフラグで管理）
    }

    /// <summary>
    /// 全ての変更をロールバックする。
    /// 登録時点の状態に戻る。
    /// </summary>
    /// <exception cref="InvalidOperationException">既にコミットまたは破棄されている場合</exception>
    public void Rollback()
    {
        ThrowIfFinalized();

        foreach (var kvp in _snapshots)
        {
            if (_inventories.TryGetValue(kvp.Key, out var inventory))
            {
                inventory.RestoreFromSnapshot(kvp.Value);
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// リソースを解放する。
    /// コミットされていない場合は自動的にロールバックされる。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_committed)
        {
            // コミットされていない場合はロールバック
            foreach (var kvp in _snapshots)
            {
                if (_inventories.TryGetValue(kvp.Key, out var inventory))
                {
                    inventory.RestoreFromSnapshot(kvp.Value);
                }
            }
        }

        _snapshots.Clear();
        _inventories.Clear();
        _disposed = true;
    }

    private void ThrowIfFinalized()
    {
        if (_disposed)
        {
            throw new InvalidOperationException("Transaction has already been disposed");
        }
        if (_committed)
        {
            throw new InvalidOperationException("Transaction has already been committed");
        }
    }
}

/// <summary>
/// トランザクションスコープを提供するヘルパー。
/// using文で使用し、スコープ終了時にコミットまたはロールバックする。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class TransactionScope<TItem> : IDisposable
    where TItem : class, IInventoryItem
{
    private readonly InventoryTransaction<TItem> _transaction;
    private bool _completed;

    /// <summary>
    /// トランザクションスコープを作成する。
    /// </summary>
    public TransactionScope(params IInventory<TItem>[] inventories)
    {
        _transaction = InventoryTransaction<TItem>.Begin(inventories);
    }

    /// <summary>
    /// 現在のトランザクション
    /// </summary>
    public InventoryTransaction<TItem> Transaction => _transaction;

    /// <summary>
    /// 追加のインベントリをトランザクションに登録する。
    /// </summary>
    public void Enlist(IInventory<TItem> inventory)
    {
        _transaction.Enlist(inventory);
    }

    /// <summary>
    /// スコープを完了としてマークする。
    /// Dispose時にコミットされる。
    /// </summary>
    public void Complete()
    {
        _completed = true;
    }

    /// <summary>
    /// リソースを解放する。
    /// Complete()が呼ばれていればコミット、そうでなければロールバック。
    /// </summary>
    public void Dispose()
    {
        if (_completed)
        {
            _transaction.Commit();
        }
        _transaction.Dispose();
    }
}
