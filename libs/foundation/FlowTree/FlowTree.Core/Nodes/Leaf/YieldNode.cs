using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 1TickだけRunningを返し、次のTickでSuccessを返すノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class YieldNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly List<bool> _hasYieldedStack;

    /// <summary>
    /// YieldNodeを作成する。
    /// </summary>
    public YieldNode()
    {
        _hasYieldedStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        if (!_hasYieldedStack[depth])
        {
            _hasYieldedStack[depth] = true;
            return NodeStatus.Running;
        }

        _hasYieldedStack[depth] = false;
        return NodeStatus.Success;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _hasYieldedStack.Count; i++)
        {
            _hasYieldedStack[i] = false;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_hasYieldedStack.Count <= depth)
        {
            _hasYieldedStack.Add(false);
        }
    }
}
