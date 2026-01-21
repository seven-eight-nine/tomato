using System;
using Tomato.EntitySystem.Context;

namespace Tomato.EntitySystem.Spawn;

/// <summary>
/// Entityの初期化ロジックを定義するインターフェース。
/// ゲーム固有の初期化はこのインターフェースを実装して提供。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public interface IEntityInitializer<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// データリソースからEntityContextを初期化する。
    /// </summary>
    /// <param name="context">初期化対象のEntityContext</param>
    /// <param name="characterId">キャラクターID</param>
    /// <param name="dataResource">データリソース</param>
    void Initialize(EntityContext<TCategory> context, string characterId, object? dataResource);
}
