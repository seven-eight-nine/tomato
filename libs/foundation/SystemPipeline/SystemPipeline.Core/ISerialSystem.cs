using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline;

/// <summary>
/// 直列処理システムのインターフェース。
/// エンティティを順番に1つずつ処理します。
/// </summary>
public interface ISerialSystem : ISystem
{
    /// <summary>
    /// エンティティを直列に処理します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="entities">処理対象のエンティティ</param>
    /// <param name="context">実行コンテキスト</param>
    void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context);
}

/// <summary>
/// 順序制御付きの直列処理システム。
/// エンティティの処理順序をカスタマイズできます（例：トポロジカルソート）。
/// </summary>
public interface IOrderedSerialSystem : ISerialSystem
{
    /// <summary>
    /// エンティティの処理順序を決定します。
    /// </summary>
    /// <param name="input">入力エンティティ</param>
    /// <param name="output">順序付けされた出力エンティティ</param>
    void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output);
}
