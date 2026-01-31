using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline;

/// <summary>
/// システムの実行を担当する静的クラス。
/// システムの種類に応じて適切な実行方法を選択します。
/// </summary>
public static class SystemExecutor
{
    /// <summary>
    /// システムを実行します。
    /// システムの型に応じて適切な実行メソッドが呼び出されます。
    /// </summary>
    /// <param name="system">実行するシステム</param>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="context">実行コンテキスト</param>
    public static void Execute(ISystem system, IEntityRegistry registry, in SystemContext context)
    {
        if (!system.IsEnabled) return;

        switch (system)
        {
            case IOrderedSerialSystem orderedSerialSystem:
                ExecuteOrderedSerialSystem(orderedSerialSystem, registry, in context);
                break;

            case ISerialSystem serialSystem:
                ExecuteSerialSystem(serialSystem, registry, in context);
                break;

            case IParallelSystem parallelSystem:
                ExecuteParallelSystem(parallelSystem, registry, in context);
                break;

            case IMessageQueueSystem messageQueueSystem:
                ExecuteMessageQueueSystem(messageQueueSystem, registry, in context);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown system type: {system.GetType().Name}. " +
                    "System must implement ISerialSystem, IParallelSystem, or IMessageQueueSystem.");
        }
    }

    private static IReadOnlyList<AnyHandle> GetFilteredEntities(
        ISystem system,
        IEntityRegistry registry,
        in SystemContext context)
    {
        var query = system.Query;

        // クエリがなければ全エンティティを返す
        if (query == null)
        {
            return registry.GetAllEntities();
        }

        // クエリキャッシュがあればキャッシュ経由で取得
        if (context.QueryCache != null)
        {
            return context.QueryCache.GetOrExecute(query, registry, context.CurrentTick.Value);
        }

        // キャッシュがなければ直接フィルタリング
        var allEntities = registry.GetAllEntities();
        var result = new List<AnyHandle>();
        foreach (var handle in query.Filter(registry, allEntities))
        {
            result.Add(handle);
        }
        return result;
    }

    private static void ExecuteSerialSystem(
        ISerialSystem system,
        IEntityRegistry registry,
        in SystemContext context)
    {
        var entities = GetFilteredEntities(system, registry, in context);
        system.ProcessSerial(registry, entities, in context);
    }

    private static void ExecuteOrderedSerialSystem(
        IOrderedSerialSystem system,
        IEntityRegistry registry,
        in SystemContext context)
    {
        var entities = GetFilteredEntities(system, registry, in context);
        var orderedEntities = new List<AnyHandle>(entities.Count);
        system.OrderEntities(entities, orderedEntities);
        system.ProcessSerial(registry, orderedEntities, in context);
    }

    private static void ExecuteParallelSystem(
        IParallelSystem system,
        IEntityRegistry registry,
        in SystemContext context)
    {
        var entities = GetFilteredEntities(system, registry, in context);
        if (entities.Count == 0) return;

        // Copy context for lambda capture
        var localContext = context;
        var cancellationToken = context.CancellationToken;

        // 並列実行
        Parallel.For(0, entities.Count, i =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            system.ProcessEntity(entities[i], in localContext);
        });
    }

    private static void ExecuteMessageQueueSystem(
        IMessageQueueSystem system,
        IEntityRegistry registry,
        in SystemContext context)
    {
        system.ProcessMessages(registry, in context);
    }
}
