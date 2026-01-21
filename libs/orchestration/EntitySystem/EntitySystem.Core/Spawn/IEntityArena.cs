using Tomato.EntityHandleSystem;

namespace Tomato.EntitySystem.Spawn;

/// <summary>
/// EntityのSpawn/Despawnを行うSpawnerインターフェース。
/// </summary>
public interface IEntitySpawner
{
    /// <summary>
    /// 新しいEntityをSpawnする。
    /// </summary>
    /// <returns>SpawnされたEntityのAnyHandle</returns>
    AnyHandle Spawn();

    /// <summary>
    /// EntityをDespawnする。
    /// </summary>
    /// <param name="handle">DespawnするEntityのAnyHandle</param>
    /// <returns>Despawn成功時true</returns>
    bool Despawn(AnyHandle handle);
}
