using System;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリのスナップショット。
/// シリアライズされたバイナリデータを保持し、状態の復元に使用する。
/// </summary>
public sealed class InventorySnapshot
{
    /// <summary>スナップショットのID</summary>
    public SnapshotId Id { get; }

    /// <summary>スナップショット作成時刻</summary>
    public DateTime CreatedAt { get; }

    /// <summary>対象インベントリのID</summary>
    public InventoryId InventoryId { get; }

    /// <summary>シリアライズされたデータ</summary>
    public byte[] Data { get; }

    public InventorySnapshot(SnapshotId id, InventoryId inventoryId, byte[] data)
    {
        Id = id;
        InventoryId = inventoryId;
        Data = data;
        CreatedAt = DateTime.UtcNow;
    }

    public override string ToString() =>
        $"InventorySnapshot(Id={Id}, InventoryId={InventoryId}, Size={Data.Length}, CreatedAt={CreatedAt:O})";
}
