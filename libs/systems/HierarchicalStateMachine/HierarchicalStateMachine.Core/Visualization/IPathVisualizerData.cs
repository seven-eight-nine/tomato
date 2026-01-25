using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// パス可視化用のデータインターフェース。
/// 探索過程のデバッグに使用。
/// </summary>
public interface IPathVisualizerData
{
    /// <summary>
    /// 探索で訪問したノードのリスト（訪問順）。
    /// </summary>
    IReadOnlyList<StateId> VisitedNodes { get; }

    /// <summary>
    /// 現在のオープンリストに含まれるノード。
    /// </summary>
    IReadOnlyList<StateId> OpenNodes { get; }

    /// <summary>
    /// 現在のクローズドリストに含まれるノード。
    /// </summary>
    IReadOnlyList<StateId> ClosedNodes { get; }

    /// <summary>
    /// 各ノードのgスコア（開始からのコスト）。
    /// </summary>
    IReadOnlyDictionary<StateId, float> GScores { get; }

    /// <summary>
    /// 各ノードのfスコア（gスコア + ヒューリスティック）。
    /// </summary>
    IReadOnlyDictionary<StateId, float> FScores { get; }

    /// <summary>
    /// 各ノードの親ノード（パス復元用）。
    /// </summary>
    IReadOnlyDictionary<StateId, StateId> CameFrom { get; }

    /// <summary>
    /// 開始ノード。
    /// </summary>
    StateId Start { get; }

    /// <summary>
    /// ゴールノード。
    /// </summary>
    StateId Goal { get; }

    /// <summary>
    /// 現在の反復回数。
    /// </summary>
    int CurrentIteration { get; }
}
