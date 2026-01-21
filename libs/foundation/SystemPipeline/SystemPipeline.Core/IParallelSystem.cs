using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline;

/// <summary>
/// 並列処理システムのインターフェース。
/// 各エンティティを独立して並列に処理します。
/// </summary>
public interface IParallelSystem : ISystem
{
    /// <summary>
    /// 単一のエンティティを処理します。
    /// この処理は他のエンティティと並列に実行される可能性があります。
    ///
    /// <para>注意:</para>
    /// <list type="bullet">
    ///   <item><description>スレッドセーフな実装が必要です</description></item>
    ///   <item><description>共有状態への書き込みは避けてください</description></item>
    ///   <item><description>読み取り専用の操作が推奨されます</description></item>
    /// </list>
    /// </summary>
    /// <param name="handle">処理対象のエンティティ</param>
    /// <param name="context">実行コンテキスト</param>
    void ProcessEntity(AnyHandle handle, in SystemContext context);
}
