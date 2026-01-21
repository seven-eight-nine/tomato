namespace Tomato.EntityHandleSystem;

/// <summary>
/// 生成されるEntity Arena型の抽象基底クラス。
/// ArenaBaseを継承し、エンティティ固有の機能を提供します。
/// </summary>
/// <typeparam name="TEntity">管理対象のエンティティ型</typeparam>
/// <typeparam name="THandle">エンティティのハンドル型</typeparam>
public abstract class EntityArenaBase<TEntity, THandle> : Tomato.HandleSystem.ArenaBase<TEntity, THandle>
    where TEntity : new()
{
    /// <summary>
    /// プールされたエンティティインスタンスの配列への参照。
    /// </summary>
    protected TEntity[] _entities => _items;

    /// <summary>
    /// EntityArenaBaseクラスの新しいインスタンスを初期化します。
    /// </summary>
    protected EntityArenaBase(
        int initialCapacity,
        Tomato.HandleSystem.RefAction<TEntity> onSpawn,
        Tomato.HandleSystem.RefAction<TEntity> onDespawn)
        : base(initialCapacity, onSpawn, onDespawn)
    {
    }

    /// <summary>
    /// 指定インデックスのエンティティへの参照を検証なしで取得します。
    /// </summary>
    protected ref TEntity GetEntityRefUnchecked(int index)
    {
        return ref GetItemRefUnchecked(index);
    }

    /// <summary>
    /// 検証付きでエンティティ参照を取得します。
    /// </summary>
    protected ref TEntity GetEntityRef(int index, int generation, out bool valid)
    {
        return ref GetItemRef(index, generation, out valid);
    }
}
