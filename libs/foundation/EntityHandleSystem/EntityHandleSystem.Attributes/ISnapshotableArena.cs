namespace Tomato.EntityHandleSystem;

/// <summary>
/// スナップショット可能なArenaのインターフェース。
/// Source Generatorにより自動実装されるか、手動で実装する。
/// </summary>
/// <typeparam name="TSnapshot">スナップショットの型</typeparam>
public interface ISnapshotableArena<TSnapshot>
    where TSnapshot : struct
{
    /// <summary>現在の状態をスナップショットとしてキャプチャ</summary>
    TSnapshot CaptureSnapshot();

    /// <summary>スナップショットから状態を復元</summary>
    void RestoreSnapshot(in TSnapshot snapshot);
}
