using System;
using System.Collections.Generic;
using Tomato.Time;

namespace Tomato.FlowTree;

/// <summary>
/// 指定tick数待機するノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class WaitNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly TickDuration _duration;
    private readonly List<int> _elapsedStack;

    /// <summary>
    /// WaitNodeを作成する。
    /// </summary>
    /// <param name="duration">待機tick数</param>
    public WaitNode(TickDuration duration)
    {
        if (duration.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative.");

        _duration = duration;
        _elapsedStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        _elapsedStack[depth] += context.DeltaTicks;

        if (_elapsedStack[depth] >= _duration.Value)
        {
            _elapsedStack[depth] = 0;
            return NodeStatus.Success;
        }

        return NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _elapsedStack.Count; i++)
        {
            _elapsedStack[i] = 0;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_elapsedStack.Count <= depth)
        {
            _elapsedStack.Add(0);
        }
    }
}
