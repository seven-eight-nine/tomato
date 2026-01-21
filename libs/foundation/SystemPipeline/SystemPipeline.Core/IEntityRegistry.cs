using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline;

/// <summary>
/// エンティティの集合を管理するレジストリインターフェース。
/// システムがエンティティにアクセスするための抽象化レイヤーを提供します。
/// </summary>
public interface IEntityRegistry
{
    /// <summary>
    /// すべてのアクティブなエンティティを取得します。
    /// </summary>
    /// <returns>VoidHandleのコレクション</returns>
    IReadOnlyList<VoidHandle> GetAllEntities();

    /// <summary>
    /// 指定した型のエンティティを取得します。
    /// </summary>
    /// <typeparam name="TArena">Arena型</typeparam>
    /// <returns>VoidHandleのコレクション</returns>
    IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class;
}
