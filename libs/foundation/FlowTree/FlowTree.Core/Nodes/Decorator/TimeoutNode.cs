using System;
using System.Collections.Generic;
using Tomato.Time;

namespace Tomato.FlowTree;

/// <summary>
/// 指定tick数を超えた場合にFailureを返すノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class TimeoutNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly TickDuration _timeout;
    private readonly List<int> _elapsedStack;

    /// <summary>
    /// TimeoutNodeを作成する。
    /// </summary>
    /// <param name="timeout">タイムアウトtick数</param>
    /// <param name="child">子ノード</param>
    public TimeoutNode(TickDuration timeout, IFlowNode child)
    {
        if (timeout.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        _child = child ?? throw new ArgumentNullException(nameof(child));
        _timeout = timeout;
        _elapsedStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        _elapsedStack[depth] += context.DeltaTicks;

        if (_elapsedStack[depth] >= _timeout.Value)
        {
            _elapsedStack[depth] = 0;
            return NodeStatus.Failure;
        }

        var status = _child.Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _elapsedStack[depth] = 0;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _elapsedStack.Count; i++)
        {
            _elapsedStack[i] = 0;
        }
        _child.Reset(fireExitEvents);
    }

    private void EnsureDepth(int depth)
    {
        while (_elapsedStack.Count <= depth)
        {
            _elapsedStack.Add(0);
        }
    }
}
