using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 階層型ステートマシン用のA*パス探索エンジン。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class HierarchicalPathFinder<TContext>
{
    private readonly StateGraph<TContext> _graph;
    private readonly IHeuristic<TContext> _heuristic;

    public HierarchicalPathFinder(StateGraph<TContext> graph, IHeuristic<TContext>? heuristic = null)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _heuristic = heuristic ?? ZeroHeuristic<TContext>.Instance;
    }

    /// <summary>
    /// 指定した開始状態からゴール状態までの最短パスを探索。
    /// </summary>
    public TransitionPath<TContext> FindPath(
        StateId start,
        StateId goal,
        TContext context,
        PathfindingOptions? options = null)
    {
        options ??= PathfindingOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        // 開始状態の検証
        if (!_graph.HasState(start))
        {
            return TransitionPath<TContext>.Empty(PathfindingResult.InvalidStart);
        }

        // ゴール状態の検証
        if (!_graph.HasState(goal))
        {
            return TransitionPath<TContext>.Empty(PathfindingResult.InvalidGoal);
        }

        // 開始 == ゴール
        if (start == goal)
        {
            return TransitionPath<TContext>.StartOnly(start, 1, stopwatch.Elapsed.TotalMilliseconds);
        }

        var openSet = new PriorityQueue<StateId>();
        var cameFrom = new Dictionary<StateId, (StateId From, Transition<TContext> Transition)>();
        var gScore = new Dictionary<StateId, float> { [start] = 0 };
        var fScore = new Dictionary<StateId, float>();

        var h = _heuristic.Estimate(start, goal, context);
        fScore[start] = h;
        openSet.Enqueue(start, h);

        int iterations = 0;
        int nodesVisited = 0;
        StateId? bestPartialState = null;
        float bestPartialHeuristic = float.MaxValue;

        while (!openSet.IsEmpty)
        {
            // タイムアウトチェック
            if (options.TimeoutMilliseconds > 0 &&
                stopwatch.Elapsed.TotalMilliseconds > options.TimeoutMilliseconds)
            {
                if (options.AllowPartialPath && bestPartialState.HasValue)
                {
                    return ReconstructPath(
                        cameFrom,
                        bestPartialState.Value,
                        start,
                        gScore[bestPartialState.Value],
                        nodesVisited,
                        stopwatch.Elapsed.TotalMilliseconds,
                        PathfindingResult.Timeout);
                }
                return TransitionPath<TContext>.Empty(
                    PathfindingResult.Timeout,
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            // 最大反復回数チェック
            if (++iterations > options.MaxIterations)
            {
                if (options.AllowPartialPath && bestPartialState.HasValue)
                {
                    return ReconstructPath(
                        cameFrom,
                        bestPartialState.Value,
                        start,
                        gScore[bestPartialState.Value],
                        nodesVisited,
                        stopwatch.Elapsed.TotalMilliseconds,
                        PathfindingResult.Timeout);
                }
                return TransitionPath<TContext>.Empty(
                    PathfindingResult.Timeout,
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            var current = openSet.Dequeue();
            nodesVisited++;

            // ゴールに到達
            if (current == goal)
            {
                return ReconstructPath(
                    cameFrom,
                    goal,
                    start,
                    gScore[goal],
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds,
                    PathfindingResult.Found);
            }

            // 部分パス用に最良の状態を記録
            var currentH = _heuristic.Estimate(current, goal, context);
            if (currentH < bestPartialHeuristic)
            {
                bestPartialHeuristic = currentH;
                bestPartialState = current;
            }

            // 隣接状態を探索
            foreach (var transition in _graph.GetTransitionsFrom(current))
            {
                // 遷移条件を確認
                if (!transition.CanTransition(context))
                    continue;

                var neighbor = transition.To;
                var tentativeG = gScore[current] + transition.GetCost(context);

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = (current, transition);
                    gScore[neighbor] = tentativeG;
                    var neighborH = _heuristic.Estimate(neighbor, goal, context);
                    fScore[neighbor] = tentativeG + neighborH;
                    openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        // パスが見つからなかった
        if (options.AllowPartialPath && bestPartialState.HasValue)
        {
            return ReconstructPath(
                cameFrom,
                bestPartialState.Value,
                start,
                gScore[bestPartialState.Value],
                nodesVisited,
                stopwatch.Elapsed.TotalMilliseconds,
                PathfindingResult.NotFound);
        }

        return TransitionPath<TContext>.Empty(
            PathfindingResult.NotFound,
            nodesVisited,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// 複数のゴール状態のいずれかへの最短パスを探索。
    /// </summary>
    public TransitionPath<TContext> FindPathToAny(
        StateId start,
        IEnumerable<StateId> goals,
        TContext context,
        PathfindingOptions? options = null)
    {
        options ??= PathfindingOptions.Default;
        var goalSet = new HashSet<StateId>(goals);

        if (goalSet.Count == 0)
        {
            return TransitionPath<TContext>.Empty(PathfindingResult.InvalidGoal);
        }

        var stopwatch = Stopwatch.StartNew();

        if (!_graph.HasState(start))
        {
            return TransitionPath<TContext>.Empty(PathfindingResult.InvalidStart);
        }

        if (goalSet.Contains(start))
        {
            return TransitionPath<TContext>.StartOnly(start, 1, stopwatch.Elapsed.TotalMilliseconds);
        }

        var openSet = new PriorityQueue<StateId>();
        var cameFrom = new Dictionary<StateId, (StateId From, Transition<TContext> Transition)>();
        var gScore = new Dictionary<StateId, float> { [start] = 0 };

        // 最も近いゴールへのヒューリスティックを使用
        float minH = float.MaxValue;
        foreach (var g in goalSet)
        {
            if (_graph.HasState(g))
            {
                var h = _heuristic.Estimate(start, g, context);
                if (h < minH) minH = h;
            }
        }
        openSet.Enqueue(start, minH);

        int iterations = 0;
        int nodesVisited = 0;

        while (!openSet.IsEmpty)
        {
            if (options.TimeoutMilliseconds > 0 &&
                stopwatch.Elapsed.TotalMilliseconds > options.TimeoutMilliseconds)
            {
                return TransitionPath<TContext>.Empty(
                    PathfindingResult.Timeout,
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            if (++iterations > options.MaxIterations)
            {
                return TransitionPath<TContext>.Empty(
                    PathfindingResult.Timeout,
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            var current = openSet.Dequeue();
            nodesVisited++;

            if (goalSet.Contains(current))
            {
                return ReconstructPath(
                    cameFrom,
                    current,
                    start,
                    gScore[current],
                    nodesVisited,
                    stopwatch.Elapsed.TotalMilliseconds,
                    PathfindingResult.Found);
            }

            foreach (var transition in _graph.GetTransitionsFrom(current))
            {
                if (!transition.CanTransition(context))
                    continue;

                var neighbor = transition.To;
                var tentativeG = gScore[current] + transition.GetCost(context);

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = (current, transition);
                    gScore[neighbor] = tentativeG;

                    minH = float.MaxValue;
                    foreach (var g in goalSet)
                    {
                        if (_graph.HasState(g))
                        {
                            var h = _heuristic.Estimate(neighbor, g, context);
                            if (h < minH) minH = h;
                        }
                    }
                    openSet.Enqueue(neighbor, tentativeG + minH);
                }
            }
        }

        return TransitionPath<TContext>.Empty(
            PathfindingResult.NotFound,
            nodesVisited,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private TransitionPath<TContext> ReconstructPath(
        Dictionary<StateId, (StateId From, Transition<TContext> Transition)> cameFrom,
        StateId current,
        StateId start,
        float totalCost,
        int nodesVisited,
        double elapsedMs,
        PathfindingResult result)
    {
        var transitions = new List<Transition<TContext>>();
        var states = new List<StateId> { current };

        while (cameFrom.TryGetValue(current, out var prev))
        {
            transitions.Add(prev.Transition);
            states.Add(prev.From);
            current = prev.From;
        }

        transitions.Reverse();
        states.Reverse();

        return new TransitionPath<TContext>(
            result,
            transitions,
            states,
            totalCost,
            nodesVisited,
            elapsedMs);
    }
}
