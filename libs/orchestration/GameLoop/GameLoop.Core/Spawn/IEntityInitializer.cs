using System;
using Tomato.GameLoop.Context;
using Tomato.UnitLODSystem;

namespace Tomato.GameLoop.Spawn;

/// <summary>
/// Entityの初期化ロジックを定義するインターフェース。
/// ゲーム固有の初期化はこのインターフェースを実装して提供。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public interface IEntityInitializer<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// UnitからEntityContextを初期化する。
    /// </summary>
    /// <param name="context">初期化対象のEntityContext</param>
    /// <param name="unit">Unit</param>
    /// <param name="dataResource">データリソース（オプション）</param>
    void Initialize(EntityContext<TCategory> context, Unit unit, object? dataResource);
}
