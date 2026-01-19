using Tomato.SystemPipeline.Query;

namespace Tomato.SystemPipeline;

/// <summary>
/// システムの基底インターフェース。
/// 全てのシステム（Serial, Parallel, MessageQueue）がこのインターフェースを実装します。
/// </summary>
public interface ISystem
{
    /// <summary>
    /// システムが有効かどうかを取得または設定します。
    /// falseの場合、SystemGroupはこのシステムをスキップします。
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 処理対象エンティティを絞り込むためのクエリ。
    /// nullの場合は全エンティティが対象となります。
    /// </summary>
    IEntityQuery Query { get; }
}
