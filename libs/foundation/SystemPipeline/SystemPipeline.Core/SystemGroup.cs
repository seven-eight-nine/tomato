using System.Collections.Generic;

namespace Tomato.SystemPipeline;

/// <summary>
/// システムのグループ。
/// 複数のシステムを配列で定義し、順番に実行します。
///
/// <example>
/// 使用例:
/// <code>
/// // システムを作成
/// var collision = new CollisionSystem();
/// var message = new UpdateBeginQueueSystem(handlerRegistry);
/// var decision = new DecisionSystem();
///
/// // グループを配列で定義（実行順序は配列の順番）
/// var updateGroup = new SystemGroup(
///     collision, message, decision);
///
/// // 実行
/// pipeline.Execute(updateGroup, deltaTime);
/// </code>
/// </example>
/// </summary>
public sealed class SystemGroup
{
    private readonly List<ISystem> _systems;

    /// <summary>
    /// グループが有効かどうかを取得または設定します。
    /// falseの場合、Execute()は何もしません。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// グループ内のシステム数を取得します。
    /// </summary>
    public int Count => _systems.Count;

    /// <summary>
    /// 指定したシステムでSystemGroupを作成します。
    /// </summary>
    /// <param name="systems">実行順序に並べたシステム</param>
    public SystemGroup(params ISystem[] systems)
    {
        _systems = new List<ISystem>(systems);
    }

    /// <summary>
    /// グループ内の全システムを順番に実行します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="context">実行コンテキスト</param>
    public void Execute(IEntityRegistry registry, in SystemContext context)
    {
        if (!IsEnabled) return;

        foreach (var system in _systems)
        {
            if (context.CancellationToken.IsCancellationRequested) return;
            if (!system.IsEnabled) continue;

            SystemExecutor.Execute(system, registry, in context);
        }
    }

    /// <summary>
    /// システムを末尾に追加します。
    /// </summary>
    /// <param name="system">追加するシステム</param>
    public void Add(ISystem system)
    {
        _systems.Add(system);
    }

    /// <summary>
    /// 指定した位置にシステムを挿入します。
    /// </summary>
    /// <param name="index">挿入位置</param>
    /// <param name="system">挿入するシステム</param>
    public void Insert(int index, ISystem system)
    {
        _systems.Insert(index, system);
    }

    /// <summary>
    /// システムをグループから削除します。
    /// </summary>
    /// <param name="system">削除するシステム</param>
    /// <returns>削除された場合true</returns>
    public bool Remove(ISystem system)
    {
        return _systems.Remove(system);
    }

    /// <summary>
    /// 指定したインデックスのシステムを取得します。
    /// </summary>
    /// <param name="index">インデックス</param>
    /// <returns>システム</returns>
    public ISystem this[int index] => _systems[index];
}
