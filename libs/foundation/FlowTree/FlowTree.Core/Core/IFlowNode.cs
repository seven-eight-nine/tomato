namespace Tomato.FlowTree;

/// <summary>
/// フローツリーノードの基底インターフェース。
/// </summary>
public interface IFlowNode
{
    /// <summary>
    /// ノードを評価する（ゼロGC）。
    /// </summary>
    /// <param name="context">実行コンテキスト</param>
    /// <returns>ノードの状態</returns>
    NodeStatus Tick(ref FlowContext context);

    /// <summary>
    /// 状態をリセットする。
    /// </summary>
    void Reset();
}
