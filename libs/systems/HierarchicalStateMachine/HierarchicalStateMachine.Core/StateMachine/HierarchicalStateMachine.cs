using System;
using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 階層型ステートマシン。
/// A*パス探索を使用して状態遷移を計画・実行する。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class HierarchicalStateMachine<TContext>
{
    private readonly StateGraph<TContext> _graph;
    private readonly HierarchicalPathFinder<TContext> _pathFinder;
    private StateId? _currentStateId;
    private TransitionPath<TContext>? _currentPath;
    private int _pathIndex;

    /// <summary>
    /// 現在の状態ID。
    /// </summary>
    public StateId? CurrentStateId => _currentStateId;

    /// <summary>
    /// 現在の状態。
    /// </summary>
    public IState<TContext>? CurrentState =>
        _currentStateId.HasValue ? _graph.GetState(_currentStateId.Value) : null;

    /// <summary>
    /// 計画されたパス。
    /// </summary>
    public TransitionPath<TContext>? CurrentPath => _currentPath;

    /// <summary>
    /// パス内の現在位置。
    /// </summary>
    public int CurrentPathIndex => _pathIndex;

    /// <summary>
    /// パスの実行が完了したか。
    /// </summary>
    public bool IsPathComplete =>
        _currentPath == null || _pathIndex >= _currentPath.Transitions.Count;

    /// <summary>
    /// 状態グラフ。
    /// </summary>
    public StateGraph<TContext> Graph => _graph;

    public HierarchicalStateMachine(StateGraph<TContext> graph, IHeuristic<TContext>? heuristic = null)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _pathFinder = new HierarchicalPathFinder<TContext>(graph, heuristic);
    }

    /// <summary>
    /// 初期状態を設定。
    /// </summary>
    public void Initialize(StateId initialState, TContext context)
    {
        if (!_graph.HasState(initialState))
            throw new InvalidOperationException($"State '{initialState}' not found in graph.");

        _currentStateId = initialState;
        _currentPath = null;
        _pathIndex = 0;

        var state = _graph.GetState(initialState);
        state?.OnEnter(context);
    }

    /// <summary>
    /// ゴール状態へのパスを計画。
    /// </summary>
    public TransitionPath<TContext> PlanPath(
        StateId goal,
        TContext context,
        PathfindingOptions? options = null)
    {
        if (!_currentStateId.HasValue)
            throw new InvalidOperationException("State machine not initialized.");

        var path = _pathFinder.FindPath(_currentStateId.Value, goal, context, options);
        if (path.IsValid)
        {
            _currentPath = path;
            _pathIndex = 0;
        }
        return path;
    }

    /// <summary>
    /// 複数のゴール状態のいずれかへのパスを計画。
    /// </summary>
    public TransitionPath<TContext> PlanPathToAny(
        IEnumerable<StateId> goals,
        TContext context,
        PathfindingOptions? options = null)
    {
        if (!_currentStateId.HasValue)
            throw new InvalidOperationException("State machine not initialized.");

        var path = _pathFinder.FindPathToAny(_currentStateId.Value, goals, context, options);
        if (path.IsValid)
        {
            _currentPath = path;
            _pathIndex = 0;
        }
        return path;
    }

    /// <summary>
    /// パスの次のステップを実行。
    /// </summary>
    /// <returns>遷移が成功したか</returns>
    public bool ExecuteNextStep(TContext context)
    {
        if (_currentPath == null || !_currentStateId.HasValue)
            return false;

        if (_pathIndex >= _currentPath.Transitions.Count)
            return false;

        var transition = _currentPath.Transitions[_pathIndex];

        // 遷移条件を再確認
        if (!transition.CanTransition(context))
            return false;

        // 現在の状態から出る
        var currentState = _graph.GetState(_currentStateId.Value);
        currentState?.OnExit(context);

        // 新しい状態に入る
        _currentStateId = transition.To;
        var newState = _graph.GetState(_currentStateId.Value);
        newState?.OnEnter(context);

        _pathIndex++;
        return true;
    }

    /// <summary>
    /// パスの全ステップを実行。
    /// </summary>
    /// <returns>すべての遷移が成功したか</returns>
    public bool ExecuteAllSteps(TContext context)
    {
        while (!IsPathComplete)
        {
            if (!ExecuteNextStep(context))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 即座に指定状態に遷移（パス探索なし）。
    /// </summary>
    public bool TransitionTo(StateId target, TContext context)
    {
        if (!_currentStateId.HasValue)
            throw new InvalidOperationException("State machine not initialized.");

        if (!_graph.HasState(target))
            return false;

        // 遷移が存在するか確認
        Transition<TContext>? foundTransition = null;
        foreach (var t in _graph.GetTransitionsFrom(_currentStateId.Value))
        {
            if (t.To == target && t.CanTransition(context))
            {
                foundTransition = t;
                break;
            }
        }

        if (foundTransition == null)
            return false;

        // 現在の状態から出る
        var currentState = _graph.GetState(_currentStateId.Value);
        currentState?.OnExit(context);

        // 新しい状態に入る
        _currentStateId = target;
        var newState = _graph.GetState(target);
        newState?.OnEnter(context);

        // パスをクリア
        _currentPath = null;
        _pathIndex = 0;

        return true;
    }

    /// <summary>
    /// 強制的に指定状態に遷移（遷移チェックなし）。
    /// </summary>
    public void ForceTransitionTo(StateId target, TContext context)
    {
        if (!_graph.HasState(target))
            throw new InvalidOperationException($"State '{target}' not found in graph.");

        if (_currentStateId.HasValue)
        {
            var currentState = _graph.GetState(_currentStateId.Value);
            currentState?.OnExit(context);
        }

        _currentStateId = target;
        var newState = _graph.GetState(target);
        newState?.OnEnter(context);

        _currentPath = null;
        _pathIndex = 0;
    }

    /// <summary>
    /// 現在の状態を更新。
    /// </summary>
    public void Update(TContext context, float deltaTime)
    {
        if (!_currentStateId.HasValue)
            return;

        var state = _graph.GetState(_currentStateId.Value);
        state?.OnUpdate(context, deltaTime);
    }

    /// <summary>
    /// 計画されたパスをクリア。
    /// </summary>
    public void ClearPath()
    {
        _currentPath = null;
        _pathIndex = 0;
    }

    /// <summary>
    /// パスを再計画（現在のパスのゴールに向かって）。
    /// </summary>
    public TransitionPath<TContext>? ReplanPath(TContext context, PathfindingOptions? options = null)
    {
        if (_currentPath == null || _currentPath.States.Count == 0)
            return null;

        var goal = _currentPath.States[_currentPath.States.Count - 1];
        return PlanPath(goal, context, options);
    }
}
