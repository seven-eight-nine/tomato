using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態グラフの可視化インターフェース。
/// デバッグやエディタ統合に使用。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public interface IGraphVisualizer<TContext>
{
    /// <summary>
    /// グラフの描画を開始。
    /// </summary>
    void BeginDraw();

    /// <summary>
    /// 状態ノードを描画。
    /// </summary>
    void DrawState(IState<TContext> state, bool isCurrent, bool isInPath);

    /// <summary>
    /// 遷移エッジを描画。
    /// </summary>
    void DrawTransition(Transition<TContext> transition, bool isInPath, float cost);

    /// <summary>
    /// グラフの描画を終了。
    /// </summary>
    void EndDraw();

    /// <summary>
    /// 状態グラフ全体を描画。
    /// </summary>
    void DrawGraph(
        StateGraph<TContext> graph,
        StateId? currentState = null,
        TransitionPath<TContext>? currentPath = null,
        TContext? context = default);
}
