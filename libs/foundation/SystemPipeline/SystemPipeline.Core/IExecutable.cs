namespace Tomato.SystemPipeline;

/// <summary>
/// 実行可能な要素の基底インターフェース。
/// ISystem と ISystemGroup の共通親として機能します。
/// </summary>
public interface IExecutable
{
    /// <summary>
    /// 有効かどうか。falseの場合、実行がスキップされます。
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 実行します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="context">実行コンテキスト</param>
    void Execute(IEntityRegistry registry, in SystemContext context);
}
