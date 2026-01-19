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
    /// <returns>SpawnされたEntityのVoidHandle</returns>
    VoidHandle Spawn();

    /// <summary>
    /// EntityをDespawnする。
    /// </summary>
    /// <param name="handle">DespawnするEntityのVoidHandle</param>
    /// <returns>Despawn成功時true</returns>
    bool Despawn(VoidHandle handle);
}
