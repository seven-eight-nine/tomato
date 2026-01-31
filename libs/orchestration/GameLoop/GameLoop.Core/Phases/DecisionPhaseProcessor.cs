using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Providers;
using Tomato.SystemPipeline;
using Tomato.ActionExecutionSystem;
using Tomato.ActionSelector;

namespace Tomato.GameLoop.Phases;

/// <summary>
/// アクション決定システム。
/// 各EntityのActionStateMachineを確認し、新しいアクションを決定。
/// 【読み取り専用】【並列化可能】
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class DecisionSystem<TCategory> : IParallelSystem
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _entityRegistry;
    private readonly ActionSelector<TCategory, InputState, GameState> _selectionEngine;
    private readonly IInputProvider _inputProvider;
    private readonly DecisionResultBuffer<TCategory> _resultBuffer;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// 決定結果バッファ。
    /// </summary>
    public DecisionResultBuffer<TCategory> ResultBuffer => _resultBuffer;

    /// <summary>
    /// DecisionSystemを生成する。
    /// </summary>
    public DecisionSystem(
        EntityContextRegistry<TCategory> entityRegistry,
        ActionSelector<TCategory, InputState, GameState> selectionEngine,
        IInputProvider inputProvider)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _selectionEngine = selectionEngine ?? throw new ArgumentNullException(nameof(selectionEngine));
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _resultBuffer = new DecisionResultBuffer<TCategory>();
    }

    /// <inheritdoc/>
    public void ProcessEntity(AnyHandle handle, in SystemContext context)
    {
        if (!_entityRegistry.TryGetContext(handle, out var entityContext) || entityContext == null)
            return;

        if (!entityContext.IsActive)
            return;

        // GameStateを構築
        var inputState = _inputProvider.GetInputState(handle);

        var gameState = new GameState(
            inputState,
            resources: null,
            context.DeltaTicks,
            context.CurrentTick);

        // ジャッジメント群を取得
        var judgments = GetJudgmentsForEntity(entityContext);

        if (judgments.Length == 0)
            return;

        // JudgmentListを構築
        var judgmentList = new JudgmentList<TCategory, InputState, GameState>();
        foreach (var j in judgments)
        {
            judgmentList.Add(j);
        }

        // FrameStateを構築
        var frameState = new FrameState<InputState, GameState>(
            inputState,
            gameState,
            context.DeltaTicks,
            context.CurrentTick);

        // アクション選択
        var result = _selectionEngine.ProcessFrame(judgmentList, in frameState);

        // 結果をバッファに保存（スレッドセーフ）
        _resultBuffer.Store(handle, result);
    }

    /// <summary>
    /// 処理前にバッファをクリアする。
    /// SystemGroupから呼び出される前に手動で呼び出す。
    /// </summary>
    public void ClearBuffer()
    {
        _resultBuffer.Clear();
    }

    private IActionJudgment<TCategory, InputState, GameState>[] GetJudgmentsForEntity(EntityContext<TCategory> context)
    {
        // 実行中アクションの遷移可能ジャッジメントを収集
        var transitionJudgments = new List<IActionJudgment<TCategory, InputState, GameState>>();

        foreach (TCategory category in GetEnumValues())
        {
            var currentAction = context.ActionStateMachine.GetCurrentAction(category);
            if (currentAction is IRunningAction<TCategory> running && running.CanCancel)
            {
                foreach (var j in running.GetTransitionableJudgments())
                {
                    transitionJudgments.Add(j);
                }
            }
        }

        // 遷移可能なジャッジメントがあればそれを、なければデフォルトを返す
        return transitionJudgments.Count > 0
            ? transitionJudgments.ToArray()
            : context.Judgments;
    }

    private static TCategory[] GetEnumValues()
    {
        return (TCategory[])Enum.GetValues(typeof(TCategory));
    }
}

/// <summary>
/// 決定結果を一時保存するバッファ（スレッドセーフ）。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class DecisionResultBuffer<TCategory> where TCategory : struct, Enum
{
    private readonly ConcurrentDictionary<AnyHandle, SelectionResult<TCategory, InputState, GameState>> _results;

    public DecisionResultBuffer()
    {
        _results = new ConcurrentDictionary<AnyHandle, SelectionResult<TCategory, InputState, GameState>>();
    }

    public void Clear() => _results.Clear();

    public void Store(AnyHandle handle, SelectionResult<TCategory, InputState, GameState> result)
    {
        _results[handle] = result;
    }

    public bool TryGet(AnyHandle handle, out SelectionResult<TCategory, InputState, GameState> result)
    {
        return _results.TryGetValue(handle, out result);
    }
}
