namespace Tomato.FlowTree;

/// <summary>
/// ツリーの早期終了を要求するノード。
/// 実行されると、現在のツリー（またはサブツリー）をリセットし、
/// 指定されたステータスを返す。EventNodeのonExitも発火される。
/// </summary>
public sealed class ReturnNode : IFlowNode
{
    private readonly NodeStatus _status;

    /// <summary>
    /// ReturnNodeを作成する。
    /// </summary>
    /// <param name="status">返すステータス（SuccessまたはFailure）</param>
    public ReturnNode(NodeStatus status)
    {
        _status = status;
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        context.ReturnRequested = true;
        context.ReturnStatus = _status;
        return _status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        // ステートレスノードなのでリセット不要
    }
}
