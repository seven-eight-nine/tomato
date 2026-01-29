using Tomato.UnitLODSystem;

namespace Tomato.GameLoop.Spawn;

/// <summary>
/// UnitのReady完了を通知するインターフェース。
/// UnitLODSystemと他システムの橋渡し役。
/// </summary>
public interface ISpawnCompletionHandler
{
    /// <summary>
    /// Unitが安定状態（IsStable）になった時に呼ばれる。
    /// </summary>
    /// <param name="unit">Unit</param>
    void OnUnitReady(Unit unit);

    /// <summary>
    /// Unitがアンロードを開始した時に呼ばれる。
    /// </summary>
    /// <param name="unit">Unit</param>
    void OnUnitUnloading(Unit unit);

    /// <summary>
    /// Unitが完全にアンロードされた時に呼ばれる。
    /// </summary>
    /// <param name="unit">Unit</param>
    void OnUnitRemoved(Unit unit);
}
