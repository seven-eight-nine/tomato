using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// エンティティフィルタリング用のクエリインターフェース。
/// システムが処理対象のエンティティを絞り込むために使用します。
/// </summary>
public interface IEntityQuery
{
    /// <summary>
    /// エンティティをフィルタリングします。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="entities">入力エンティティ</param>
    /// <returns>フィルタリングされたエンティティ</returns>
    IEnumerable<VoidHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<VoidHandle> entities);
}
