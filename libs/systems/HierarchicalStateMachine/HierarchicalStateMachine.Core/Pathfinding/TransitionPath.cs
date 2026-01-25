using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// パス探索の結果を表すクラス。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class TransitionPath<TContext>
{
    /// <summary>
    /// 探索結果。
    /// </summary>
    public PathfindingResult Result { get; }

    /// <summary>
    /// パスを構成する遷移のリスト。
    /// </summary>
    public IReadOnlyList<Transition<TContext>> Transitions { get; }

    /// <summary>
    /// パスを構成する状態IDのリスト（開始状態から終了状態まで）。
    /// </summary>
    public IReadOnlyList<StateId> States { get; }

    /// <summary>
    /// パスの総コスト。
    /// </summary>
    public float TotalCost { get; }

    /// <summary>
    /// 探索で訪問したノード数。
    /// </summary>
    public int NodesVisited { get; }

    /// <summary>
    /// 探索にかかった時間（ミリ秒）。
    /// </summary>
    public double ElapsedMilliseconds { get; }

    /// <summary>
    /// パスが有効か（見つかったか）。
    /// </summary>
    public bool IsValid => Result == PathfindingResult.Found;

    /// <summary>
    /// パスが空か。
    /// </summary>
    public bool IsEmpty => Transitions.Count == 0;

    public TransitionPath(
        PathfindingResult result,
        IReadOnlyList<Transition<TContext>> transitions,
        IReadOnlyList<StateId> states,
        float totalCost,
        int nodesVisited,
        double elapsedMilliseconds)
    {
        Result = result;
        Transitions = transitions;
        States = states;
        TotalCost = totalCost;
        NodesVisited = nodesVisited;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    /// <summary>
    /// 空のパス（見つからなかった場合）を作成。
    /// </summary>
    public static TransitionPath<TContext> Empty(PathfindingResult result, int nodesVisited = 0, double elapsed = 0)
    {
        return new TransitionPath<TContext>(
            result,
            System.Array.Empty<Transition<TContext>>(),
            System.Array.Empty<StateId>(),
            0f,
            nodesVisited,
            elapsed);
    }

    /// <summary>
    /// 開始状態のみのパス（開始 == ゴール の場合）。
    /// </summary>
    public static TransitionPath<TContext> StartOnly(StateId start, int nodesVisited = 1, double elapsed = 0)
    {
        return new TransitionPath<TContext>(
            PathfindingResult.Found,
            System.Array.Empty<Transition<TContext>>(),
            new[] { start },
            0f,
            nodesVisited,
            elapsed);
    }
}
