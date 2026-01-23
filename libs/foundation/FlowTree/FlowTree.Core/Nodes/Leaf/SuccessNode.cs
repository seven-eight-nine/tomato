namespace Tomato.FlowTree;

/// <summary>
/// 即座にSuccessを返すノード。
/// </summary>
public sealed class SuccessNode : IFlowNode
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly SuccessNode Instance = new();

    /// <summary>
    /// SuccessNodeを作成する。
    /// </summary>
    public SuccessNode()
    {
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        return NodeStatus.Success;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // 状態なし
    }
}
