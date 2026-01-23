namespace Tomato.InventorySystem;

/// <summary>
/// スナップショットの作成と復元が可能なオブジェクトのインターフェース。
/// </summary>
/// <typeparam name="T">スナップショットを作成するオブジェクトの型</typeparam>
public interface ISnapshotable<T>
{
    /// <summary>現在の状態からスナップショットを作成する</summary>
    InventorySnapshot CreateSnapshot();

    /// <summary>スナップショットから状態を復元する</summary>
    void RestoreFromSnapshot(InventorySnapshot snapshot);
}
