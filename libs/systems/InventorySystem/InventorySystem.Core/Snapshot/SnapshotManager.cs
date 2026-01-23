using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリスナップショットを管理するマネージャー。
/// 複数のインベントリのスナップショットを作成・保持・復元する。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class SnapshotManager<TItem>
    where TItem : class, IInventoryItem
{
    private readonly Dictionary<SnapshotId, InventorySnapshot> _snapshots;
    private readonly Dictionary<InventoryId, List<SnapshotId>> _snapshotsByInventory;
    private readonly int _maxSnapshotsPerInventory;

    /// <summary>
    /// SnapshotManagerを作成する。
    /// </summary>
    /// <param name="maxSnapshotsPerInventory">インベントリごとの最大スナップショット数（0で無制限）</param>
    public SnapshotManager(int maxSnapshotsPerInventory = 10)
    {
        _snapshots = new Dictionary<SnapshotId, InventorySnapshot>();
        _snapshotsByInventory = new Dictionary<InventoryId, List<SnapshotId>>();
        _maxSnapshotsPerInventory = maxSnapshotsPerInventory;
    }

    /// <summary>
    /// インベントリのスナップショットを作成して保存する。
    /// </summary>
    public SnapshotId CreateSnapshot(IInventory<TItem> inventory)
    {
        var snapshot = inventory.CreateSnapshot();
        _snapshots[snapshot.Id] = snapshot;

        if (!_snapshotsByInventory.TryGetValue(inventory.Id, out var snapshotList))
        {
            snapshotList = new List<SnapshotId>();
            _snapshotsByInventory[inventory.Id] = snapshotList;
        }

        snapshotList.Add(snapshot.Id);

        if (_maxSnapshotsPerInventory > 0 && snapshotList.Count > _maxSnapshotsPerInventory)
        {
            var oldestId = snapshotList[0];
            snapshotList.RemoveAt(0);
            _snapshots.Remove(oldestId);
        }

        return snapshot.Id;
    }

    /// <summary>
    /// スナップショットを取得する。
    /// </summary>
    public InventorySnapshot? GetSnapshot(SnapshotId snapshotId)
    {
        return _snapshots.TryGetValue(snapshotId, out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// 指定したIDのスナップショットが存在するかどうかを確認する。
    /// </summary>
    public bool HasSnapshot(SnapshotId snapshotId)
    {
        return _snapshots.ContainsKey(snapshotId);
    }

    /// <summary>
    /// 指定したインベントリにスナップショットが存在するかどうかを確認する。
    /// </summary>
    public bool HasSnapshotFor(InventoryId inventoryId)
    {
        return _snapshotsByInventory.TryGetValue(inventoryId, out var list) && list.Count > 0;
    }

    /// <summary>
    /// スナップショットからインベントリを復元する。
    /// </summary>
    public bool TryRestoreSnapshot(SnapshotId snapshotId, IInventory<TItem> inventory)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            return false;
        }

        inventory.RestoreFromSnapshot(snapshot);
        return true;
    }

    /// <summary>
    /// 指定したインベントリの最新スナップショットから復元する。
    /// </summary>
    public bool TryRestoreLatest(IInventory<TItem> inventory)
    {
        if (!_snapshotsByInventory.TryGetValue(inventory.Id, out var snapshotList) || snapshotList.Count == 0)
        {
            return false;
        }

        var latestId = snapshotList[snapshotList.Count - 1];
        return TryRestoreSnapshot(latestId, inventory);
    }

    /// <summary>
    /// スナップショットを削除する。
    /// </summary>
    public bool TryRemoveSnapshot(SnapshotId snapshotId)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            return false;
        }

        _snapshots.Remove(snapshotId);

        if (_snapshotsByInventory.TryGetValue(snapshot.InventoryId, out var snapshotList))
        {
            snapshotList.Remove(snapshotId);
        }

        return true;
    }

    /// <summary>
    /// 指定したインベントリのすべてのスナップショットを削除する。
    /// </summary>
    public int ClearSnapshots(InventoryId inventoryId)
    {
        if (!_snapshotsByInventory.TryGetValue(inventoryId, out var snapshotList))
        {
            return 0;
        }

        int count = snapshotList.Count;
        foreach (var snapshotId in snapshotList)
        {
            _snapshots.Remove(snapshotId);
        }

        snapshotList.Clear();
        return count;
    }

    /// <summary>
    /// すべてのスナップショットを削除する。
    /// </summary>
    public void ClearAll()
    {
        _snapshots.Clear();
        _snapshotsByInventory.Clear();
    }

    /// <summary>
    /// 保存されているスナップショットの数を取得する。
    /// </summary>
    public int SnapshotCount => _snapshots.Count;

    /// <summary>
    /// 指定したインベントリのスナップショット数を取得する。
    /// </summary>
    public int GetSnapshotCount(InventoryId inventoryId)
    {
        return _snapshotsByInventory.TryGetValue(inventoryId, out var list) ? list.Count : 0;
    }

    /// <summary>
    /// 指定したインベントリのスナップショットIDリストを取得する。
    /// </summary>
    public IReadOnlyList<SnapshotId> GetSnapshotIds(InventoryId inventoryId)
    {
        return _snapshotsByInventory.TryGetValue(inventoryId, out var list)
            ? list
            : System.Array.Empty<SnapshotId>();
    }
}
