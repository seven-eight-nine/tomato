namespace Tomato.FlowTree;

/// <summary>
/// 即座にFailureを返すノード。
/// </summary>
public sealed class FailureNode : IFlowNode
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly FailureNode Instance = new();

    /// <summary>
    /// FailureNodeを作成する。
    /// </summary>
    public FailureNode()
    {
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        // 状態なし
    }
}
