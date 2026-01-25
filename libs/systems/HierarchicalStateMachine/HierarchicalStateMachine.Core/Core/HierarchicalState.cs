namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 階層構造を持つ状態の実装。
/// 内部にサブグラフを持つことができる。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class HierarchicalState<TContext> : StateBase<TContext>, IHierarchicalState<TContext>
{
    public StateGraph<TContext>? SubGraph { get; }
    public StateId? InitialSubStateId { get; }
    public StateId? CurrentSubStateId { get; private set; }
    public bool HasSubGraph => SubGraph != null;

    /// <summary>
    /// サブグラフなしの階層状態を作成。
    /// </summary>
    public HierarchicalState(StateId id)
        : base(id)
    {
        SubGraph = null;
        InitialSubStateId = null;
    }

    /// <summary>
    /// サブグラフ付きの階層状態を作成。
    /// </summary>
    public HierarchicalState(StateId id, StateGraph<TContext> subGraph, StateId initialSubStateId)
        : base(id)
    {
        SubGraph = subGraph;
        InitialSubStateId = initialSubStateId;
    }

    public override void OnEnter(TContext context)
    {
        base.OnEnter(context);

        // サブグラフがある場合は初期状態に入る
        if (HasSubGraph && InitialSubStateId.HasValue)
        {
            EnterSubState(InitialSubStateId.Value, context);
        }
    }

    public override void OnExit(TContext context)
    {
        // サブグラフの現在状態から出る
        if (CurrentSubStateId.HasValue)
        {
            ExitSubState(context);
        }

        base.OnExit(context);
    }

    public override void OnUpdate(TContext context, float deltaTime)
    {
        base.OnUpdate(context, deltaTime);

        // サブグラフの現在状態を更新
        if (CurrentSubStateId.HasValue && SubGraph != null)
        {
            var currentSubState = SubGraph.GetState(CurrentSubStateId.Value);
            currentSubState?.OnUpdate(context, deltaTime);
        }
    }

    public void EnterSubState(StateId subStateId, TContext context)
    {
        if (SubGraph == null)
            return;

        // 現在の状態から出る
        if (CurrentSubStateId.HasValue)
        {
            var currentState = SubGraph.GetState(CurrentSubStateId.Value);
            currentState?.OnExit(context);
        }

        // 新しい状態に入る
        CurrentSubStateId = subStateId;
        var newState = SubGraph.GetState(subStateId);
        newState?.OnEnter(context);
    }

    public void ExitSubState(TContext context)
    {
        if (SubGraph == null || !CurrentSubStateId.HasValue)
            return;

        var currentState = SubGraph.GetState(CurrentSubStateId.Value);
        currentState?.OnExit(context);
        CurrentSubStateId = null;
    }
}
