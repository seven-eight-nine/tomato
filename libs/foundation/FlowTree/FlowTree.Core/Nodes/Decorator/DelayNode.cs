using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 指定時間遅延してから子ノードを実行するノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class DelayNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly float _delay;
    private readonly List<float> _elapsedStack;
    private readonly List<bool> _delayCompleteStack;

    /// <summary>
    /// DelayNodeを作成する。
    /// </summary>
    /// <param name="delay">遅延時間（秒）</param>
    /// <param name="child">子ノード</param>
    public DelayNode(float delay, IFlowNode child)
    {
        if (delay < 0)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be non-negative.");

        _child = child ?? throw new ArgumentNullException(nameof(child));
        _delay = delay;
        _elapsedStack = new List<float>(InitialCapacity) { 0f };
        _delayCompleteStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        if (!_delayCompleteStack[depth])
        {
            _elapsedStack[depth] += context.DeltaTime;

            if (_elapsedStack[depth] < _delay)
                return NodeStatus.Running;

            _delayCompleteStack[depth] = true;
        }

        var status = _child.Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _elapsedStack[depth] = 0;
            _delayCompleteStack[depth] = false;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset(bool fireExitEvents = true)
    {
        for (int i = 0; i < _elapsedStack.Count; i++)
        {
            _elapsedStack[i] = 0;
            _delayCompleteStack[i] = false;
        }
        _child.Reset(fireExitEvents);
    }

    private void EnsureDepth(int depth)
    {
        while (_elapsedStack.Count <= depth)
        {
            _elapsedStack.Add(0f);
            _delayCompleteStack.Add(false);
        }
    }
}
