using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.EntitySystem.Context;
using Tomato.EntitySystem.Providers;
using Tomato.SystemPipeline;
using Tomato.ActionExecutionSystem;

namespace Tomato.EntitySystem.Phases;

/// <summary>
/// アクション実行システム。
/// DecisionSystemで決定されたアクションを実行。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class ExecutionSystem<TCategory> : ISerialSystem
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _entityRegistry;
    private readonly DecisionResultBuffer<TCategory> _resultBuffer;
    private readonly IActionFactory<TCategory> _actionFactory;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// ExecutionSystemを生成する。
    /// </summary>
    public ExecutionSystem(
        EntityContextRegistry<TCategory> entityRegistry,
        DecisionResultBuffer<TCategory> resultBuffer,
        IActionFactory<TCategory> actionFactory)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _resultBuffer = resultBuffer ?? throw new ArgumentNullException(nameof(resultBuffer));
        _actionFactory = actionFactory ?? throw new ArgumentNullException(nameof(actionFactory));
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<VoidHandle> entities,
        in SystemContext context)
    {
        foreach (var handle in entities)
        {
            if (!_entityRegistry.TryGetContext(handle, out var entityContext) || entityContext == null)
                continue;

            if (!entityContext.IsActive)
                continue;

            // 1. 決定されたアクションを開始
            if (_resultBuffer.TryGet(handle, out var result))
            {
                foreach (TCategory category in GetEnumValues())
                {
                    if (result.TryGetRequested(category, out var judgment))
                    {
                        // 新しいアクションを生成
                        var action = _actionFactory.Create(judgment.ActionId, category);
                        if (action != null)
                        {
                            // ActionStateMachineに登録
                            entityContext.ActionStateMachine.StartAction(category, action);
                        }
                    }
                }
            }

            // 2. 実行中アクションを更新
            entityContext.ActionStateMachine.Update(context.DeltaTime);
        }
    }

    private static TCategory[] GetEnumValues()
    {
        return (TCategory[])Enum.GetValues(typeof(TCategory));
    }
}
